using System;
using Cordi.Configuration;
using Cordi.Services.Chatbox;
using Cordi.UI.Themes;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab
{
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

        DrawConversationRoutingCard();
        DrawConversationAlertCard();
        DrawConversationAppearanceCard();
        DrawConversationHistoryCard();
        DrawConversationListCard();

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
