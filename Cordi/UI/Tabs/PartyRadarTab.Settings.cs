using System.Numerics;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Tabs;

public partial class PartyRadarTab
{
    private void DrawDiscordCard()
    {
        Card.Draw(
            "party-discord",
            innerWidth =>
            {
                DrawToggleRow(
                    "party-discord-enabled",
                    FontAwesomeIcon.Bell,
                    Party.DiscordEnabled ? UiTheme.TileTeal : theme.MutedText,
                    "Announce party changes",
                    Party.DiscordEnabled
                        ? "Join, leave and full messages are posted to Discord"
                        : "Party changes stay in game, nothing is posted",
                    innerWidth,
                    () => Party.DiscordEnabled,
                    value => Party.DiscordEnabled = value);

                Row.Draw(
                    id: "party-discord-channel",
                    icon: FontAwesomeIcon.Hashtag,
                    iconColor: string.IsNullOrEmpty(Party.DiscordChannelId) ? theme.MutedText : theme.Accent,
                    title: "Notification channel",
                    subtitle: "Where party messages and summaries are posted",
                    controlWidth: 240f,
                    drawControl: (_, width) => theme.ChannelPicker(
                        "party-channel",
                        Party.DiscordChannelId,
                        plugin.Channels.TextChannels,
                        newId =>
                        {
                            Party.DiscordChannelId = newId;
                            Save();
                        },
                        defaultLabel: "Select a Channel...",
                        showLabel: false,
                        width: width),
                    rowWidth: innerWidth);

                bool hasChannel = !string.IsNullOrEmpty(Party.DiscordChannelId);
                bool hasParty = plugin.PartyService.PartyMembers.Count > 0;

                Row.Draw(
                    id: "party-discord-summary",
                    icon: FontAwesomeIcon.PaperPlane,
                    iconColor: hasChannel && hasParty ? UiTheme.TileBlue : theme.MutedText,
                    title: "Send summary now",
                    subtitle: !hasChannel
                        ? "Pick a channel first"
                        : hasParty
                            ? "Posts the current roster with gear and progress"
                            : "Join a party first",
                    controlWidth: 150f,
                    drawControl: (pos, width) =>
                    {
                        float buttonHeight = theme.Scaled(32f);
                        ImGui.SetCursorScreenPos(
                            new Vector2(pos.X, pos.Y + (theme.Scaled(UiTheme.ControlHeight) - buttonHeight) * 0.5f));

                        using var disabled = ImRaii.Disabled(!hasChannel || !hasParty);

                        if (theme.SecondaryButton("Send Summary", new Vector2(width, buttonHeight)))
                            _ = plugin.PartyService.SendPartySummary();
                    },
                    rowWidth: innerWidth);
            },
            label: "Discord");
    }

    private void DrawTriggersCard()
    {
        Card.Draw(
            "party-triggers",
            innerWidth =>
            {
                DrawToggleRow(
                    "party-trigger-join",
                    FontAwesomeIcon.UserPlus,
                    Party.NotifyJoin ? UiTheme.TileGreen : theme.MutedText,
                    "Someone joins",
                    "Posts the new member with their gear and raid progress",
                    innerWidth,
                    () => Party.NotifyJoin,
                    value => Party.NotifyJoin = value);

                DrawToggleRow(
                    "party-trigger-leave",
                    FontAwesomeIcon.UserMinus,
                    Party.NotifyLeave ? UiTheme.TileRed : theme.MutedText,
                    "Someone leaves",
                    "Posts a short message when a member drops out",
                    innerWidth,
                    () => Party.NotifyLeave,
                    value => Party.NotifyLeave = value);

                DrawToggleRow(
                    "party-trigger-full",
                    FontAwesomeIcon.Users,
                    Party.NotifyFull ? UiTheme.TilePurple : theme.MutedText,
                    "Party fills up",
                    "A one line message the moment the party hits 8/8",
                    innerWidth,
                    () => Party.NotifyFull,
                    value => Party.NotifyFull = value);

                DrawToggleRow(
                    "party-trigger-summary",
                    FontAwesomeIcon.ClipboardList,
                    Party.AutoSendSummary ? UiTheme.TileBlue : theme.MutedText,
                    "Summary when full",
                    "Posts the whole roster automatically once the party hits 8/8",
                    innerWidth,
                    () => Party.AutoSendSummary,
                    value => Party.AutoSendSummary = value);
            },
            label: "Triggers");
    }

    private void DrawTrackingCard()
    {
        Card.Draw(
            "party-tracking",
            innerWidth =>
            {
                DrawToggleRow(
                    "party-tracking-alliance",
                    FontAwesomeIcon.LayerGroup,
                    Party.ExcludeAlliance ? UiTheme.TileAmber : theme.MutedText,
                    "Ignore alliance parties",
                    Party.ExcludeAlliance
                        ? "Alliance raids of up to 24 players are skipped"
                        : "Alliance raids are tracked like any other party",
                    innerWidth,
                    () => Party.ExcludeAlliance,
                    value => Party.ExcludeAlliance = value);

                DrawToggleRow(
                    "party-tracking-self",
                    FontAwesomeIcon.UserCheck,
                    Party.IncludeSelf ? UiTheme.TileTeal : theme.MutedText,
                    "Include yourself",
                    "Your own character is listed and looked up like everyone else",
                    innerWidth,
                    () => Party.IncludeSelf,
                    value => Party.IncludeSelf = value);

                Row.Draw(
                    id: "party-tracking-notice",
                    icon: FontAwesomeIcon.ExclamationTriangle,
                    iconColor: UiTheme.TileAmber,
                    title: "Gear and progress come from Tomestone",
                    subtitle: "Third party data, it can be missing or out of date",
                    rowWidth: innerWidth);

                DrawToggleRow(
                    "party-tracking-gear",
                    FontAwesomeIcon.Shield,
                    Party.ShowGearLevel ? UiTheme.TileBlue : theme.MutedText,
                    "Show item level",
                    "Adds each member's average item level to the roster and to Discord",
                    innerWidth,
                    () => Party.ShowGearLevel,
                    value => Party.ShowGearLevel = value);

                DrawToggleRow(
                    "party-tracking-savage",
                    FontAwesomeIcon.Trophy,
                    Party.ShowSavageProgress ? UiTheme.TilePurple : theme.MutedText,
                    "Show savage progress",
                    "Adds clears and best pull percentages to Discord messages",
                    innerWidth,
                    () => Party.ShowSavageProgress,
                    value => Party.ShowSavageProgress = value);
            },
            label: "Tracking");
    }

    private void DrawMemoryCard()
    {
        Card.Draw(
            "party-memory",
            innerWidth =>
            {
                DrawToggleRow(
                    "party-memory-enabled",
                    FontAwesomeIcon.Brain,
                    Memory.Enabled ? UiTheme.TileTeal : theme.MutedText,
                    "Remember everyone I party with",
                    Memory.Enabled
                        ? "New party members are saved so you can leave them a note"
                        : "Nobody new is saved, players you already remembered are kept",
                    innerWidth,
                    () => Memory.Enabled,
                    value => Memory.Enabled = value);

                Row.Draw(
                    id: "party-memory-notes",
                    icon: FontAwesomeIcon.StickyNote,
                    iconColor: UiTheme.TileBlue,
                    title: "Notes show up in Discord",
                    subtitle: "A saved note is added to that player's join message",
                    rowWidth: innerWidth);
            },
            label: "Memory");
    }
}
