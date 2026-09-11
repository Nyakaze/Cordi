using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Domain;
using Cordi.Packets.Handler.Chat;
using Cordi.Services.Discord;
using Cordi.Services.Emojis;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
    private EmojiTranslator Emoji => _plugin.Emoji;

    private bool FilterAdvertisementsFor(ChatboxChannelState channel) =>
        channel.Config.FilterAdvertisements;

    public void IngestGameMessage(ChatMessage message)
    {
        if (_disposed || !Config.Enabled) return;

        BeginSourceCapture();

        var gameMaster = ChatTypes.IsGameMaster(message.ChatType);

        var targets = Channels
            .Where(c => gameMaster || c.Config.GameChatTypes.Contains(message.ChatType))
            .ToList();

        if (targets.Count == 0)
            return;

        var (name, world) = ResolveGameSender(message);
        var (prefix, prefixColor) = ResolveSenderPrefix(message);
        var localName = _plugin.cachedLocalPlayer?.Name.TextValue ?? string.Empty;
        var isSelf = message.ChatType == XivChatType.TellOutgoing
                     || (!string.IsNullOrEmpty(localName) && string.Equals(name, localName, StringComparison.Ordinal));
        var raw = message.Message?.TextValue ?? string.Empty;

        var filtered = !isSelf
                       && targets.Any(FilterAdvertisementsFor)
                       && _plugin.AdvertisementFilterService.IsAdvertisementPreview(Player.FromNameWorld(name, world), raw);

        foreach (var target in targets)
        {
            var resolver = BuildResolver(target);
            var segments = ParseGameContent(message.Message, resolver, out var onlyEmotes, out var parsedMentionsMe);
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
                AuthorPrefix = prefix,
                AuthorPrefixColor = prefixColor,
                GameChatType = message.ChatType,
                RawContent = raw,
                Source = message.Message,
                SourcePayload = EncodeSource(message.Message),
                Segments = segments,
                MentionsMe = mentionsMe,
                IsSelf = isSelf,
                AuthorColor = ColorFor(target.Config, message.ChatType),
                OnlyEmotes = onlyEmotes,
            };

            AwaitSource(entry);
            Publish(target, entry, notify: !blocked);
            RequestGameAvatar(entry);
        }
    }

    private static byte[]? EncodeSource(SeString? content)
    {
        if (content == null || content.Payloads.Count == 0) return null;

        try
        {
            return content.Encode();
        }
        catch (Exception ex)
        {
            Service.Log.Debug($"[Chatbox] Failed to encode SeString: {ex.Message}");
            return null;
        }
    }

    internal IReadOnlyList<ContentSegment> ParseGameContent(SeString? content, MentionResolver resolver, out bool onlyEmotes, out bool mentionsMe)
    {
        onlyEmotes = false;
        mentionsMe = false;

        if (content == null || content.Payloads.Count == 0)
        {
            var p = _parser.Parse(content?.TextValue ?? string.Empty, resolver);
            onlyEmotes = p.OnlyEmotes;
            mentionsMe = p.MentionsMe;
            return p.Segments;
        }

        var segments = new List<ContentSegment>();
        var foreground = new Stack<uint>();
        var glow = new Stack<uint>();

        var linkKind = GameLinkKind.None;
        Payload? link = null;
        uint linkId = 0;
        Vector4? linkColor = null;
        var linkText = new StringBuilder();
        string? absorbedWorld = null;

        void FlushLink()
        {
            var kind = linkKind;
            var payload = link;
            var id = linkId;
            var color = linkColor;
            var captured = linkText.ToString();

            linkKind = GameLinkKind.None;
            link = null;
            linkId = 0;
            linkColor = null;
            linkText.Clear();

            if (kind == GameLinkKind.None) return;

            var text = LinkDisplayText(kind, payload, id, captured);
            if (text.Length == 0) return;

            if (kind == GameLinkKind.Player)
            {
                var world = PayloadWorldName(payload as PlayerPayload);
                if (world.Length > 0)
                {
                    absorbedWorld = world;
                    text = $"{text}@{world}";
                }
            }

            segments.Add(new ContentSegment
            {
                Kind = SegmentKind.GameLink,
                Text = text,
                LinkKind = kind,
                LinkId = id,
                Link = payload,
                Color = color,
            });
        }

        void BeginLink(GameLinkKind kind, Payload? payload, uint id)
        {
            FlushLink();
            absorbedWorld = null;
            linkKind = kind;
            link = payload;
            linkId = id;
        }

        void AddIcon(uint icon, string fallback)
        {
            segments.Add(new ContentSegment
            {
                Kind = SegmentKind.GameIcon,
                IconId = icon,
                Text = fallback,
            });
        }

        foreach (var payload in content.Payloads)
        {
            switch (payload)
            {
                case UIForegroundPayload fgPayload:
                    if (fgPayload.IsEnabled) foreground.Push(fgPayload.UIColor.Value.Dark);
                    else if (foreground.Count > 0) foreground.Pop();
                    continue;

                case UIGlowPayload glowPayload:
                    if (glowPayload.IsEnabled) glow.Push(glowPayload.UIColor.Value.Light);
                    else if (glow.Count > 0) glow.Pop();
                    continue;

                case ItemPayload itemPayload:
                    BeginLink(GameLinkKind.Item, itemPayload, itemPayload.RawItemId);
                    continue;

                case StatusPayload statusPayload:
                    BeginLink(GameLinkKind.Status, statusPayload, statusPayload.Status.RowId);
                    continue;

                case MapLinkPayload mapPayload:
                    BeginLink(GameLinkKind.Map, mapPayload, 0);
                    continue;

                case QuestPayload questPayload:
                    BeginLink(GameLinkKind.Quest, questPayload, questPayload.Quest.RowId);
                    continue;

                case PlayerPayload playerPayload:
                    BeginLink(GameLinkKind.Player, playerPayload, playerPayload.World.RowId);
                    continue;

                case DalamudLinkPayload pluginPayload:
                    BeginLink(GameLinkKind.Plugin, pluginPayload, pluginPayload.CommandId);
                    continue;

                case PartyFinderPayload pfPayload:
                    BeginLink(
                        pfPayload.LinkType == PartyFinderPayload.PartyFinderLinkType.PartyFinderNotification
                            ? GameLinkKind.PartyFinderNotification
                            : GameLinkKind.PartyFinder,
                        pfPayload,
                        pfPayload.ListingId);
                    continue;

                case AutoTranslatePayload atPayload:
                    FlushLink();
                    absorbedWorld = null;
                    AddIcon((uint)BitmapFontIcon.AutoTranslateBegin, ((char)SeIconChar.AutoTranslateOpen).ToString());
                    segments.Add(new ContentSegment
                    {
                        Kind = SegmentKind.AutoTranslate,
                        Text = TrimAutoTranslate(atPayload.Text),
                    });
                    AddIcon((uint)BitmapFontIcon.AutoTranslateEnd, ((char)SeIconChar.AutoTranslateClose).ToString());
                    continue;

                case IconPayload iconPayload:
                    if (absorbedWorld != null && iconPayload.Icon == BitmapFontIcon.CrossWorld) continue;
                    FlushLink();
                    absorbedWorld = null;
                    AddIcon((uint)iconPayload.Icon, string.Empty);
                    continue;

                case RawPayload rawPayload:
                    if (IsLinkTerminator(rawPayload)) FlushLink();
                    else if (TryReadRawLink(rawPayload, out var rawKind, out var rawId)) BeginLink(rawKind, rawPayload, rawId);
                    else ApplyRawColor(rawPayload, foreground, glow);
                    continue;

                case TextPayload textPayload:
                {
                    if (string.IsNullOrEmpty(textPayload.Text)) continue;

                    if (linkKind != GameLinkKind.None)
                    {
                        linkColor = PeekColor(foreground) ?? linkColor;
                        linkText.Append(textPayload.Text);
                        continue;
                    }

                    var text = StripAbsorbedWorld(textPayload.Text, ref absorbedWorld);
                    if (text.Length == 0) continue;

                    var parsed = _parser.Parse(text, resolver);
                    if (parsed.MentionsMe) mentionsMe = true;

                    var color = PeekColor(foreground);
                    if (color != null)
                        foreach (var segment in parsed.Segments)
                            segment.Color ??= color;

                    segments.AddRange(parsed.Segments);
                    continue;
                }
            }
        }

        FlushLink();

        if (segments.Count == 0)
        {
            var p = _parser.Parse(content.TextValue, resolver);
            onlyEmotes = p.OnlyEmotes;
            mentionsMe = p.MentionsMe;
            return p.Segments;
        }

        onlyEmotes = segments.All(s => s.Kind == SegmentKind.Emote);
        return segments;
    }

    public void PostSystemMessage(string channelId, string text)
    {
        var target = GetChannel(channelId);
        if (target == null) return;

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

        Emotes.Record(entry);

        var isActive = WindowFocused
                       && _plugin.ChatboxWindow?.IsOpen == true
                       && ResolveActiveChannelId() == target.Id;

        target.Append(entry, LimitFor(target.Id), markUnread: !isActive && !entry.IsSelf && !entry.FilteredAsAd);

        Persist(entry, target);

        Translator.Consider(entry);

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

        _lastMentionSound = DateTime.Now;
        _plugin.Audio.Play(Config.MentionSoundPath, Config.MentionSoundVolume);
    }

    private static string Excerpt(string text, int max)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        text = text.Replace('\n', ' ').Trim();
        return text.Length <= max ? text : text[..max] + "…";
    }

    public ChatboxReplyRef BuildReplyRef(ChatboxMessage message) => new()
    {
        Seq = message.Seq,
        DiscordMessageId = message.DiscordMessageId,
        AuthorName = message.AuthorName,
        AvatarUrl = message.AvatarUrl,
        Excerpt = Excerpt(message.RawContent, Config.ReplyExcerptLength),
    };

    private static string TrimAutoTranslate(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        return text
            .Trim((char)SeIconChar.AutoTranslateOpen, (char)SeIconChar.AutoTranslateClose)
            .Trim();
    }

    private static string StripAbsorbedWorld(string text, ref string? absorbedWorld)
    {
        var world = absorbedWorld;
        if (world == null) return text;

        absorbedWorld = null;

        var trimmed = text.TrimStart();
        return trimmed.StartsWith(world, StringComparison.Ordinal)
            ? trimmed[world.Length..]
            : text;
    }

    private static string PayloadWorldName(PlayerPayload? payload)
    {
        if (payload == null || payload.World.RowId == 0) return string.Empty;

        return payload.World.ValueNullable?.Name.ExtractText() ?? string.Empty;
    }

    private const char BoxedLetterFirst = (char)0xE071;
    private const char BoxedLetterLast = (char)0xE08A;

    private static bool IsRolePlate(string text) =>
        text.Any(ch => ch is >= BoxedLetterFirst and <= BoxedLetterLast);

    private static (string Text, Vector4? Color) ResolveSenderPrefix(ChatMessage message)
    {
        var payloads = message.Sender?.Payloads;

        if (payloads == null || payloads.All(p => p.Type != PayloadType.Player))
            return (string.Empty, null);

        var builder = new StringBuilder();
        Vector4? color = null;

        foreach (var payload in payloads.TakeWhile(p => p.Type != PayloadType.Player))
        {
            switch (payload)
            {
                case UIForegroundPayload foreground when foreground.IsEnabled:
                    color ??= RgbaToColor(foreground.UIColor.ValueNullable?.Dark ?? 0);
                    break;
                case TextPayload text when !string.IsNullOrEmpty(text.Text):
                    builder.Append(text.Text);
                    break;
            }
        }

        var prefix = builder.ToString().Trim();

        return (IsRolePlate(prefix) ? prefix : string.Empty, color);
    }

    private static (string Name, string World) ResolveGameSender(ChatMessage message)
    {
        if (message.Sender?.Payloads.FirstOrDefault(p => p.Type == PayloadType.Player) is PlayerPayload player)
        {
            var world = PayloadWorldName(player);
            return (player.PlayerName, world.Length > 0 ? world : ResolveWorldForName(player.PlayerName));
        }

        var fallback = message.Sender?.TextValue ?? string.Empty;
        var localName = CordiPlugin.Plugin.cachedLocalPlayer?.Name.TextValue;
        if (!string.IsNullOrEmpty(localName) && fallback.EndsWith(localName, StringComparison.Ordinal))
            return (localName, CordiPlugin.Plugin.cachedLocalPlayer!.HomeWorld.Value.Name.ExtractText());

        var name = StripSenderMarkers(fallback);

        if (message.Message?.Payloads.FirstOrDefault(p => p.Type == PayloadType.Player) is PlayerPayload mentioned
            && string.Equals(mentioned.PlayerName, name, StringComparison.Ordinal))
        {
            var world = PayloadWorldName(mentioned);
            if (world.Length > 0) return (name, world);
        }

        return (name, ResolveWorldForName(name));
    }

    private static string ResolveWorldForName(string name)
    {
        if (string.IsNullOrEmpty(name) || string.Equals(name, ChatboxMessage.SystemSender, StringComparison.Ordinal))
            return string.Empty;

        var local = CordiPlugin.Plugin.cachedLocalPlayer;
        if (local != null && string.Equals(local.Name.TextValue, name, StringComparison.Ordinal))
            return local.HomeWorld.ValueNullable?.Name.ExtractText() ?? string.Empty;

        foreach (var obj in Service.ObjectTable)
        {
            if (obj is not IPlayerCharacter character) continue;
            if (!string.Equals(character.Name.TextValue, name, StringComparison.Ordinal)) continue;

            return character.HomeWorld.ValueNullable?.Name.ExtractText() ?? string.Empty;
        }

        return string.Empty;
    }

    private static string StripSenderMarkers(string sender) =>
        string.IsNullOrEmpty(sender) ? ChatboxMessage.SystemSender : sender.Trim();

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
        _plugin.Config.ActivityConfig?.TargetUserId ?? 0UL;

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
            ResolveEmoteNameById = ResolveEmoteName,
        };
    }

    private string? ResolveDiscordUserName(ulong id)
    {
        var member = _plugin.Channels?.FindMember(id);
        if (member == null) return null;

        return string.IsNullOrEmpty(member.Nickname) ? member.User.DisplayName : member.Nickname;
    }

    private string? ResolveDiscordRoleName(ulong id) => _plugin.Channels?.FindRole(id)?.Name;

    private string? ResolveDiscordChannelName(ulong id) => _plugin.Channels?.Find(id)?.Name;

    private string? ResolveGuildEmoteUrl(string name) => Emoji.ResolveUrlByName(name);

    private string? ResolveEmoteName(ulong id) => Emoji.ResolveName(id);

    public void RegisterEmotes(string? content)
    {
        if (_disposed) return;

        Emoji.Register(content);
    }
}
