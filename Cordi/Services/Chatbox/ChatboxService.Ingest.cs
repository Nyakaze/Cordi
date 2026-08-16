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

    public static bool IsCommunicationChatType(XivChatType type) =>
        type is XivChatType.Say
            or XivChatType.Shout
            or XivChatType.Yell
            or XivChatType.TellIncoming
            or XivChatType.TellOutgoing
            or XivChatType.Party
            or XivChatType.CrossParty
            or XivChatType.Alliance
            or XivChatType.FreeCompany
            or XivChatType.NoviceNetwork
            or XivChatType.PvPTeam
            or XivChatType.CustomEmote
            or XivChatType.StandardEmote
            or XivChatType.Ls1 or XivChatType.Ls2 or XivChatType.Ls3 or XivChatType.Ls4
            or XivChatType.Ls5 or XivChatType.Ls6 or XivChatType.Ls7 or XivChatType.Ls8
            or XivChatType.CrossLinkShell1 or XivChatType.CrossLinkShell2 or XivChatType.CrossLinkShell3 or XivChatType.CrossLinkShell4
            or XivChatType.CrossLinkShell5 or XivChatType.CrossLinkShell6 or XivChatType.CrossLinkShell7 or XivChatType.CrossLinkShell8
            or XivChatType.Echo;

    public void IngestGameMessage(ChatMessage message)
    {
        if (_disposed || !Config.Enabled) return;

        var targets = Channels
            .Where(c => c.Config.GameChatTypes.Contains(message.ChatType))
            .ToList();

        // If not mapped to any custom tab and not a player communication channel (e.g. battle logs, casting, actions, effect gains) -> skip spam
        if (targets.Count == 0 && !IsCommunicationChatType(message.ChatType))
            return;

        var (name, world) = ResolveGameSender(message);
        var localName = _plugin.cachedLocalPlayer?.Name.TextValue ?? string.Empty;
        var isSelf = message.ChatType == XivChatType.TellOutgoing
                     || (!string.IsNullOrEmpty(localName) && string.Equals(name, localName, StringComparison.Ordinal));
        var raw = message.Message?.TextValue ?? string.Empty;

        var filtered = !isSelf
                       && targets.Any(FilterAdvertisementsFor)
                       && _plugin.AdvertisementFilterService.IsAdvertisementPreview(name, world, raw);

        ChatboxMessage? combinedEntry = null;

        if (targets.Count > 0)
        {
            foreach (var target in targets)
            {
                var resolver = BuildResolver(target);
                var segments = ParseGameContent(message, resolver, out var onlyEmotes, out var parsedMentionsMe);
                var blocked = filtered && FilterAdvertisementsFor(target);
                var mentionsMe = !blocked && !isSelf
                                 && (parsedMentionsMe || target.Config.TreatAllAsMention
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
                    Segments = segments,
                    MentionsMe = mentionsMe,
                    IsSelf = isSelf,
                    AuthorColor = target.Config.Color,
                    OnlyEmotes = onlyEmotes,
                };

                Publish(target, entry, notify: !blocked, addToCombined: false);
                combinedEntry ??= entry;
                RequestGameAvatar(entry);
            }
        }
        else
        {
            // Message does not belong to any custom tab, but IS a valid communication chat type for Combined Channel
            var resolver = BuildResolver(_combined);
            var segments = ParseGameContent(message, resolver, out var onlyEmotes, out var parsedMentionsMe);
            var mentionsMe = !isSelf && (parsedMentionsMe
                             || (Config.MentionOnTell && message.ChatType == XivChatType.TellIncoming));

            var entry = new ChatboxMessage
            {
                FilteredAsAd = false,
                ChannelId = CombinedChannelId,
                Origin = ChatboxOrigin.Game,
                AuthorKey = $"{name}@{world}",
                AuthorName = name,
                AuthorWorld = world,
                GameChatType = message.ChatType,
                RawContent = raw,
                Segments = segments,
                MentionsMe = mentionsMe,
                IsSelf = isSelf,
                OnlyEmotes = onlyEmotes,
            };

            entry.Seq = Interlocked.Increment(ref _sequence);
            entry.SegmentsReady = true;
            Persist(entry, _combined);
            combinedEntry = entry;
            RequestGameAvatar(entry);
        }

        if (Config.ShowCombinedChannel && combinedEntry != null)
        {
            _combined.Append(combinedEntry, Config.MaxMessagesPerChannel, markUnread: false);
            MessageAdded?.Invoke(combinedEntry);
        }
    }

    private IReadOnlyList<ContentSegment> ParseGameContent(ChatMessage message, MentionResolver resolver, out bool onlyEmotes, out bool mentionsMe)
    {
        onlyEmotes = false;
        mentionsMe = false;

        if (message.Message == null || message.Message.Payloads.Count == 0)
        {
            var p = _parser.Parse(message.Message?.TextValue ?? string.Empty, resolver);
            onlyEmotes = p.OnlyEmotes;
            mentionsMe = p.MentionsMe;
            return p.Segments;
        }

        var segments = new List<ContentSegment>();
        var itemSheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        var statusSheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>();

        var payloads = message.Message.Payloads;
        for (var i = 0; i < payloads.Count; i++)
        {
            var p = payloads[i];

            if (p is ItemPayload itemPayload)
            {
                var itemId = itemPayload.ItemId;
                var isHq = itemPayload.IsHQ;
                var item = itemSheet?.GetRowOrDefault(itemId);
                var itemName = item?.Name.ExtractText();
                if (string.IsNullOrEmpty(itemName)) itemName = "Item";

                segments.Add(new ContentSegment
                {
                    Kind = SegmentKind.ItemLink,
                    Text = $"[{itemName}{(isHq ? " " : "")}]",
                    ItemId = itemId,
                    IsHq = isHq,
                    IconId = item?.Icon ?? 0,
                    TooltipText = item?.Description.ExtractText(),
                });

                while (i + 1 < payloads.Count)
                {
                    var next = payloads[i + 1];
                    if (next is ItemPayload || next is RawPayload { Data: { Length: 1 } and [0xCF] })
                    {
                        i++;
                        break;
                    }
                    if (next is TextPayload tp && (tp.Text.Contains(itemName) || tp.Text.StartsWith('')))
                    {
                        i++;
                        continue;
                    }
                    if (next is UIForegroundPayload or UIGlowPayload)
                    {
                        i++;
                        continue;
                    }
                    break;
                }
                continue;
            }

            if (p is StatusPayload statusPayload)
            {
                var status = statusPayload.Status.Value;
                var statusId = statusPayload.Status.RowId;
                var statusName = status.Name.ExtractText();
                if (string.IsNullOrEmpty(statusName)) statusName = "Status";

                segments.Add(new ContentSegment
                {
                    Kind = SegmentKind.StatusLink,
                    Text = $"[{statusName}]",
                    StatusId = statusId,
                    IconId = status.Icon,
                    TooltipText = status.Description.ExtractText(),
                });

                while (i + 1 < payloads.Count)
                {
                    var next = payloads[i + 1];
                    if (next is StatusPayload)
                    {
                        i++;
                        break;
                    }
                    if (next is TextPayload tp && tp.Text.Contains(statusName))
                    {
                        i++;
                        continue;
                    }
                    if (next is UIForegroundPayload or UIGlowPayload)
                    {
                        i++;
                        continue;
                    }
                    break;
                }
                continue;
            }

            if (p is MapLinkPayload mapPayload)
            {
                segments.Add(new ContentSegment
                {
                    Kind = SegmentKind.MapLink,
                    Text = $"{mapPayload.PlaceName} ({mapPayload.XCoord:F1}, {mapPayload.YCoord:F1})",
                    MapLink = mapPayload,
                });
                continue;
            }

            if (p is AutoTranslatePayload atPayload)
            {
                segments.Add(new ContentSegment
                {
                    Kind = SegmentKind.AutoTranslate,
                    Text = $" {atPayload.Text} ",
                });
                continue;
            }

            if (p is TextPayload textPayload)
            {
                if (string.IsNullOrEmpty(textPayload.Text)) continue;
                var parsed = _parser.Parse(textPayload.Text, resolver);
                if (parsed.MentionsMe) mentionsMe = true;
                segments.AddRange(parsed.Segments);
                continue;
            }
        }

        if (segments.Count == 0)
        {
            var p = _parser.Parse(message.Message?.TextValue ?? string.Empty, resolver);
            onlyEmotes = p.OnlyEmotes;
            mentionsMe = p.MentionsMe;
            return p.Segments;
        }

        onlyEmotes = segments.All(s => s.Kind == SegmentKind.Emote);
        return segments;
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

        // 2. If this Discord channel routes incoming Discord messages into the game chat via DiscordMessageRouter, skip ingesting it here because it will be ingested as an in-game message
        var isRoutedToGame = _plugin.Config.Chat.Mappings.Any(m => m.DiscordChannelId == channelKey)
                             || _plugin.Config.Chat.ExtraChatMappings.Any(m => m.Value.DiscordChannelId == channelKey)
                             || _plugin.Config.Chat.TellThreadMappings.ContainsValue(channelKey);

        if (isRoutedToGame)
            return;

        // 3. If this message was sent by the user from Discord on a bridged channel, DiscordMessageRouter routes it into game chat, so it will be ingested as an in-game message -> SKIP so only in-game message is shown
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

    public void DeleteDiscordMessage(ulong discordMessageId)
    {
        if (discordMessageId == 0) return;

        foreach (var channel in Channels)
            channel.RemoveByDiscordId(discordMessageId);

        _combined.RemoveByDiscordId(discordMessageId);
        Store.DeleteByDiscordMessageId(discordMessageId);
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

    private void Publish(ChatboxChannelState target, ChatboxMessage entry, bool notify = true, bool addToCombined = true)
    {
        entry.Seq = Interlocked.Increment(ref _sequence);
        entry.SegmentsReady = true;

        var isActive = WindowFocused
                       && _plugin.ChatboxWindow?.IsOpen == true
                       && (ResolveActiveChannelId() == target.Id || ResolveActiveChannelId() == CombinedChannelId);

        target.Append(entry, LimitFor(target.Id), markUnread: !isActive && !entry.IsSelf && !entry.FilteredAsAd);

        if (addToCombined && Config.ShowCombinedChannel)
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
