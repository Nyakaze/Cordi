using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Domain;
using Cordi.Services.Features;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using DiscordChannel = Crovus.Models.DiscordChannel;

namespace Cordi.UI.Tabs;

public partial class ChatsTab
{
    private sealed class ChatTypeRow
    {
        public required XivChatType Type { get; init; }
        public required string Label { get; init; }
        public required FontAwesomeIcon Icon { get; init; }
        public required Vector4 Color { get; init; }
    }

    private static readonly ChatTypeRow[] PrimaryChatRows =
    {
        new() { Type = XivChatType.Say, Label = "Say", Icon = FontAwesomeIcon.CommentDots, Color = UiTheme.TilePurple },
        new() { Type = XivChatType.Shout, Label = "Shout", Icon = FontAwesomeIcon.Bullhorn, Color = UiTheme.TilePink },
        new() { Type = XivChatType.Yell, Label = "Yell", Icon = FontAwesomeIcon.ExclamationCircle, Color = UiTheme.TileAmber },
        new() { Type = XivChatType.Party, Label = "Party", Icon = FontAwesomeIcon.Users, Color = UiTheme.TileGreen },
        new() { Type = XivChatType.Alliance, Label = "Alliance", Icon = FontAwesomeIcon.ShieldAlt, Color = UiTheme.TileBlue },
        new() { Type = XivChatType.FreeCompany, Label = "FreeCompany", Icon = FontAwesomeIcon.Flag, Color = UiTheme.TileTeal },
        new() { Type = XivChatType.TellIncoming, Label = "Tell", Icon = FontAwesomeIcon.Envelope, Color = UiTheme.TilePink },
    };

    private bool linkshellExpanded;
    private bool crossWorldExpanded;

    private void DrawChatMappingsPage(IReadOnlyList<DiscordChannel>? textChannels, IReadOnlyList<DiscordChannel>? forumChannels)
    {
        Layout.Draw(
            "Channel Mappings",
            "Map Discord channels to in-game chat types",
            innerWidth => DrawDefaultChannelRow(textChannels, innerWidth));

        Card.Draw(
            "chat-mappings",
            innerWidth =>
            {
                foreach (var row in PrimaryChatRows)
                    DrawMappingRow(row.Type, row.Label, string.Empty, row.Icon, row.Color, textChannels, forumChannels, innerWidth);
            },
            label: "Chat Mappings",
            drawTrailing: anchor => theme.HelpPill(
                "quick-help",
                anchor,
                "Every chat type can be mapped to a Discord channel.\n" +
                "The toggle enables the advertisement filter for that chat type.\n" +
                "The smiley button controls whether Discord emoji are translated before they reach the game.\n" +
                "Tell needs to be mapped to a forum channel to be relayed."));

        DrawLinkshellCards(textChannels, forumChannels);
        DrawTellNotificationSection(textChannels);
    }

    private void DrawDefaultChannelRow(IReadOnlyList<DiscordChannel>? textChannels, float rowWidth)
    {
        string defaultChannelId = plugin.Config.Discord.DefaultChannelId;

        Row.Draw(
            id: "default-channel",
            icon: FontAwesomeIcon.Hashtag,
            iconColor: theme.Accent,
            title: "Default Channel",
            subtitle: "Fallback channel for unmapped messages",
            controlWidth: 300f,
            drawControl: (pos, width) =>
            {
                theme.ApplyFontScale(0.82f);
                using (ImRaii.PushColor(ImGuiCol.Text, theme.MutedText))
                {
                    ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y - ImGui.GetTextLineHeight() - theme.Scaled(5f)));
                    ImGui.TextUnformatted("Discord Channel");
                }
                theme.ApplyFontScale();

                ImGui.SetCursorScreenPos(pos);

                if (textChannels != null)
                {
                    theme.ChannelPicker(
                        "dsc-default-channel-combo",
                        defaultChannelId,
                        textChannels,
                        newId =>
                        {
                            plugin.Config.Discord.DefaultChannelId = newId;
                            plugin.Config.Save();
                        },
                        defaultLabel: "Select a Channel...",
                        showLabel: false,
                        width: width);
                }
                else
                {
                    if (theme.TextInput("##dsc-default-channel-id", pos, width, ref defaultChannelId, 32))
                    {
                        plugin.Config.Discord.DefaultChannelId = defaultChannelId;
                        plugin.Config.Save();
                    }
                }
            },
            rowWidth: rowWidth,
            rowHeight: 84f);
    }

    private void DrawMappingRow(
        XivChatType chatType,
        string label,
        string description,
        FontAwesomeIcon icon,
        Vector4 color,
        IReadOnlyList<DiscordChannel>? textChannels,
        IReadOnlyList<DiscordChannel>? forumChannels,
        float rowWidth)
    {
        bool isTell = chatType == XivChatType.TellIncoming;
        var targetChannels = isTell ? forumChannels : textChannels;

        string currentId = plugin.Config.MappingCache.TryGetValue(chatType, out var cachedId) ? cachedId : string.Empty;
        var mapping = plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == chatType);
        bool hasChannel = mapping != null && !string.IsNullOrEmpty(mapping.DiscordChannelId);

        Row.Draw(
            id: $"map-{chatType}",
            icon: icon,
            iconColor: color,
            title: label,
            subtitle: description,
            controlWidth: 282f,
            drawControl: (pos, width) => DrawMappingControls(chatType, currentId, mapping, hasChannel, isTell, targetChannels, pos, width),
            toggleValue: hasChannel && mapping!.EnableAdvertisementFilter,
            onToggle: value =>
            {
                if (mapping == null)
                    return;

                mapping.EnableAdvertisementFilter = value;
                plugin.Config.Save();
            },
            toggleEnabled: hasChannel,
            toggleTooltip: "Filter advertisements for this chat type",
            toggleDisabledTooltip: "Map a Discord channel first",
            rowWidth: rowWidth);
    }

    private void DrawMappingControls(
        XivChatType chatType,
        string currentId,
        ChannelMapping? mapping,
        bool hasChannel,
        bool isTell,
        IReadOnlyList<DiscordChannel>? targetChannels,
        Vector2 pos,
        float width)
    {
        float action = theme.Scaled(UiTheme.ActionButtonSize);
        float gap = theme.Gap();
        float pickerWidth = MathF.Max(theme.Scaled(80f), width - action - gap);

        ImGui.SetCursorScreenPos(pos);
        theme.ChannelPicker(
            $"combo-{chatType}",
            currentId,
            targetChannels,
            newId => ApplyMapping(chatType, newId, isTell),
            showLabel: false,
            width: pickerWidth);

        bool translate = mapping?.TranslateEmoji ?? true;
        var color = hasChannel && translate ? UiTheme.TileAmber : theme.MutedText;

        string tooltip = !hasChannel
            ? "Map a Discord channel first"
            : translate
                ? "Emoji translation on — Discord emoji and server emotes become text the game can show"
                : "Emoji translation off — Discord content is forwarded unchanged";

        bool clicked = theme.IconAction(
            $"emoji-{chatType}",
            new Vector2(pos.X + pickerWidth + gap, pos.Y),
            FontAwesomeIcon.Smile,
            color,
            tooltip,
            restColor: color);

        if (!clicked || !hasChannel || mapping == null) return;

        mapping.TranslateEmoji = !mapping.TranslateEmoji;
        plugin.Config.Save();
    }

    private void ApplyMapping(XivChatType chatType, string newId, bool isTell)
    {
        var mappings = plugin.Config.Chat.Mappings;

        if (string.IsNullOrEmpty(newId))
        {
            var map = mappings.FirstOrDefault(m => m.GameChatType == chatType);
            if (map != null)
                mappings.Remove(map);

            if (isTell)
            {
                var mapOut = mappings.FirstOrDefault(m => m.GameChatType == XivChatType.TellOutgoing);
                if (mapOut != null)
                    mappings.Remove(mapOut);
            }
        }
        else
        {
            var map = mappings.FirstOrDefault(m => m.GameChatType == chatType);
            if (map != null)
                map.DiscordChannelId = newId;
            else
                mappings.Add(new ChannelMapping { GameChatType = chatType, DiscordChannelId = newId });

            if (isTell)
            {
                var mapOut = mappings.FirstOrDefault(m => m.GameChatType == XivChatType.TellOutgoing);
                if (mapOut != null)
                    mapOut.DiscordChannelId = newId;
                else
                    mappings.Add(new ChannelMapping { GameChatType = XivChatType.TellOutgoing, DiscordChannelId = newId });
            }
        }

        plugin.Config.Save();
    }

    private void DrawLinkshellCards(IReadOnlyList<DiscordChannel>? textChannels, IReadOnlyList<DiscordChannel>? forumChannels)
    {
        var start = ImGui.GetCursorScreenPos();
        float full = ImGui.GetContentRegionAvail().X;
        float cardWidth = (full - theme.Gap()) * 0.5f;

        if (Layout.DrawNavCard("linkshell", FontAwesomeIcon.Link, "Linkshell", "Configure Linkshell chat channels", cardWidth))
            linkshellExpanded = !linkshellExpanded;

        var afterCards = ImGui.GetCursorScreenPos();

        ImGui.SetCursorScreenPos(new Vector2(start.X + cardWidth + theme.Gap(), start.Y));
        if (Layout.DrawNavCard("cwls", FontAwesomeIcon.Globe, "Cross-World Linkshell", "Configure Cross-World Linkshell channels", cardWidth))
            crossWorldExpanded = !crossWorldExpanded;

        ImGui.SetCursorScreenPos(afterCards);

        if (IsExtraChatAvailable)
        {
            ImGui.SetCursorScreenPos(new Vector2(start.X + (full - cardWidth) * 0.5f, afterCards.Y));

            if (Layout.DrawNavCard("extrachat", FontAwesomeIcon.ProjectDiagram, "ExtraChat Mappings", "Map ExtraChat channels to Discord", cardWidth))
                extraChatExpanded = !extraChatExpanded;

            ImGui.SetCursorScreenPos(new Vector2(start.X, ImGui.GetCursorScreenPos().Y));
        }

        if (linkshellExpanded)
        {
            Card.Draw(
                "linkshell-rows",
                innerWidth =>
                {
                    for (int i = 0; i < ChatTypes.Linkshells.Length; i++)
                    {
                        var name = LinkshellNameService.GetLinkshellName(i);
                        DrawMappingRow(
                            ChatTypes.Linkshells[i],
                            name ?? $"Linkshell {i + 1}",
                            name != null ? $"Linkshell {i + 1}" : "Not joined on this character",
                            FontAwesomeIcon.Link,
                            UiTheme.TileBlue,
                            textChannels,
                            forumChannels,
                            innerWidth);
                    }
                },
                label: "Linkshells");
        }

        if (crossWorldExpanded)
        {
            Card.Draw(
                "cwls-rows",
                innerWidth =>
                {
                    for (int i = 0; i < ChatTypes.CrossWorldLinkshells.Length; i++)
                    {
                        var name = LinkshellNameService.GetCrossWorldLinkshellName(i);
                        DrawMappingRow(
                            ChatTypes.CrossWorldLinkshells[i],
                            name ?? $"CWLS {i + 1}",
                            name != null ? $"Cross-world linkshell {i + 1}" : "Not joined on this character",
                            FontAwesomeIcon.Globe,
                            UiTheme.TileTeal,
                            textChannels,
                            forumChannels,
                            innerWidth);
                    }
                },
                label: "Cross-World Linkshells");
        }

        if (IsExtraChatAvailable && extraChatExpanded)
            DrawExtraChatCard(textChannels);
    }

    private void DrawTellNotificationSection(IReadOnlyList<DiscordChannel>? textChannels)
    {
        Card.Draw(
            "tell-notifications",
            innerWidth =>
            {
                bool tellNotif = plugin.Config.Chat.EnableTellNotification;

                Row.Draw(
                    id: "tell-notification",
                    icon: FontAwesomeIcon.Bell,
                    iconColor: UiTheme.TileAmber,
                    title: "Tell notification",
                    subtitle: "Announce incoming tells in a separate channel",
                    toggleValue: tellNotif,
                    onToggle: value =>
                    {
                        plugin.Config.Chat.EnableTellNotification = value;
                        plugin.Config.Save();
                    },
                    rowWidth: innerWidth);

                if (!tellNotif)
                    return;

                Row.Draw(
                    id: "tell-notification-channel",
                    icon: FontAwesomeIcon.Hashtag,
                    iconColor: theme.Accent,
                    title: "Notification channel",
                    subtitle: "Where tell notifications are posted",
                    controlWidth: 240f,
                    drawControl: (_, width) => theme.ChannelPicker(
                        "tell-notif-channel",
                        plugin.Config.Chat.TellNotificationChannelId,
                        textChannels,
                        newId =>
                        {
                            plugin.Config.Chat.TellNotificationChannelId = newId;
                            plugin.Config.Save();
                        },
                        defaultLabel: "Select a Channel...",
                        showLabel: false,
                        width: width),
                    rowWidth: innerWidth);

                Row.Draw(
                    id: "tell-notification-cooldown",
                    icon: FontAwesomeIcon.Stopwatch,
                    iconColor: UiTheme.TileGreen,
                    title: "Conversation cooldown",
                    subtitle: "Seconds before the same conversation notifies again",
                    controlWidth: 120f,
                    drawControl: (pos, width) =>
                    {
                        int cooldown = plugin.Config.Chat.TellNotificationCooldownSeconds;
                        if (theme.NumberInput("##tell-cooldown", pos, width, ref cooldown, 0))
                        {
                            plugin.Config.Chat.TellNotificationCooldownSeconds = cooldown;
                            plugin.Config.Save();
                        }
                    },
                    rowWidth: innerWidth);
            },
            label: "Tell Notifications");
    }
}
