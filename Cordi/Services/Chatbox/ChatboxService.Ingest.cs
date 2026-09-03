using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Domain;
using Cordi.Packets.Handler.Chat;
using Cordi.Services.Discord;
using Cordi.Services.Emojis;
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

        var gameMaster = IsGameMasterChatType(message.ChatType);

        var targets = Channels
            .Where(c => gameMaster || c.Config.GameChatTypes.Contains(message.ChatType))
            .ToList();

        if (targets.Count == 0)
            return;

        var (name, world) = ResolveGameSender(message);
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

            Publish(target, entry, notify: !blocked);
            RequestGameAvatar(entry);
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
