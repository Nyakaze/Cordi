using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Domain;
using Cordi.Services.Discord.Webhooks;
using Crovus.Client;
using Crovus.Models;
using Crovus.Rest;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace Cordi.Services.Discord.Send;

public sealed class DiscordSender
{
    private const string LogSource = "Send";

    private readonly Configuration.Configuration _config;
    private readonly CordiLogService Log;
    private readonly Connection.DiscordConnection _connection;
    private readonly Projections.DiscordChannelProjection _channels;
    private readonly Queue.DiscordSendQueue _queue;
    private readonly Emojis.EmojiTranslator _emoji;
    private readonly Features.LodestoneService _lodestone;
    private readonly NotificationManager _notifications;
    private readonly DiscordWebhookService _webhooks;
    private readonly AdvertisementFilterService _adFilter;

    public DiscordSender(
        Configuration.Configuration config,
        CordiLogService log,
        Connection.DiscordConnection connection,
        Projections.DiscordChannelProjection channels,
        Queue.DiscordSendQueue queue,
        Emojis.EmojiTranslator emoji,
        Features.LodestoneService lodestone,
        NotificationManager notifications,
        DiscordWebhookService webhooks,
        AdvertisementFilterService adFilter)
    {
        _config = config;
        Log = log;
        _connection = connection;
        _channels = channels;
        _queue = queue;
        _emoji = emoji;
        _lodestone = lodestone;
        _notifications = notifications;
        _webhooks = webhooks;
        _adFilter = adFilter;
    }

    public Task SendMessage(ulong? channelId, SeString message, Player sender,
        XivChatType chatType = XivChatType.None, string? correspondentName = null) =>
        SendMessage(channelId, message.TextValue, sender, chatType, correspondentName);

    private static readonly TimeSpan EchoWindow = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, DateTime> _postedLinks = new(StringComparer.Ordinal);

    private static string LinkKey(string url) => url.Split('?')[0];

    private bool ConsumePostedLinks(string content)
    {
        var now = DateTime.UtcNow;
        var matched = false;

        foreach (var entry in _postedLinks)
        {
            if (now - entry.Value > EchoWindow)
            {
                _postedLinks.TryRemove(entry.Key, out _);
                continue;
            }

            if (!content.Contains(entry.Key, StringComparison.Ordinal)) continue;

            _postedLinks.TryRemove(entry.Key, out _);
            matched = true;
        }

        return matched;
    }

    public bool CanSendFiles(ulong? channelId, XivChatType chatType) =>
        IsConnected && (channelId ?? StrictMappedChannel(chatType)) is not null;

    private ulong? StrictMappedChannel(XivChatType chatType) =>
        _config.MappingCache.TryGetValue(chatType, out var mapped) && ulong.TryParse(mapped, out var id) ? id : null;

    public async Task<IReadOnlyList<string>> SendFiles(ulong? channelId, string content, Player sender,
        IReadOnlyList<DiscordFile> files, XivChatType chatType = XivChatType.None, string? correspondentName = null)
    {
        if (_connection.Context is not { } context || files.Count == 0) return Array.Empty<string>();
        if ((channelId ?? StrictMappedChannel(chatType)) is not { } targetId) return Array.Empty<string>();

        try
        {
            var channel = await FindChannelAsync(context, targetId);

            if (channel?.Type is ChannelType.GuildForum)
            {
                if (string.IsNullOrEmpty(correspondentName)) return Array.Empty<string>();
                targetId = await ResolveTellThreadAsync(context, targetId, correspondentName);
            }

            var sanitized = DiscordTextSanitizer.Sanitize(_emoji.ToDiscord(content));
            var avatarUrl = NormalizeAvatar(await _lodestone.GetAvatarUrlAsync(sender));

            var message = await _webhooks.SendAsync(targetId, factory =>
            {
                if (!string.IsNullOrWhiteSpace(sanitized)) factory.WithContent(sanitized);
                factory.AddFiles(files).As(sender.FullName, avatarUrl);
            });

            var links = message?.Attachments.Select(attachment => attachment.Url)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToList() ?? new List<string>();

            foreach (var link in links)
                _postedLinks[LinkKey(link)] = DateTime.UtcNow;

            if (links.Count == 0)
            {
                Log.Warning(LogSource, $"Upload of {files.Count} file(s) to channel {targetId} returned no attachments");
                return links;
            }

            Log.Debug(LogSource, $"Uploaded {links.Count} file(s) as {sender.FullName} to channel {targetId}");

            _config.Stats.IncrementTotal();
            if (chatType != XivChatType.None) _config.Stats.IncrementChatType(chatType);

            return links;
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Failed to upload files to channel {targetId}", ex);
            return Array.Empty<string>();
        }
    }

    public async Task SendMessage(ulong? channelId, string content, Player sender,
        XivChatType chatType = XivChatType.None, string? correspondentName = null, string? avatarUrl = null)
    {
        if (_connection.Context is not { } context) return;

        if (ConsumePostedLinks(content))
        {
            Log.Debug(LogSource, $"Skipped [{chatType}] echo, its files are already on Discord");
            return;
        }

        if ((channelId ?? ResolveMappedChannel(chatType)) is not { } targetId)
        {
            Log.Info(LogSource, $"No Discord channel mapped for [{chatType}]");
            return;
        }

        try
        {
            var channel = await FindChannelAsync(context, targetId);

            if (channel?.Type is ChannelType.GuildForum && !string.IsNullOrEmpty(correspondentName))
                targetId = await ResolveTellThreadAsync(context, targetId, correspondentName);

            var converted = _emoji.ToDiscord(content);
            var sanitized = DiscordTextSanitizer.Sanitize(converted);

            if (string.IsNullOrWhiteSpace(sanitized))
            {
                Log.Warning(LogSource, $"Sanitized content is empty, skipping send. Original: '{content}'");
                return;
            }

            var finalAvatarUrl = NormalizeAvatar(
                avatarUrl ?? await _lodestone.GetAvatarUrlAsync(sender));

            var mapping = _config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == chatType);
            var channelFilterEnabled = mapping?.EnableAdvertisementFilter ?? true;

            if (_adFilter.IsAdvertisementOrPenalized(sender, sanitized, channelFilterEnabled, targetId))
                return;

            var sentMessageId = await _webhooks.ExecuteAsync(targetId, message =>
                message.WithContent(sanitized).As(sender.FullName, finalAvatarUrl));

            Log.Debug(LogSource, $"Sent [{chatType}] {sender.FullName}: {sanitized}");

            _adFilter.AddMessageToBuffer(sender, sanitized, sentMessageId, channelFilterEnabled);

            _config.Stats.IncrementTotal();
            if (chatType != XivChatType.None) _config.Stats.IncrementChatType(chatType);
            if (!string.IsNullOrEmpty(correspondentName)) _config.Stats.IncrementTell(correspondentName);
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Failed to send message to channel {targetId}", ex);
        }
    }

    public Task<ulong> SendWebhookMessage(ulong channelId, string content, Player sender)
    {
        if (!IsConnected) return Task.FromResult(0UL);

        var converted = _emoji.ToDiscord(content);
        var sanitized = DiscordTextSanitizer.Sanitize(converted);

        if (string.IsNullOrWhiteSpace(sanitized)) return Task.FromResult(0UL);

        return QueuedSendAsync($"webhook send (channel {channelId})", "webhook", async () =>
        {
            var avatarUrl = NormalizeAvatar(await _lodestone.GetAvatarUrlAsync(sender));

            return await _webhooks.ExecuteAsync(channelId, message =>
                message.WithContent(sanitized).As(sender.FullName, avatarUrl));
        });
    }

    public Task<ulong> SendWebhookMessage(ulong channelId, DiscordEmbed embed, Player sender)
    {
        if (!IsConnected) return Task.FromResult(0UL);

        return QueuedSendAsync($"webhook embed (channel {channelId})", "webhook", async () =>
        {
            var avatarUrl = NormalizeAvatar(await _lodestone.GetAvatarUrlAsync(sender));

            return await _webhooks.ExecuteAsync(channelId, message =>
                message.AddEmbed(embed).As(sender.FullName, avatarUrl));
        });
    }

    public Task<ulong> SendWebhookMessageRaw(ulong channelId, DiscordEmbed embed, string username, string? avatarUrl)
    {
        if (!IsConnected) return Task.FromResult(0UL);

        var finalAvatarUrl = NormalizeAvatar(avatarUrl);

        return QueuedSendAsync($"webhook embed raw (channel {channelId})", "webhook", () =>
            _webhooks.ExecuteAsync(channelId, message => message.AddEmbed(embed).As(username, finalAvatarUrl)));
    }

    public Task EditWebhookMessage(ulong channelId, ulong messageId, DiscordEmbed embed)
    {
        if (!IsConnected) return Task.CompletedTask;

        return QueuedSendAsync($"webhook edit (channel {channelId} msg {messageId})", "webhook", () =>
            _webhooks.EditMessageAsync(channelId, messageId, message => message.AddEmbed(embed)));
    }

    public Task AddReaction(ulong channelId, ulong messageId, string emoji)
    {
        if (_connection.Context is not { } context) return Task.CompletedTask;

        return QueuedSendAsync($"add reaction {emoji} on {messageId}", "reaction", () =>
            context.Services.Reactions.AddAsync(channelId, messageId, emoji));
    }

    public Task RemoveReaction(ulong channelId, ulong messageId, string emoji)
    {
        if (_connection.Context is not { } context) return Task.CompletedTask;

        return QueuedSendAsync($"remove reaction {emoji} on {messageId}", "reaction", () =>
            context.Services.Reactions.RemoveAsync(channelId, messageId, emoji));
    }

    public Task<ulong> SendEmbedToChannelAsync(ulong channelId, DiscordEmbed embed)
    {
        if (_connection.Context is not { } context) return Task.FromResult(0UL);

        return QueuedSendAsync($"bot embed (channel {channelId})", "bot", async () =>
        {
            var message = await context.Services.Messages.SendAsync(channelId, m => m.AddEmbed(embed));

            return message.Id.Value;
        });
    }

    public async Task<bool> EditEmbedInChannelAsync(ulong channelId, ulong messageId, DiscordEmbed embed)
    {
        if (_connection.Context is not { } context) return false;

        try
        {
            return await _queue.RunAsync(
                $"bot embed edit (channel {channelId} msg {messageId})", "bot", async () =>
                {
                    await context.Services.Messages.EditAsync(channelId, messageId, m => m.AddEmbed(embed));

                    return true;
                });
        }
        catch (DiscordRestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(LogSource, $"Failed to edit bot embed {messageId} in channel {channelId}: {ex.Message}");
            return false;
        }
    }

    public Task DeleteChannelMessageAsync(ulong channelId, ulong messageId)
    {
        if (_connection.Context is not { } context) return Task.CompletedTask;

        return QueuedSendAsync($"bot delete (channel {channelId} msg {messageId})", "bot", async () =>
        {
            try
            {
                await context.Services.Messages.DeleteAsync(channelId, messageId);
            }
            catch (DiscordRestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
            }
        });
    }

    private bool IsConnected => _connection.Context is not null;

    private ulong? ResolveMappedChannel(XivChatType chatType)
    {
        var targetChannelId = chatType != XivChatType.Debug ? _config.Discord.DefaultChannelId : string.Empty;

        if (_config.MappingCache.TryGetValue(chatType, out var mappedId))
            targetChannelId = mappedId;

        return ulong.TryParse(targetChannelId, out var id) ? id : null;
    }

    private async Task<DiscordChannel?> FindChannelAsync(ICrovusContext context, ulong channelId)
    {
        if (_channels.Find(channelId) is { } known) return known;

        try
        {
            return await context.Services.Channels.GetAsync(channelId);
        }
        catch (Exception ex)
        {
            Log.Warning(LogSource, $"Failed to resolve channel {channelId}: {ex.Message}");
            return null;
        }
    }

    private async Task<ulong> ResolveTellThreadAsync(ICrovusContext context, ulong forumId, string correspondentName)
    {
        if (_config.Chat.TellThreadMappings.TryGetValue(correspondentName, out var mapped)
            && ulong.TryParse(mapped, out var mappedThreadId))
        {
            var existing = await FindChannelAsync(context, mappedThreadId);
            if (existing is { IsThread: true }) return existing.Id.Value;
        }

        var post = await context.Services.Threads.CreatePostAsync(forumId, correspondentName,
            $"Started conversation with {correspondentName}");

        _config.Chat.TellThreadMappings[correspondentName] = post.Id.ToString();
        _config.Save();
        _notifications.Add("New Conversation!", $"Created Channel for: {correspondentName}",
            CordiNotificationType.Success);

        Log.Info(LogSource, $"Created forum post '{correspondentName}' ({post.Id}) in forum {forumId}");

        return post.Id.Value;
    }

    private static string? NormalizeAvatar(string? avatarUrl) =>
        !string.IsNullOrEmpty(avatarUrl) && Uri.IsWellFormedUriString(avatarUrl, UriKind.Absolute) ? avatarUrl : null;

    private async Task<ulong> QueuedSendAsync(string description, string category, Func<Task<ulong>> action)
    {
        try
        {
            return await _queue.RunAsync(description, category, action);
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"{description} ultimately failed", ex);
            return 0UL;
        }
    }

    private async Task QueuedSendAsync(string description, string category, Func<Task> action)
    {
        try
        {
            await _queue.RunAsync(description, category, action);
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"{description} ultimately failed", ex);
        }
    }
}
