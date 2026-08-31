using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Domain;
using Cordi.Services.Discord.Webhooks;
using Dalamud.Plugin.Services;

namespace Cordi.Services.Discord;

public class AdvertisementFilterService
{
    private static readonly IPluginLog Logger = Service.Log;
    private CordiLogService Log => _plugin.LogService;
    private const string LogSource = "AdFilter";
    private readonly CordiPlugin _plugin;
    private readonly DiscordWebhookService _webhooks;

    private readonly ConcurrentDictionary<string, List<(string Content, DateTime Timestamp, ulong MessageId)>> _messageBuffer = new();
    private readonly ConcurrentDictionary<string, DateTime> _penaltyBox = new();
    private readonly ConcurrentDictionary<string, List<(string Content, DateTime Timestamp)>> _previewBuffer = new();

    private const int PreviewWindowSeconds = 5;
    private const int PreviewBufferSenderCap = 256;

    public AdvertisementFilterService(CordiPlugin plugin, DiscordWebhookService webhooks)
    {
        _plugin = plugin;
        _webhooks = webhooks;
    }

    public bool IsAdvertisementOrPenalized(Player sender, string sanitizedContent, bool channelFilterEnabled, ulong channelId)
    {
        if (!channelFilterEnabled) return false;

        string senderKey = sender.FullName;

        if (_penaltyBox.TryGetValue(senderKey, out var releaseTime))
        {
            if (DateTime.UtcNow < releaseTime)
            {
                Logger.Info($"[AdvertisementFilter] Blocked message from penalized user {senderKey} until {releaseTime}");
                Log.Warning(LogSource, $"Blocked penalized user {senderKey} (until {releaseTime:HH:mm:ss})");
                return true;
            }
            else
            {
                _penaltyBox.TryRemove(senderKey, out _);
            }
        }

        var cleanupTime = DateTime.UtcNow.AddSeconds(-5);
        var userMessages = _messageBuffer.GetOrAdd(senderKey, _ => new List<(string Content, DateTime Timestamp, ulong MessageId)>());

        bool isAd = false;
        string combinedMessage = string.Empty;

        lock (userMessages)
        {
            userMessages.RemoveAll(x => x.Timestamp < cleanupTime);

            var recentContent = userMessages.Select(x => x.Content).ToList();
            recentContent.Add(sanitizedContent);
            combinedMessage = string.Join(" ", recentContent);
        }

        bool checkAd(string content) => AdvertisementFilter.IsAdvertisement(content, _plugin.Config.AdvertisementFilter);

        isAd = checkAd(sanitizedContent);

        if (!isAd && combinedMessage != sanitizedContent)
        {
            isAd = checkAd(combinedMessage);
        }

        if (isAd)
        {
            Logger.Info($"[AdvertisementFilter] Blocked advertisement: {sanitizedContent.Substring(0, Math.Min(100, sanitizedContent.Length))}...");
            Log.Warning(LogSource, $"Blocked ad from {senderKey}: {sanitizedContent.Substring(0, Math.Min(80, sanitizedContent.Length))}");

            _penaltyBox.AddOrUpdate(senderKey, DateTime.UtcNow.AddSeconds(10), (_, _) => DateTime.UtcNow.AddSeconds(10));

            lock (userMessages)
            {
                foreach (var (_, _, msgId) in userMessages)
                {
                    if (msgId != 0)
                    {
                        Task.Run(() => _webhooks.DeleteMessageAsync(channelId, msgId));
                        Logger.Info($"[AdvertisementFilter] Retroactively deleted message ID: {msgId}");
                        Log.Info(LogSource, $"Retroactively deleted message {msgId} from {senderKey}");
                    }
                }
                userMessages.Clear();
            }
            return true;
        }

        return false;
    }

    public bool IsAdvertisementPreview(Player sender, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;

        var senderKey = sender.FullName;
        var cleanupTime = DateTime.UtcNow.AddSeconds(-PreviewWindowSeconds);
        var recent = _previewBuffer.GetOrAdd(senderKey, _ => new List<(string Content, DateTime Timestamp)>());

        string combined;
        lock (recent)
        {
            recent.RemoveAll(x => x.Timestamp < cleanupTime);
            var parts = recent.Select(x => x.Content).ToList();
            parts.Add(content);
            combined = string.Join(" ", parts);
            recent.Add((content, DateTime.UtcNow));
        }

        SweepPreviewBuffer(cleanupTime);

        if (Score(content)) return true;

        return combined != content && Score(combined);
    }

    private void SweepPreviewBuffer(DateTime cleanupTime)
    {
        if (_previewBuffer.Count <= PreviewBufferSenderCap) return;

        foreach (var pair in _previewBuffer)
        {
            bool stale;
            lock (pair.Value)
            {
                pair.Value.RemoveAll(x => x.Timestamp < cleanupTime);
                stale = pair.Value.Count == 0;
            }

            if (stale) _previewBuffer.TryRemove(pair.Key, out _);
        }
    }

    private bool Score(string content) =>
        AdvertisementFilter.IsAdvertisement(content, _plugin.Config.AdvertisementFilter);

    public FilterEvaluation Evaluate(string content) =>
        AdvertisementFilter.Evaluate(content, _plugin.Config.AdvertisementFilter);

    public void AddMessageToBuffer(Player sender, string sanitizedContent, ulong sentMessageId, bool channelFilterEnabled)
    {
        if (!channelFilterEnabled) return;

        string senderKey = sender.FullName;
        var userMessages = _messageBuffer.GetOrAdd(senderKey, _ => new List<(string Content, DateTime Timestamp, ulong MessageId)>());

        lock (userMessages)
        {
            userMessages.Add((sanitizedContent, DateTime.UtcNow, sentMessageId));
        }
    }
}
