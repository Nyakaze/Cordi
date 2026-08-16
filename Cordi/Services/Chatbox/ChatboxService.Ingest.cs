using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Packets.Handler.Chat;
using Cordi.Services.Discord;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using DSharpPlus.Entities;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
    private static readonly Regex ShortcodeRegex = new(@":([A-Za-z0-9_+-]{2,32}):", RegexOptions.Compiled);

    private readonly Dictionary<string, (ulong Id, bool Animated)> _guildEmotes = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _guildEmotesRefreshedAt = DateTime.MinValue;
    private readonly object _guildEmoteGate = new();

    private bool FilterAdvertisementsFor(ChatboxChannelState channel) =>
        Config.FilterAdvertisements && channel.Config.FilterAdvertisements;

    public void IngestGameMessage(ChatMessage message)
    {
        if (_disposed || !Config.Enabled) return;

        var targets = Channels
            .Where(c => c.Config.GameChatTypes.Contains(message.ChatType))
            .ToList();

        if (targets.Count == 0) return;

        var (name, world) = ResolveGameSender(message);
        var localName = _plugin.cachedLocalPlayer?.Name.TextValue ?? string.Empty;
        var isSelf = message.ChatType == XivChatType.TellOutgoing
                     || (!string.IsNullOrEmpty(localName) && string.Equals(name, localName, StringComparison.Ordinal));
        var raw = message.Message?.TextValue ?? string.Empty;
        var filtered = !isSelf
                       && targets.Any(FilterAdvertisementsFor)
                       && _plugin.AdvertisementFilterService.IsAdvertisementPreview(name, world, raw);

        foreach (var target in targets)
        {
            var resolver = BuildResolver(target);
            var parsed = _parser.Parse(raw, resolver);
            var blocked = filtered && FilterAdvertisementsFor(target);
            var mentionsMe = !blocked && !isSelf
                             && (parsed.MentionsMe || target.Config.TreatAllAsMention
                                 || (Config.MentionOnTell && message.ChatType == XivChatType.TellIncoming));

            var entry = new ChatboxMessage
            {
                FilteredAsAd = blocked,
                ChannelId = target.Id,
                Origin = ChatboxOrigin.Game,
                AuthorKey = $"{name}@{world}",
                AuthorName = name,
                AuthorWorld = world,
                GameChatType = message.ChatType,
                RawContent = raw,
                Segments = parsed.Segments,
                MentionsMe = mentionsMe,
                IsSelf = isSelf,
                AuthorColor = target.Config.Color,
                OnlyEmotes = parsed.OnlyEmotes,
            };

            Publish(target, entry, notify: !blocked);
            RequestGameAvatar(entry);
        }
    }

    public void IngestDiscordMessage(DiscordMessage message, ulong channelId)
    {
        if (_disposed || !Config.Enabled || message == null) return;

        var channelKey = channelId.ToString();
        var targets = Channels
            .Where(c => c.Config.DiscordChannelId == channelKey)
            .ToList();

        if (targets.Count == 0) return;

        var author = message.Author;
        var selfId = ParseSelfDiscordId();
        var isBot = author != null && _plugin.Discord?.Client?.CurrentUser?.Id == author.Id;
        var isSelf = (selfId != 0 && author?.Id == selfId) || isBot;

        // Check if this Discord channel is bridged to in-game chat (standard, extra chat, or tell)
        var isBridgedToGame = targets.Any(t => t.Config.GameChatTypes.Count > 0 || t.Config.SendToGame)
                              || _plugin.Config.Chat.Mappings.Any(m => m.DiscordChannelId == channelKey)
                              || _plugin.Config.Chat.ExtraChatMappings.Any(m => m.Value.DiscordChannelId == channelKey)
                              || _plugin.Config.Chat.TellThreadMappings.ContainsValue(channelKey);

        // 1. If this message is a Webhook message on a bridged channel, it's an echo of a game message already ingested by IngestGameMessage -> SKIP
        if (message.WebhookMessage && isBridgedToGame)
            return;

        // 2. If this message was sent by the user from Discord on a bridged channel, DiscordMessageRouter routes it into game chat, so it will be ingested as an in-game message -> SKIP so only in-game message is shown
        if (isSelf && isBridgedToGame)
            return;

        var displayName = (message.Channel?.Guild != null && author is DiscordMember member && !string.IsNullOrEmpty(member.Nickname))
            ? member.Nickname
            : author?.Username ?? "unknown";

        var avatarUrl = author?.AvatarUrl ?? author?.DefaultAvatarUrl;
        var raw = message.Content ?? string.Empty;
        var attachments = message.Attachments?.Select(a => a.Url).Where(u => !string.IsNullOrEmpty(u)).ToList()
                          ?? new List<string>();

        var explicitlyMentioned = message.MentionEveryone
                                  || (selfId != 0 && message.MentionedUsers?.Any(u => u?.Id == selfId) == true);

        foreach (var target in targets)
        {
            // If the target channel listens to game chat, DiscordMessageRouter forwards the Discord message to the game, and IngestGameMessage will ingest the game echo -> SKIP to avoid duplicate
            if (isBridgedToGame && target.Config.GameChatTypes.Count > 0)
                continue;

            var resolver = BuildResolver(target);
            var parsed = _parser.Parse(raw, resolver);

            var entry = new ChatboxMessage
            {
                ChannelId = target.Id,
                Origin = ChatboxOrigin.Discord,
                AuthorKey = author?.Id.ToString() ?? displayName,
                AuthorName = displayName,
                AvatarUrl = avatarUrl,
                DiscordMessageId = message.Id,
                DiscordChannelId = channelId,
                RawContent = raw,
                Segments = parsed.Segments,
                MentionsMe = explicitlyMentioned || parsed.MentionsMe || target.Config.TreatAllAsMention,
                IsSelf = isSelf,
                AuthorColor = target.Config.Color,
                Attachments = attachments,
                OnlyEmotes = parsed.OnlyEmotes,
                Reply = BuildReplyRef(message.ReferencedMessage),
            };

            Publish(target, entry);
        }
    }

    public void PostSystemMessage(string channelId, string text)
    {
        var target = GetChannel(channelId);
        if (target == null || target.Id == CombinedChannelId) return;

        var entry = new ChatboxMessage
        {
            ChannelId = target.Id,
            Origin = ChatboxOrigin.System,
            AuthorName = "Cordi",
            RawContent = text,
            Segments = new[] { ContentSegment.PlainText(text) },
        };

        Publish(target, entry, notify: false);
    }

    private void Publish(ChatboxChannelState target, ChatboxMessage entry, bool notify = true)
    {
        entry.Seq = Interlocked.Increment(ref _sequence);
        entry.SegmentsReady = true;

        var isActive = WindowFocused
                       && _plugin.ChatboxWindow?.IsOpen == true
                       && (ResolveActiveChannelId() == target.Id || ResolveActiveChannelId() == CombinedChannelId);

        target.Append(entry, LimitFor(target.Id), markUnread: !isActive && !entry.IsSelf && !entry.FilteredAsAd);

        if (Config.ShowCombinedChannel)
            _combined.Append(entry, Config.MaxMessagesPerChannel, markUnread: false);

        Persist(entry, target);

        MessageAdded?.Invoke(entry);

        if (notify && !entry.IsSelf && !target.Config.MuteNotifications)
            Notify(target, entry, isActive);
    }

    private void Notify(ChatboxChannelState target, ChatboxMessage entry, bool isActive)
    {
        if (isActive) return;

        if (entry.MentionsMe && Config.NotifyOnMention)
        {
            _plugin.NotificationManager.Add(
                $"{target.Config.Name} — mention",
                $"{entry.AuthorName}: {Excerpt(entry.RawContent, 80)}",
                CordiNotificationType.Info);
        }
        else if (Config.NotifyOnUnread)
        {
            _plugin.NotificationManager.Add(
                target.Config.Name,
                $"{entry.AuthorName}: {Excerpt(entry.RawContent, 80)}",
                CordiNotificationType.Info);
        }

        if (entry.MentionsMe && Config.PlaySoundOnMention)
            PlayMentionSound();
    }

    private DateTime _lastMentionSound = DateTime.MinValue;

    private void PlayMentionSound()
    {
        if (DateTime.Now - _lastMentionSound < TimeSpan.FromSeconds(2)) return;

        var path = Config.MentionSoundPath;
        if (string.IsNullOrWhiteSpace(path))
            path = System.IO.Path.Join(Service.PluginInterface.AssemblyLocation.Directory!.FullName, "target.wav");

        if (!System.IO.File.Exists(path)) return;

        _lastMentionSound = DateTime.Now;
        var volume = Config.MentionSoundVolume;

        Task.Run(() =>
        {
            try
            {
                using var audio = new NAudio.Wave.AudioFileReader(path) { Volume = volume };
                using var device = new NAudio.Wave.WaveOutEvent();
                device.Init(audio);
                device.Play();
                while (device.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                    Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "[Chatbox] Mention sound playback failed");
            }
        });
    }

    private static string Excerpt(string text, int max)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        text = text.Replace('\n', ' ').Trim();
        return text.Length <= max ? text : text[..max] + "…";
    }

    private ChatboxReplyRef? BuildReplyRef(DiscordMessage? referenced)
    {
        if (referenced == null) return null;

        return new ChatboxReplyRef
        {
            DiscordMessageId = referenced.Id,
            AuthorName = referenced.Author?.Username ?? "unknown",
            AvatarUrl = referenced.Author?.AvatarUrl,
            Excerpt = Excerpt(referenced.Content ?? string.Empty, Config.ReplyExcerptLength),
        };
    }

    public ChatboxReplyRef BuildReplyRef(ChatboxMessage message) => new()
    {
        Seq = message.Seq,
        DiscordMessageId = message.DiscordMessageId,
        AuthorName = message.AuthorName,
        AvatarUrl = message.AvatarUrl,
        Excerpt = Excerpt(message.RawContent, Config.ReplyExcerptLength),
    };

    private static (string Name, string World) ResolveGameSender(ChatMessage message)
    {
        if (message.Sender?.Payloads.FirstOrDefault(p => p.Type == PayloadType.Player) is PlayerPayload player)
            return (player.PlayerName, player.World.Value.Name.ExtractText());

        var fallback = message.Sender?.TextValue ?? string.Empty;
        var localName = CordiPlugin.Plugin.cachedLocalPlayer?.Name.TextValue;
        if (!string.IsNullOrEmpty(localName) && fallback.EndsWith(localName, StringComparison.Ordinal))
            return (localName, CordiPlugin.Plugin.cachedLocalPlayer!.HomeWorld.Value.Name.ExtractText());

        return (StripSenderMarkers(fallback), string.Empty);
    }

    private static string StripSenderMarkers(string sender) =>
        string.IsNullOrEmpty(sender) ? "System" : sender.Trim();

    private void RequestGameAvatar(ChatboxMessage entry)
    {
        if (!Config.ShowAvatars || !Config.ShowLodestoneAvatars) return;
        if (string.IsNullOrEmpty(entry.AuthorName) || string.IsNullOrEmpty(entry.AuthorWorld)) return;

        Task.Run(async () =>
        {
            try
            {
                var url = await _plugin.Lodestone.GetAvatarUrlAsync(entry.AuthorName, entry.AuthorWorld);
                if (!string.IsNullOrEmpty(url)) entry.AvatarUrl = url;
            }
            catch (Exception ex)
            {
                Service.Log.Debug($"[Chatbox] Avatar lookup failed for {entry.AuthorKey}: {ex.Message}");
            }
        });
    }

    private ulong ParseSelfDiscordId() =>
        ulong.TryParse(Config.DiscordUserId, out var id) ? id : 0UL;

    private MentionResolver BuildResolver(ChatboxChannelState channel)
    {
        var local = _plugin.cachedLocalPlayer;
        var fullName = local?.Name.TextValue ?? string.Empty;

        return new MentionResolver
        {
            LocalFullName = fullName,
            LocalNameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            SelfDiscordId = ParseSelfDiscordId(),
            Keywords = Config.MentionKeywords,
            KnownNames = channel.RecentAuthors(),
            MatchOwnName = Config.MentionOwnName,
            MatchOwnNameParts = Config.MentionOwnNameParts,
            ResolveUser = ResolveDiscordUserName,
            ResolveRole = ResolveDiscordRoleName,
            ResolveChannel = ResolveDiscordChannelName,
            ResolveEmoteByName = ResolveGuildEmoteUrl,
        };
    }

    private string? ResolveDiscordUserName(ulong id)
    {
        var client = _plugin.Discord?.Client;
        if (client == null) return null;

        foreach (var guild in client.Guilds.Values)
        {
            if (guild.Members.TryGetValue(id, out var member))
                return string.IsNullOrEmpty(member.Nickname) ? member.Username : member.Nickname;
        }

        return null;
    }

    private string? ResolveDiscordRoleName(ulong id)
    {
        var client = _plugin.Discord?.Client;
        if (client == null) return null;

        foreach (var guild in client.Guilds.Values)
        {
            if (guild.Roles.TryGetValue(id, out var role))
                return role.Name;
        }

        return null;
    }

    private string? ResolveDiscordChannelName(ulong id)
    {
        var client = _plugin.Discord?.Client;
        if (client == null) return null;

        foreach (var guild in client.Guilds.Values)
        {
            if (guild.Channels.TryGetValue(id, out var channel))
                return channel.Name;
        }

        return null;
    }

    private void RefreshGuildEmotes()
    {
        if (DateTime.UtcNow - _guildEmotesRefreshedAt < TimeSpan.FromSeconds(30)) return;

        var client = _plugin.Discord?.Client;
        if (client == null) return;

        lock (_guildEmoteGate)
        {
            _guildEmotesRefreshedAt = DateTime.UtcNow;
            _guildEmotes.Clear();

            foreach (var guild in client.Guilds.Values)
            {
                foreach (var emoji in guild.Emojis.Values)
                {
                    if (string.IsNullOrEmpty(emoji.Name)) continue;
                    _guildEmotes[emoji.Name] = (emoji.Id, emoji.IsAnimated);
                }
            }
        }
    }

    private string? ResolveGuildEmoteUrl(string name)
    {
        RefreshGuildEmotes();
        lock (_guildEmoteGate)
        {
            return _guildEmotes.TryGetValue(name, out var emote)
                ? ChatboxContentParser.CustomEmoteUrl(emote.Id, emote.Animated)
                : null;
        }
    }

    public string ConvertShortcodes(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        RefreshGuildEmotes();

        return ShortcodeRegex.Replace(text, match =>
        {
            var name = match.Groups[1].Value;

            // 1. Guild Emotes (Custom animated/static Discord emojis)
            lock (_guildEmoteGate)
            {
                if (_guildEmotes.TryGetValue(name, out var emote))
                {
                    return emote.Animated
                        ? $"<a:{name}:{emote.Id}>"
                        : $"<:{name}:{emote.Id}>";
                }
            }

            // 2. Unicode shortcodes (:sob: -> 😭, :heart: -> ❤️, etc.)
            if (EmojiIndex.TryGetShortcode(name, out var unicode))
            {
                return unicode;
            }

            return match.Value;
        });
    }

    private string ConvertShortcodesForDiscord(string text) => ConvertShortcodes(text);
}
