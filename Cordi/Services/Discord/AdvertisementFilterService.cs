using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cordi.Core;
using Dalamud.Plugin.Services;
using DSharpPlus.Entities;

namespace Cordi.Services.Discord;

public class AdvertisementFilterService
{
    private static readonly IPluginLog Logger = Service.Log;
    private readonly CordiPlugin _plugin;
    private readonly DiscordWebhookService _webhooks;

    private readonly ConcurrentDictionary<string, List<(string Content, DateTime Timestamp, ulong MessageId)>> _messageBuffer = new();
    private readonly ConcurrentDictionary<string, DateTime> _penaltyBox = new();

    public AdvertisementFilterService(CordiPlugin plugin, DiscordWebhookService webhooks)
    {
        _plugin = plugin;
        _webhooks = webhooks;
    }

    public bool IsAdvertisementOrPenalized(string senderName, string senderWorld, string sanitizedContent, bool channelFilterEnabled, DiscordChannel channel)
    {
        if (!_plugin.Config.AdvertisementFilter.Enabled || !channelFilterEnabled) return false;

        string senderKey = $"{senderName}@{senderWorld}";

        // Check Penalty Box
        if (_penaltyBox.TryGetValue(senderKey, out var releaseTime))
        {
            if (DateTime.UtcNow < releaseTime)
            {
                Logger.Info($"[AdvertisementFilter] Blocked message from penalized user {senderKey} until {releaseTime}");
                return true;
            }
            else
            {
                _penaltyBox.TryRemove(senderKey, out _);
            }
        }

        // Buffer logic for split messages
        var cleanupTime = DateTime.UtcNow.AddSeconds(-5); // 5 second buffer window
        var userMessages = _messageBuffer.GetOrAdd(senderKey, _ => new List<(string Content, DateTime Timestamp, ulong MessageId)>());

        bool isAd = false;
        string combinedMessage = string.Empty;

        lock (userMessages)
        {
            // Clean up old messages
            userMessages.RemoveAll(x => x.Timestamp < cleanupTime);

            // Construct combined message to check
            var recentContent = userMessages.Select(x => x.Content).ToList();
            recentContent.Add(sanitizedContent);
            combinedMessage = string.Join(" ", recentContent);
        }

        var filterConfig = _plugin.Config.AdvertisementFilter;
        bool checkAd(string content) => AdvertisementFilter.IsAdvertisement(
            content,
            filterConfig.ScoreThreshold,
            filterConfig.HighScoreRegexPatterns,
            filterConfig.HighScoreKeywords,
            filterConfig.MediumScoreRegexPatterns,
            filterConfig.MediumScoreKeywords,
            filterConfig.Whitelist);

        // Check individual first (optimization)
        isAd = checkAd(sanitizedContent);

        if (!isAd && combinedMessage != sanitizedContent)
        {
            isAd = checkAd(combinedMessage);
        }

        if (isAd)
        {
            Logger.Info($"[AdvertisementFilter] Blocked advertisement: {sanitizedContent.Substring(0, Math.Min(100, sanitizedContent.Length))}...");

            // Add to Penalty Box (10 seconds)
            _penaltyBox.AddOrUpdate(senderKey, DateTime.UtcNow.AddSeconds(10), (_, _) => DateTime.UtcNow.AddSeconds(10));

            // Retroactive Deletion: Delete previous messages
            lock (userMessages)
            {
                foreach (var (_, _, msgId) in userMessages)
                {
                    if (msgId != 0)
                    {
                        Task.Run(() => _webhooks.DeleteWebhookMessageAsync(channel, msgId));
                        Logger.Info($"[AdvertisementFilter] Retroactively deleted message ID: {msgId}");
                    }
                }
                userMessages.Clear();
            }
            return true;
        }

        return false;
    }

    public void AddMessageToBuffer(string senderName, string senderWorld, string sanitizedContent, ulong sentMessageId, bool channelFilterEnabled)
    {
        if (!_plugin.Config.AdvertisementFilter.Enabled || !channelFilterEnabled) return;

        string senderKey = $"{senderName}@{senderWorld}";
        var userMessages = _messageBuffer.GetOrAdd(senderKey, _ => new List<(string Content, DateTime Timestamp, ulong MessageId)>());

        lock (userMessages)
        {
            userMessages.Add((sanitizedContent, DateTime.UtcNow, sentMessageId));
        }
    }
}
