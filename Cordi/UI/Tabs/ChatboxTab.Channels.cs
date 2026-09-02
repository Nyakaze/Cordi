using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Services.Chatbox;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab
{
    private static readonly XivChatType[] SelectableChatTypes =
    {
        XivChatType.Say,
        XivChatType.Shout,
        XivChatType.Yell,
        XivChatType.TellIncoming,
        XivChatType.Party,
        XivChatType.CrossParty,
        XivChatType.Alliance,
        XivChatType.FreeCompany,
        XivChatType.NoviceNetwork,
        XivChatType.PvPTeam,
        XivChatType.Ls1,
        XivChatType.Ls2,
        XivChatType.Ls3,
        XivChatType.Ls4,
        XivChatType.Ls5,
        XivChatType.Ls6,
        XivChatType.Ls7,
        XivChatType.Ls8,
        XivChatType.CrossLinkShell1,
        XivChatType.CrossLinkShell2,
        XivChatType.CrossLinkShell3,
        XivChatType.CrossLinkShell4,
        XivChatType.CrossLinkShell5,
        XivChatType.CrossLinkShell6,
        XivChatType.CrossLinkShell7,
        XivChatType.CrossLinkShell8,
        XivChatType.Echo,
        XivChatType.SystemMessage,
    };

    private static bool IsTellType(XivChatType type) =>
        type is XivChatType.TellIncoming or XivChatType.TellOutgoing;

    private string? editingChannelId;
    private string? pendingRemoveId;
    private string? pendingMoveId;
    private int pendingMoveDelta;

    public void DrawChannels()
    {
        ConsumeScroll();

        var editing = Cfg.Channels.FirstOrDefault(c => c.Id == editingChannelId);

        if (editing != null)
        {
            DrawChannelEditor(editing);
            return;
        }

        editingChannelId = null;

        Layout.Draw(
            "Channels",
            "A channel bundles game chat types and an optional Discord channel.",
            innerWidth =>
            {
                if (theme.PrimaryButton("Add Channel##chatbox", new Vector2(theme.Scaled(160f), theme.Scaled(UiTheme.ControlHeight))))
                {
                    var created = new ChatboxChannelConfig
                    {
                        Name = "New Channel",
                        Order = Cfg.Channels.Count,
                    };

                    Cfg.Channels.Add(created);
                    Save();
                    plugin.Chatbox.RebuildChannels();
                    OpenChannelEditor(created.Id);
                }

                theme.SameLineGap();

                if (theme.SecondaryButton("Sort by Order##chatbox", new Vector2(theme.Scaled(160f), theme.Scaled(UiTheme.ControlHeight))))
                {
                    Cfg.Channels.Sort((a, b) => a.Order.CompareTo(b.Order));
                    Save();
                    plugin.Chatbox.RebuildChannels();
                }

                _ = innerWidth;
            });

        Card.Draw(
            "chatbox-channel-list",
            innerWidth =>
            {
                if (Cfg.Channels.Count == 0)
                {
                    ImGui.TextColored(theme.FaintText, "No channels configured yet.");
                    return;
                }

                for (int i = 0; i < Cfg.Channels.Count; i++)
                    DrawChannelListRow(Cfg.Channels[i], i, innerWidth);
            },
            "Configured Channels",
            anchor => DrawCountChip(anchor, Cfg.Channels.Count == 1 ? "1 channel" : $"{Cfg.Channels.Count} channels"));

        ApplyPendingChannelChanges();
    }

    private void OpenChannelEditor(string id)
    {
        editingChannelId = id;
        pendingScrollTop = true;
    }

    private void DrawChannelListRow(ChatboxChannelConfig channel, int index, float rowWidth)
    {
        string title = string.IsNullOrWhiteSpace(channel.Name) ? $"Channel {index + 1}" : channel.Name;
        string subtitle = DescribeChannel(channel);

        var result = Row.Draw(
            id: $"chatbox-channel-{channel.Id}",
            icon: FontAwesomeIcon.Hashtag,
            iconColor: channel.Enabled ? channel.Color : theme.MutedText,
            title: title,
            subtitle: subtitle,
            controlWidth: 172f,
            drawControl: (pos, width) =>
            {
                float size = theme.Scaled(UiTheme.ActionButtonSize);
                float step = (width - size) / 3f;

                if (theme.IconAction($"##chatbox-up-{channel.Id}", pos, FontAwesomeIcon.ArrowUp, theme.MutedText, "Move up"))
                {
                    pendingMoveId = channel.Id;
                    pendingMoveDelta = -1;
                }

                var next = new Vector2(pos.X + step, pos.Y);

                if (theme.IconAction($"##chatbox-down-{channel.Id}", next, FontAwesomeIcon.ArrowDown, theme.MutedText, "Move down"))
                {
                    pendingMoveId = channel.Id;
                    pendingMoveDelta = 1;
                }

                next = new Vector2(pos.X + step * 2f, pos.Y);

                if (theme.ToggleAction($"##chatbox-power-{channel.Id}", next, channel.Enabled, channel.Enabled ? "Disable channel" : "Enable channel"))
                {
                    channel.Enabled = !channel.Enabled;
                    Save();
                    plugin.Chatbox.RebuildChannels();
                }

                next = new Vector2(pos.X + step * 3f, pos.Y);

                if (theme.DeleteAction($"##chatbox-del-{channel.Id}", next, "Delete channel"))
                    pendingRemoveId = channel.Id;
            },
            showChevron: true,
            rowWidth: rowWidth);

        if (result.RowClicked || result.ChevronClicked)
            OpenChannelEditor(channel.Id);
    }

    private string DescribeChannel(ChatboxChannelConfig channel)
    {
        string types = channel.GameChatTypes.Count == 0
            ? "No game chat"
            : string.Join(", ", channel.GameChatTypes.Select(ChatboxService.LabelFor).Distinct());

        string discord = string.IsNullOrEmpty(channel.DiscordChannelId)
            ? "no Discord channel"
            : "linked to Discord";

        return $"{types} · {discord}";
    }

    private void ApplyPendingChannelChanges()
    {
        if (pendingRemoveId != null)
        {
            if (editingChannelId == pendingRemoveId)
                editingChannelId = null;

            Cfg.Channels.RemoveAll(c => c.Id == pendingRemoveId);
            pendingRemoveId = null;
            Save();
            plugin.Chatbox.RebuildChannels();
        }

        if (pendingMoveId == null)
            return;

        int index = Cfg.Channels.FindIndex(c => c.Id == pendingMoveId);
        int target = index + pendingMoveDelta;
        pendingMoveId = null;

        if (index < 0 || target < 0 || target >= Cfg.Channels.Count)
            return;

        (Cfg.Channels[index], Cfg.Channels[target]) = (Cfg.Channels[target], Cfg.Channels[index]);

        for (int i = 0; i < Cfg.Channels.Count; i++)
            Cfg.Channels[i].Order = i;

        Save();
        plugin.Chatbox.RebuildChannels();
    }

    private void DrawChannelEditor(ChatboxChannelConfig channel)
    {
        string title = string.IsNullOrWhiteSpace(channel.Name) ? "Channel" : channel.Name;

        Layout.Draw(title, DescribeChannel(channel), innerWidth =>
        {
            var back = Row.Draw(
                id: "chatbox-channel-back",
                icon: FontAwesomeIcon.ArrowLeft,
                iconColor: theme.Accent,
                title: "Back to Channels",
                subtitle: "Return to the channel list",
                rowWidth: innerWidth);

            if (back.RowClicked)
            {
                editingChannelId = null;
                pendingScrollTop = true;
            }
        });

        if (editingChannelId == null)
            return;

        DrawChannelIdentity(channel);
        DrawChannelSources(channel);
        DrawChannelSending(channel);
        DrawChannelBehaviour(channel);
        DrawChannelHistory(channel);

        ApplyPendingChannelChanges();
    }

    private void DrawChannelIdentity(ChatboxChannelConfig channel) =>
        Card.Draw($"chatbox-identity-{channel.Id}", innerWidth =>
        {
            DrawToggleRow(
                $"chatbox-enabled-{channel.Id}", FontAwesomeIcon.PowerOff,
                "Enabled", "Messages are only captured while the channel is enabled.",
                innerWidth,
                () => channel.Enabled,
                v =>
                {
                    channel.Enabled = v;
                    plugin.Chatbox.RebuildChannels();
                },
                UiTheme.TileGreen);

            DrawToggleRow(
                $"chatbox-nav-{channel.Id}", FontAwesomeIcon.Bars,
                "Show in Navigation", "Lists the channel in the chatbox sidebar.",
                innerWidth, () => channel.ShowInNav, v => channel.ShowInNav = v);

            DrawTextRow(
                $"chatbox-name-{channel.Id}", FontAwesomeIcon.Tag,
                "Name", "Shown in the chatbox navigation.",
                innerWidth, () => channel.Name, v => channel.Name = v, 64, "Channel name");

            DrawTextRow(
                $"chatbox-short-{channel.Id}", FontAwesomeIcon.Font,
                "Rail Label", "Up to six characters, used by the server rail.",
                innerWidth, () => channel.ShortLabel, v => channel.ShortLabel = v, 6, "ABC", 120f);

            DrawTextRow(
                $"chatbox-icon-{channel.Id}", FontAwesomeIcon.Image,
                "Icon URL", "Optional image shown instead of the label.",
                innerWidth, () => channel.IconUrl, v => channel.IconUrl = v, 260, "https://...", 320f);

            DrawColorRow(
                $"chatbox-color-{channel.Id}",
                "Colour", "Tints the channel entry and its names.",
                innerWidth, () => channel.Color, v => channel.Color = v);
        }, "Identity");

    private void DrawChannelSources(ChatboxChannelConfig channel) =>
        Card.Draw($"chatbox-sources-{channel.Id}", innerWidth =>
        {
            var items = SelectableChatTypes
                .Select(type => new DropdownItem { Key = type.ToString(), Label = ChatboxService.LabelFor(type) })
                .ToList();

            string preview = channel.GameChatTypes.Count == 0
                ? "None"
                : string.Join(", ", channel.GameChatTypes.Select(ChatboxService.LabelFor).Distinct());

            Row.Draw(
                id: $"chatbox-types-{channel.Id}",
                icon: FontAwesomeIcon.Comments,
                iconColor: channel.GameChatTypes.Count == 0 ? theme.MutedText : theme.Accent,
                title: "Game Chats",
                subtitle: "Which in-game chat types land in this channel.",
                controlWidth: 280f,
                drawControl: (pos, width) =>
                {
                    ImGui.SetCursorScreenPos(pos);
                    theme.MultiPicker(
                        $"chatbox-types-picker-{channel.Id}",
                        preview,
                        channel.GameChatTypes.Count > 0,
                        items,
                        key => Enum.TryParse<XivChatType>(key, out var parsed) && channel.GameChatTypes.Contains(parsed),
                        key =>
                        {
                            if (!Enum.TryParse<XivChatType>(key, out var parsed))
                                return;

                            ToggleChatType(channel, parsed);
                        },
                        width);
                },
                rowWidth: innerWidth);

            bool linked = !string.IsNullOrEmpty(channel.DiscordChannelId);

            Row.Draw(
                id: $"chatbox-discord-{channel.Id}",
                icon: linked ? FontAwesomeIcon.Hashtag : FontAwesomeIcon.ExclamationTriangle,
                iconColor: linked ? UiTheme.TileGreen : UiTheme.TileAmber,
                title: "Discord Channel",
                subtitle: linked ? "Messages are mirrored here" : "Not linked to Discord",
                controlWidth: 280f,
                drawControl: (pos, width) =>
                {
                    ImGui.SetCursorScreenPos(pos);
                    theme.ChannelPicker(
                        $"chatbox-discord-picker-{channel.Id}",
                        channel.DiscordChannelId,
                        plugin.Channels.TextChannels,
                        id =>
                        {
                            channel.DiscordChannelId = id;
                            Save();
                            plugin.Chatbox.RebuildChannels();
                        },
                        defaultLabel: "No Discord Channel",
                        showLabel: false,
                        width: width);
                },
                rowWidth: innerWidth);
        }, "Sources");

    private void ToggleChatType(ChatboxChannelConfig channel, XivChatType type)
    {
        bool add = !channel.GameChatTypes.Contains(type);

        if (add)
            channel.GameChatTypes.Add(type);
        else
            channel.GameChatTypes.Remove(type);

        if (IsTellType(type))
        {
            if (add)
                channel.GameChatTypes.Add(XivChatType.TellOutgoing);
            else
                channel.GameChatTypes.Remove(XivChatType.TellOutgoing);
        }

        Save();
        plugin.Chatbox.RebuildChannels();
    }

    private void DrawChannelSending(ChatboxChannelConfig channel) =>
        Card.Draw($"chatbox-sending-{channel.Id}", innerWidth =>
        {
            DrawToggleRow(
                $"chatbox-sendtogame-{channel.Id}", FontAwesomeIcon.PaperPlane,
                "Send to Game", "Messages typed here are forwarded into game chat.",
                innerWidth, () => channel.SendToGame, v => channel.SendToGame = v);

            var items = new List<DropdownItem>
            {
                new() { Key = XivChatType.None.ToString(), Label = "None (Tell reply)" },
            };

            items.AddRange(SelectableChatTypes
                .Where(type => !IsTellType(type))
                .Select(type => new DropdownItem { Key = type.ToString(), Label = ChatboxService.LabelFor(type) }));

            Row.Draw(
                id: $"chatbox-sendtype-{channel.Id}",
                icon: FontAwesomeIcon.Reply,
                iconColor: theme.Accent,
                title: "Send as",
                subtitle: "Chat type used when this channel sends into the game.",
                controlWidth: 240f,
                drawControl: (pos, width) =>
                {
                    ImGui.SetCursorScreenPos(pos);
                    theme.OptionPicker(
                        $"chatbox-sendtype-picker-{channel.Id}",
                        channel.SendGameChatType.ToString(),
                        items,
                        key =>
                        {
                            if (!Enum.TryParse<XivChatType>(key, out var parsed))
                                return;

                            channel.SendGameChatType = parsed;
                            Save();
                        },
                        width);
                },
                rowWidth: innerWidth);
        }, "Sending");

    private void DrawChannelBehaviour(ChatboxChannelConfig channel) =>
        Card.Draw($"chatbox-behaviour-{channel.Id}", innerWidth =>
        {
            DrawToggleRow(
                $"chatbox-mute-{channel.Id}", FontAwesomeIcon.BellSlash,
                "Mute Notifications", "No sounds, badges or popups from this channel.",
                innerWidth, () => channel.MuteNotifications, v => channel.MuteNotifications = v);

            DrawToggleRow(
                $"chatbox-allmention-{channel.Id}", FontAwesomeIcon.At,
                "Treat every Message as Mention", "Every message here counts as a mention.",
                innerWidth, () => channel.TreatAllAsMention, v => channel.TreatAllAsMention = v);

            DrawToggleRow(
                $"chatbox-combined-{channel.Id}", FontAwesomeIcon.LayerGroup,
                "Include in Combined View", "Messages also appear in the combined channel.",
                innerWidth, () => channel.IncludeInCombined, v => channel.IncludeInCombined = v);

            DrawToggleRow(
                $"chatbox-ads-{channel.Id}", FontAwesomeIcon.Filter,
                "Hide Advertisements", "Uses the Advertisement Filter from the Chats page. Blocked messages collapse into a placeholder.",
                innerWidth, () => channel.FilterAdvertisements, v => channel.FilterAdvertisements = v);
        }, "Behaviour");

    private void DrawChannelHistory(ChatboxChannelConfig channel) =>
        Card.Draw($"chatbox-history-{channel.Id}", innerWidth =>
        {
            DrawToggleRow(
                $"chatbox-persist-{channel.Id}", FontAwesomeIcon.Save,
                "Save History", "Keeps this channel's messages on disk between sessions.",
                innerWidth, () => channel.PersistHistory, v => channel.PersistHistory = v);

            DrawIntSliderRow(
                $"chatbox-limit-{channel.Id}", FontAwesomeIcon.Sort,
                "Messages kept",
                channel.MaxMessages <= 0
                    ? $"0 uses the global default ({Cfg.MaxMessagesPerChannel})."
                    : "How many messages this channel keeps.",
                innerWidth, 0, 50000,
                () => channel.MaxMessages, v => channel.MaxMessages = v);

            DrawActionRow(
                $"chatbox-forget-{channel.Id}", FontAwesomeIcon.TrashAlt, UiTheme.TileRed,
                "Delete History", $"{plugin.Chatbox.Store.CountFor(channel.Id)} messages stored.",
                "Delete History", innerWidth,
                () => plugin.Chatbox.ForgetChannel(channel.Id));
        }, "History");
}
