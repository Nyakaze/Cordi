using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Domain;
using Cordi.Services.Chatbox;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab
{
    private const string ChatGroupKeyPrefix = "group:";

    private static List<ChipSelectorGroup> BuildSelectableChatGroups(bool advanced)
    {
        var groups = ChatTypes.SelectableGroups
            .Where(group => advanced || group.Simple)
            .Select(group => new ChipSelectorGroup
            {
                Label = group.Group,
                Items = group.Types
                    .Select(type => new DropdownItem
                    {
                        Key = type.ToString(),
                        Label = ChatboxService.LabelFor(type),
                    })
                    .ToList(),
            })
            .ToList();

        if (advanced)
            return groups;

        groups.Add(new ChipSelectorGroup
        {
            Label = "Categories",
            Items = ChatTypes.SelectableGroups
                .Where(group => !group.Simple)
                .Select(group => new DropdownItem
                {
                    Key = ChatGroupKeyPrefix + group.Group,
                    Label = group.Group,
                })
                .ToList(),
        });

        return groups;
    }

    private static IReadOnlyList<XivChatType> ResolveChatTypes(string key)
    {
        if (key.StartsWith(ChatGroupKeyPrefix, StringComparison.Ordinal))
        {
            string name = key[ChatGroupKeyPrefix.Length..];

            foreach (var group in ChatTypes.SelectableGroups)
            {
                if (string.Equals(group.Group, name, StringComparison.Ordinal))
                    return group.Types;
            }

            return Array.Empty<XivChatType>();
        }

        return Enum.TryParse<XivChatType>(key, out var parsed)
            ? new[] { parsed }
            : Array.Empty<XivChatType>();
    }

    private const string ChannelPayload = "CORDI_CHATBOX_CHANNEL";
    private static readonly byte[] ChannelPayloadData = { 1 };

    private string? editingChannelId;
    private string? pendingRemoveId;
    private int draggedChannelIndex = -1;
    private int pendingDropIndex = -1;
    private bool channelDragActive;

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
            "A channel bundles the game chat types that share one tab.",
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

        if (!ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            channelDragActive = false;

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
            controlWidth: 80f,
            drawControl: (pos, width) =>
            {
                float size = theme.Scaled(UiTheme.ActionButtonSize);

                if (theme.ToggleAction($"##chatbox-power-{channel.Id}", pos, channel.Enabled, channel.Enabled ? "Disable channel" : "Enable channel"))
                {
                    channel.Enabled = !channel.Enabled;
                    Save();
                    plugin.Chatbox.RebuildChannels();
                }

                var next = new Vector2(pos.X + width - size, pos.Y);

                if (theme.DeleteAction($"##chatbox-del-{channel.Id}", next, "Delete channel"))
                    pendingRemoveId = channel.Id;
            },
            showChevron: true,
            rowWidth: rowWidth,
            onRowItem: () => HandleChannelDrag(index, title));

        if ((result.RowClicked || result.ChevronClicked) && !channelDragActive)
            OpenChannelEditor(channel.Id);
    }

    private void HandleChannelDrag(int index, string title)
    {
        if (ImGui.BeginDragDropSource())
        {
            draggedChannelIndex = index;
            channelDragActive = true;
            ImGui.SetDragDropPayload(ChannelPayload, ChannelPayloadData, ImGuiCond.Always);
            ImGui.TextUnformatted(title);
            ImGui.EndDragDropSource();
        }

        if (!ImGui.BeginDragDropTarget())
            return;

        var payload = ImGui.AcceptDragDropPayload(ChannelPayload, ImGuiDragDropFlags.None);

        if (!payload.IsNull && draggedChannelIndex >= 0 && draggedChannelIndex != index)
            pendingDropIndex = index;

        ImGui.EndDragDropTarget();
    }

    private string DescribeChannel(ChatboxChannelConfig channel) =>
        channel.GameChatTypes.Count == 0
            ? "No game chat"
            : string.Join(", ", channel.GameChatTypes.Select(ChatboxService.LabelFor).Distinct());

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

        if (pendingDropIndex < 0)
            return;

        int from = draggedChannelIndex;
        int to = pendingDropIndex;

        draggedChannelIndex = -1;
        pendingDropIndex = -1;

        if (from < 0 || from >= Cfg.Channels.Count || to < 0 || to >= Cfg.Channels.Count || from == to)
            return;

        var moved = Cfg.Channels[from];
        Cfg.Channels.RemoveAt(from);
        Cfg.Channels.Insert(to, moved);

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

            DrawToggleRow(
                $"chatbox-override-color-{channel.Id}", FontAwesomeIcon.Palette,
                "Override Chat Colours", "Messages take this channel's colour instead of the global colour of their chat type.",
                innerWidth, () => channel.OverrideChatColor, v => channel.OverrideChatColor = v);
        }, "Identity");

    private void DrawChannelSources(ChatboxChannelConfig channel) =>
        Card.Draw($"chatbox-sources-{channel.Id}", innerWidth =>
        {
            theme.WrappedText(
                Cfg.AdvancedChatTypes
                    ? "Which in-game chat types land in this channel. Game Master messages always arrive."
                    : "Which in-game chat types land in this channel. Categories switch on everything they contain, Advanced unfolds them into single types.",
                innerWidth,
                theme.MutedText);
            theme.SpacerY(0.6f);

            Chips.Draw(
                $"chatbox-types-{channel.Id}",
                innerWidth,
                BuildSelectableChatGroups(Cfg.AdvancedChatTypes),
                key => IsChatKeySelected(channel, key),
                key => ToggleChatKey(channel, key),
                (keys, enabled) => SetChatTypes(channel, keys, enabled),
                Cfg.AdvancedChatTypes,
                advanced =>
                {
                    Cfg.AdvancedChatTypes = advanced;
                    Save();
                },
                key => IsChatKeyPartial(channel, key));
        }, "Game Chats");

    private static bool ApplyChatType(ChatboxChannelConfig channel, XivChatType type, bool enabled)
    {
        if (channel.GameChatTypes.Contains(type) == enabled)
            return false;

        if (enabled)
            channel.GameChatTypes.Add(type);
        else
            channel.GameChatTypes.Remove(type);

        if (!ChatTypes.IsTell(type))
            return true;

        if (enabled)
        {
            if (!channel.GameChatTypes.Contains(XivChatType.TellOutgoing))
                channel.GameChatTypes.Add(XivChatType.TellOutgoing);
        }
        else
        {
            channel.GameChatTypes.Remove(XivChatType.TellOutgoing);
        }

        return true;
    }

    private static bool IsChatKeySelected(ChatboxChannelConfig channel, string key)
    {
        var types = ResolveChatTypes(key);

        return types.Count > 0 && types.All(channel.GameChatTypes.Contains);
    }

    private static bool IsChatKeyPartial(ChatboxChannelConfig channel, string key)
    {
        var types = ResolveChatTypes(key);

        if (types.Count < 2)
            return false;

        int active = types.Count(channel.GameChatTypes.Contains);

        return active > 0 && active < types.Count;
    }

    private void ToggleChatKey(ChatboxChannelConfig channel, string key)
    {
        var types = ResolveChatTypes(key);

        if (types.Count == 0)
            return;

        ApplyChatTypes(channel, types, !types.All(channel.GameChatTypes.Contains));
    }

    private void SetChatTypes(ChatboxChannelConfig channel, IReadOnlyList<string> keys, bool enabled) =>
        ApplyChatTypes(channel, keys.SelectMany(ResolveChatTypes).ToList(), enabled);

    private void ApplyChatTypes(ChatboxChannelConfig channel, IReadOnlyList<XivChatType> types, bool enabled)
    {
        bool changed = false;

        foreach (var type in types)
            changed |= ApplyChatType(channel, type, enabled);

        if (!changed)
            return;

        Save();
        plugin.Chatbox.RebuildChannels();
    }

    private void DrawChannelSending(ChatboxChannelConfig channel) =>
        Card.Draw($"chatbox-sending-{channel.Id}", innerWidth =>
        {
            var items = new List<DropdownItem>
            {
                new() { Key = XivChatType.None.ToString(), Label = "None" },
            };

            items.AddRange(ChatTypes.Sendable
                .Select(type => new DropdownItem
                {
                    Key = type.ToString(),
                    Label = ChatboxService.LabelFor(type),
                    Group = ChatTypes.SendGroup(type),
                }));

            Row.Draw(
                id: $"chatbox-sendtype-{channel.Id}",
                icon: FontAwesomeIcon.Reply,
                iconColor: theme.Accent,
                title: "Send as",
                subtitle: "Pins the game chat type this channel sends in. None follows the picker next to the chatbox input.",
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
