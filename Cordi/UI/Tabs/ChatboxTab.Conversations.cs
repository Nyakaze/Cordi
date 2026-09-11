using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Services.Chatbox;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab
{
    private const float ConversationRowControlBand = 220f;

    private string conversationDraftName = string.Empty;
    private string conversationDraftWorld = string.Empty;
    private string conversationFilter = string.Empty;
    private string? pendingConversationRemoveId;

    private ConversationSettings Ccfg => plugin.Config.Chatbox.Conversations;

    public void DrawConversations()
    {
        ConsumeScroll();

        Layout.Draw(
            "Conversations",
            "Tells get their own tab with a history per person. Cordi never deletes that history.",
            DrawConversationsMaster);

        if (!Ccfg.Enabled)
            return;

        DrawConversationStartCard();
        DrawConversationListCard();
        DrawConversationRoutingCard();
        DrawConversationAlertCard();
        DrawConversationAppearanceCard();
        DrawConversationHistoryCard();

        ApplyPendingConversationChanges();
    }

    private void DrawConversationsMaster(float innerWidth)
    {
        bool enabled = Ccfg.Enabled;

        var result = Row.Draw(
            id: "conversations-master",
            icon: FontAwesomeIcon.Envelope,
            iconColor: enabled ? Ccfg.Color : theme.MutedText,
            title: enabled ? "Conversations are on" : "Conversations are off",
            subtitle: "Incoming and outgoing tells are grouped per character instead of landing in a shared channel.",
            toggleValue: enabled,
            onToggle: SetConversationsEnabled,
            rowWidth: innerWidth);

        if (result.RowClicked && !result.ToggleChanged)
            SetConversationsEnabled(!enabled);
    }

    private void SetConversationsEnabled(bool value)
    {
        Ccfg.Enabled = value;
        Save();
        plugin.Chatbox.RefreshConversationChannels();
    }

    private void DrawConversationStartCard() =>
        Card.Draw(
            "conversation-start",
            innerWidth =>
            {
                DrawConversationDraftRow(
                    "conversation-draft-name",
                    FontAwesomeIcon.User,
                    "Character name",
                    "The full name of the character you want to write to.",
                    innerWidth,
                    () => conversationDraftName,
                    value => conversationDraftName = value,
                    "Firstname Lastname");

                DrawConversationDraftRow(
                    "conversation-draft-world",
                    FontAwesomeIcon.Globe,
                    "World",
                    "Leave empty for a character on your own world.",
                    innerWidth,
                    () => conversationDraftWorld,
                    value => conversationDraftWorld = value,
                    "Omega");

                bool ready = conversationDraftName.Trim().Length > 0;

                DrawActionRow(
                    "conversation-draft-open",
                    FontAwesomeIcon.PaperPlane,
                    ready ? Ccfg.Color : theme.MutedText,
                    ready ? $"Open a tab for {DraftLabel()}" : "Nothing to open yet",
                    ready
                        ? "Creates the tab and loads whatever history Cordi already has for them."
                        : "Enter a character name first.",
                    "Open",
                    innerWidth,
                    StartDraftConversation);

                DrawToggleRow(
                    "conversation-context-menu",
                    FontAwesomeIcon.MousePointer,
                    "Add \"Message\" to the game context menu",
                    "Right-click a player anywhere in the game to open a conversation with them.",
                    innerWidth,
                    () => Ccfg.ContextMenuEntry,
                    value => Ccfg.ContextMenuEntry = value,
                    Ccfg.Color);
            },
            "Start a Conversation");

    private string DraftLabel()
    {
        var name = conversationDraftName.Trim();
        var world = conversationDraftWorld.Trim();

        return world.Length == 0 ? name : $"{name}@{world}";
    }

    private void StartDraftConversation()
    {
        var name = conversationDraftName.Trim();
        if (name.Length == 0)
            return;

        var state = plugin.Chatbox.OpenConversation(name, conversationDraftWorld.Trim(), true);
        if (state == null)
            return;

        if (!plugin.Chatbox.IsConversationDetached(state.Id))
            plugin.ChatboxWindow.IsOpen = true;

        conversationDraftName = string.Empty;
        conversationDraftWorld = string.Empty;
    }

    private void DrawConversationDraftRow(
        string id,
        FontAwesomeIcon icon,
        string title,
        string subtitle,
        float rowWidth,
        Func<string> get,
        Action<string> set,
        string hint)
    {
        string current = get();

        Row.Draw(
            id: id,
            icon: icon,
            iconColor: current.Trim().Length == 0 ? theme.MutedText : theme.Accent,
            title: title,
            subtitle: subtitle,
            controlWidth: 280f,
            drawControl: (pos, width) =>
            {
                string value = current;

                theme.PushInputScope();
                if (theme.TextInput($"##{id}-input", pos, width, ref value, 64, hint))
                    set(value);
                theme.PopInputScope();
            },
            rowWidth: rowWidth);
    }

    private void DrawConversationListCard()
    {
        var all = Ccfg.Items;
        var matches = FilteredConversations();

        Card.Draw(
            "conversation-list",
            innerWidth =>
            {
                DrawConversationFilterRow(innerWidth);

                if (all.Count == 0)
                {
                    ImGui.TextColored(theme.FaintText, "No conversations yet. Open one above, or wait for a tell.");
                    return;
                }

                if (matches.Count == 0)
                {
                    ImGui.TextColored(theme.FaintText, $"No conversation matches \"{conversationFilter.Trim()}\".");
                    return;
                }

                foreach (var entry in matches)
                    DrawConversationListRow(entry, innerWidth);
            },
            "Conversations",
            anchor => DrawCountChip(anchor, all.Count == 1 ? "1 person" : $"{all.Count} people"));
    }

    private void DrawConversationFilterRow(float rowWidth)
    {
        if (Ccfg.Items.Count == 0)
            return;

        DrawConversationDraftRow(
            "conversation-filter",
            FontAwesomeIcon.Search,
            "Find a conversation",
            "History is kept forever, so the list grows. Filter it by name or world.",
            rowWidth,
            () => conversationFilter,
            value => conversationFilter = value,
            "Search");
    }

    private List<ConversationConfig> FilteredConversations()
    {
        var ordered = Ccfg.Items
            .OrderByDescending(c => c.Pinned)
            .ThenByDescending(c => c.Open)
            .ThenByDescending(c => c.LastActivityTicks)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase);

        var needle = conversationFilter.Trim();

        if (needle.Length == 0)
            return ordered.ToList();

        return ordered
            .Where(c => c.Label.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void DrawConversationListRow(ConversationConfig entry, float rowWidth)
    {
        Row.Draw(
            id: $"conversation-{entry.Id}",
            icon: entry.Pinned ? FontAwesomeIcon.Thumbtack : FontAwesomeIcon.Comment,
            iconColor: Ccfg.Color,
            title: entry.Label,
            subtitle: $"Last message {DescribeConversationAge(entry.LastActivityTicks)}",
            controlWidth: ConversationRowControlBand,
            drawControl: (pos, width) =>
            {
                float size = theme.Scaled(UiTheme.ActionButtonSize);
                float step = (width - size) / 3f;

                if (theme.IconAction(
                        $"conversation-pin-{entry.Id}",
                        pos,
                        FontAwesomeIcon.Thumbtack,
                        UiTheme.TileAmber,
                        entry.Pinned ? "Unpin" : "Pin to the top",
                        restColor: entry.Pinned ? UiTheme.TileAmber : null))
                {
                    entry.Pinned = !entry.Pinned;
                    Save();
                }

                var openPos = new Vector2(pos.X + step, pos.Y);

                if (theme.IconAction(
                        $"conversation-open-{entry.Id}",
                        openPos,
                        FontAwesomeIcon.ExternalLinkAlt,
                        UiTheme.TileGreen,
                        "Open the tab"))
                {
                    plugin.Chatbox.OpenConversationFor(entry.Name, entry.World);
                }

                var clearPos = new Vector2(pos.X + step * 2f, pos.Y);

                if (theme.IconAction(
                        $"conversation-clear-{entry.Id}",
                        clearPos,
                        FontAwesomeIcon.Eraser,
                        UiTheme.TileAmber,
                        "Clear this conversation's history"))
                {
                    plugin.Chatbox.ForgetChannel(entry.Id);
                }

                var removePos = new Vector2(pos.X + width - size, pos.Y);

                if (theme.DeleteAction($"conversation-del-{entry.Id}", removePos, "Forget this person and their history"))
                    pendingConversationRemoveId = entry.Id;
            },
            rowWidth: rowWidth);
    }

    private static string DescribeConversationAge(long ticks)
    {
        if (ticks <= 0)
            return "never";

        var age = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);

        if (age < TimeSpan.Zero)
            return "just now";

        if (age.TotalMinutes < 1)
            return "just now";

        if (age.TotalHours < 1)
            return $"{(int)age.TotalMinutes} min ago";

        if (age.TotalDays < 1)
            return $"{(int)age.TotalHours} h ago";

        if (age.TotalDays < 30)
            return $"{(int)age.TotalDays} d ago";

        return new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("d");
    }

    private void ApplyPendingConversationChanges()
    {
        if (pendingConversationRemoveId == null)
            return;

        var id = pendingConversationRemoveId;
        pendingConversationRemoveId = null;

        plugin.Chatbox.RemoveConversation(id);
        plugin.Chatbox.ForgetChannel(id);
    }

    private void DrawConversationRoutingCard() =>
        Card.Draw(
            "conversation-routing",
            innerWidth =>
            {
                DrawOptionRow(
                    "conversation-routing-mode",
                    FontAwesomeIcon.Random,
                    "Where tells go",
                    "Conversations only keeps tells out of your regular channels.",
                    innerWidth,
                    () => Ccfg.TellRouting,
                    value => Ccfg.TellRouting = value,
                    Options(
                        (ConversationTellRouting.ConversationsOnly, "Conversations only"),
                        (ConversationTellRouting.Both, "Conversations and channels"),
                        (ConversationTellRouting.ChannelsOnly, "Channels only")));

                DrawToggleRow(
                    "conversation-auto-open-incoming",
                    FontAwesomeIcon.Inbox,
                    "Open on an incoming tell",
                    "Creates the tab and shows the chatbox when somebody writes to you.",
                    innerWidth,
                    () => Ccfg.AutoOpenIncoming,
                    value => Ccfg.AutoOpenIncoming = value,
                    Ccfg.Color);

                DrawToggleRow(
                    "conversation-auto-open-outgoing",
                    FontAwesomeIcon.PaperPlane,
                    "Open on an outgoing tell",
                    "Sending a tell from the game chat pulls the conversation up in Cordi.",
                    innerWidth,
                    () => Ccfg.AutoOpenOutgoing,
                    value => Ccfg.AutoOpenOutgoing = value,
                    Ccfg.Color);

                DrawToggleRow(
                    "conversation-auto-focus",
                    FontAwesomeIcon.Keyboard,
                    "Focus the input after an outgoing tell",
                    "Puts the cursor straight into the message box so you can keep typing.",
                    innerWidth,
                    () => Ccfg.AutoFocusOutgoing,
                    value => Ccfg.AutoFocusOutgoing = value,
                    Ccfg.Color);

                DrawToggleRow(
                    "conversation-reopen-login",
                    FontAwesomeIcon.DoorOpen,
                    "Reopen tabs on login",
                    "Off by default. History survives either way, this only restores the tabs.",
                    innerWidth,
                    () => Ccfg.ReopenOnLogin,
                    value => Ccfg.ReopenOnLogin = value,
                    Ccfg.Color);

                DrawToggleRow(
                    "conversation-suppress-game-log",
                    FontAwesomeIcon.EyeSlash,
                    "Hide tells from the game chat log",
                    Ccfg.TellRouting == ConversationTellRouting.ChannelsOnly
                        ? "Has no effect while tells go to channels only."
                        : "Tells that land in a conversation are no longer printed by the game.",
                    innerWidth,
                    () => Ccfg.SuppressGameLog,
                    value => Ccfg.SuppressGameLog = value,
                    Ccfg.Color);
            },
            "Routing");

    private void DrawConversationAlertCard() =>
        Card.Draw(
            "conversation-alerts",
            innerWidth =>
            {
                DrawToggleRow(
                    "conversation-play-sound",
                    FontAwesomeIcon.VolumeUp,
                    "Play a sound on a new tell",
                    "Only fires when the conversation is not the tab you are looking at.",
                    innerWidth,
                    () => Ccfg.PlaySound,
                    value => Ccfg.PlaySound = value,
                    UiTheme.TileGreen);

                if (Ccfg.PlaySound)
                {
                    DrawToggleRow(
                        "conversation-custom-sound",
                        FontAwesomeIcon.FileAudio,
                        "Use a sound file",
                        "Off uses one of the sixteen in-game alert sounds instead.",
                        innerWidth,
                        () => Ccfg.UseCustomSound,
                        value => Ccfg.UseCustomSound = value);

                    if (Ccfg.UseCustomSound)
                    {
                        DrawConversationSoundPathRow(innerWidth);

                        DrawPercentRow(
                            "conversation-sound-volume",
                            FontAwesomeIcon.SlidersH,
                            theme.Accent,
                            "Volume",
                            "How loud the sound file is played.",
                            innerWidth,
                            () => Ccfg.CustomSoundVolume,
                            value => Ccfg.CustomSoundVolume = value);
                    }
                    else
                    {
                        DrawIntSliderRow(
                            "conversation-sound-effect",
                            FontAwesomeIcon.Bell,
                            "Game sound",
                            "The same sixteen sounds the game offers for <se.1> through <se.16>.",
                            innerWidth,
                            (int)ChatboxService.MinConversationSound,
                            (int)ChatboxService.MaxConversationSound,
                            ConversationSoundIndex,
                            value => Ccfg.SoundEffect = (uint)value);
                    }

                    DrawActionRow(
                        "conversation-sound-test",
                        FontAwesomeIcon.Play,
                        theme.Accent,
                        "Test the sound",
                        "Plays it once, exactly as an incoming tell would.",
                        "Play",
                        innerWidth,
                        plugin.Chatbox.PreviewConversationSound);
                }

                DrawToggleRow(
                    "conversation-mute-game-sound",
                    FontAwesomeIcon.VolumeMute,
                    "Mute the game's own tell sound",
                    "The game plays its tell sound too. Leave this off and you hear both.",
                    innerWidth,
                    () => Ccfg.MuteGameSound,
                    SetConversationMuteGameSound,
                    UiTheme.TileAmber);

                DrawToggleRow(
                    "conversation-mute-notifications",
                    FontAwesomeIcon.BellSlash,
                    "Mute unread badges and toasts",
                    "Silences Cordi's own notifications for conversations, including the tell sound.",
                    innerWidth,
                    () => Ccfg.MuteNotifications,
                    value => SetConversationChannelFlag(() => Ccfg.MuteNotifications = value),
                    UiTheme.TileAmber);

                DrawToggleRow(
                    "conversation-mention",
                    FontAwesomeIcon.At,
                    "Treat every tell as a mention",
                    "Highlights the whole conversation the way a mention would.",
                    innerWidth,
                    () => Ccfg.TreatAllAsMention,
                    value => SetConversationChannelFlag(() => Ccfg.TreatAllAsMention = value));

                DrawToggleRow(
                    "conversation-flash-taskbar",
                    FontAwesomeIcon.Desktop,
                    "Flash the game in the Windows taskbar",
                    "Only while the game is in the background. Stops as soon as you click back in.",
                    innerWidth,
                    () => Ccfg.FlashTaskbar,
                    value => Ccfg.FlashTaskbar = value,
                    theme.Accent);

                DrawToggleRow(
                    "conversation-focus-window",
                    FontAwesomeIcon.WindowRestore,
                    "Bring the game to the foreground",
                    "Pulls the game window in front of whatever you are doing. Takes over from the taskbar flash.",
                    innerWidth,
                    () => Ccfg.FocusGameWindow,
                    value => Ccfg.FocusGameWindow = value,
                    UiTheme.TileAmber);
            },
            "Alerts");

    private int ConversationSoundIndex() =>
        Math.Clamp(
            (int)Ccfg.SoundEffect,
            (int)ChatboxService.MinConversationSound,
            (int)ChatboxService.MaxConversationSound);

    private void SetConversationMuteGameSound(bool value) =>
        SetConversationChannelFlag(() => Ccfg.MuteGameSound = value);

    private void SetConversationOpenInOwnWindow(bool value)
    {
        Ccfg.OpenInOwnWindow = value;
        Save();
        plugin.ConversationWindows.Sync();
    }

    private void SetConversationChannelFlag(Action apply)
    {
        apply();
        Save();
        plugin.Chatbox.RefreshConversationChannels();
    }

    private void DrawConversationSoundPathRow(float rowWidth) =>
        Row.Draw(
            id: "conversation-sound-path",
            icon: FontAwesomeIcon.Folder,
            iconColor: string.IsNullOrWhiteSpace(Ccfg.CustomSoundPath) ? theme.MutedText : theme.Accent,
            title: "Sound file",
            subtitle: "A .wav or .mp3 file. Empty falls back to Cordi's bundled sound.",
            controlWidth: 300f,
            drawControl: (pos, width) =>
            {
                string value = Ccfg.CustomSoundPath;

                theme.PushInputScope();
                bool changed = theme.PathInput(
                    "##conversation-sound-path-input",
                    pos,
                    width,
                    ref value,
                    () => BrowseForSound(picked =>
                    {
                        Ccfg.CustomSoundPath = picked;
                        Save();
                    }),
                    "Path to a .wav or .mp3 file");
                theme.PopInputScope();

                if (!changed)
                    return;

                Ccfg.CustomSoundPath = value;
                Save();
            },
            rowWidth: rowWidth);

    private void DrawConversationAppearanceCard() =>
        Card.Draw(
            "conversation-appearance",
            innerWidth =>
            {
                DrawToggleRow(
                    "conversation-own-window",
                    FontAwesomeIcon.WindowMaximize,
                    "Every conversation gets its own window",
                    "Off keeps them in the chatbox, where you can still pop out a single person from the list above.",
                    innerWidth,
                    () => Ccfg.OpenInOwnWindow,
                    SetConversationOpenInOwnWindow,
                    UiTheme.TilePurple);

                DrawPercentRow(
                    "conversation-window-opacity",
                    FontAwesomeIcon.Adjust,
                    UiTheme.TilePurple,
                    "Pop-out window background",
                    "Only affects the separate conversation windows, not the chatbox.",
                    innerWidth,
                    () => Ccfg.Window.BackgroundOpacity,
                    value => Ccfg.Window.BackgroundOpacity = value);

                DrawToggleRow(
                    "conversation-window-esc",
                    FontAwesomeIcon.Keyboard,
                    "Escape closes a pop-out window",
                    "Off keeps the window open when you press Escape.",
                    innerWidth,
                    () => !Ccfg.Window.IgnoreEsc,
                    value => Ccfg.Window.IgnoreEsc = !value,
                    UiTheme.TilePurple);

                DrawTextRow(
                    "conversation-section-label",
                    FontAwesomeIcon.Tag,
                    "Section label",
                    "The heading the conversations get in the chatbox channel list.",
                    innerWidth,
                    () => Ccfg.SectionLabel,
                    value => Ccfg.SectionLabel = value,
                    48);

                DrawToggleRow(
                    "conversation-show-world",
                    FontAwesomeIcon.Globe,
                    "Show the world in the tab name",
                    "Off shows just the character name.",
                    innerWidth,
                    () => Ccfg.ShowWorldInLabel,
                    value => SetConversationChannelFlag(() => Ccfg.ShowWorldInLabel = value),
                    Ccfg.Color);

                DrawColorRow(
                    "conversation-color",
                    "Conversation colour",
                    "Used for the tab, the rail tile and the recipient in the input bar.",
                    innerWidth,
                    () => Ccfg.Color,
                    value => SetConversationChannelFlag(() => Ccfg.Color = value));

                DrawToggleRow(
                    "conversation-flash",
                    FontAwesomeIcon.Lightbulb,
                    "Flash the tab while unread",
                    "The tab, the rail tile and the channel entry all pulse until you read it.",
                    innerWidth,
                    () => Ccfg.Flash,
                    value => Ccfg.Flash = value,
                    theme.Accent);

                if (!Ccfg.Flash)
                    return;

                DrawToggleRow(
                    "conversation-no-flashing",
                    FontAwesomeIcon.Eye,
                    "Hold the colour instead of pulsing",
                    "For anyone who would rather not have something blinking on screen.",
                    innerWidth,
                    () => Ccfg.NoFlashing,
                    value => Ccfg.NoFlashing = value,
                    UiTheme.TileAmber);

                if (Ccfg.NoFlashing)
                    return;

                DrawIntSliderRow(
                    "conversation-flash-period",
                    FontAwesomeIcon.Stopwatch,
                    "Pulse length",
                    "One full fade in and out.",
                    innerWidth,
                    100,
                    2000,
                    () => Ccfg.FlashPeriodMs,
                    value => Ccfg.FlashPeriodMs = value,
                    " ms");
            },
            "Appearance");

    private void DrawConversationHistoryCard() =>
        Card.Draw(
            "conversation-history",
            innerWidth =>
            {
                DrawInfoRow(
                    "conversation-history-note",
                    FontAwesomeIcon.InfoCircle,
                    "Conversation history is never pruned",
                    "Retention and message caps do not touch conversations. These two settings only decide how much is in the window at once.",
                    innerWidth);

                DrawIntSliderRow(
                    "conversation-history-window",
                    FontAwesomeIcon.LayerGroup,
                    "Messages shown on open",
                    "How far back a freshly opened conversation starts.",
                    innerWidth,
                    50,
                    2000,
                    () => Ccfg.HistoryWindow,
                    value => Ccfg.HistoryWindow = value,
                    " messages");

                DrawIntSliderRow(
                    "conversation-history-page",
                    FontAwesomeIcon.AngleDoubleUp,
                    "Messages per \"Load older\"",
                    "How much the button at the top of the conversation pulls in each time.",
                    innerWidth,
                    50,
                    2000,
                    () => Ccfg.HistoryPageSize,
                    value => Ccfg.HistoryPageSize = value,
                    " messages");
            },
            "History");
}
