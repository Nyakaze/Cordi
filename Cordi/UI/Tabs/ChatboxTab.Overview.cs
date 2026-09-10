using System.Numerics;
using Cordi.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab
{
    public void DrawOverview()
    {
        ConsumeScroll();

        Layout.Draw(
            "Chatbox",
            "A Discord-style chat window inside the game.",
            innerWidth => DrawMasterToggle(innerWidth));

        Card.Draw("chatbox-window", innerWidth =>
        {
            DrawToggleRow(
                "chatbox-hide-titlebar", FontAwesomeIcon.WindowMaximize,
                "Hide Title Bar", "Removes the window frame and its buttons.",
                innerWidth, () => Cfg.HideTitleBar, v => Cfg.HideTitleBar = v);

            DrawToggleRow(
                "chatbox-lock-pos", FontAwesomeIcon.Thumbtack,
                "Lock Position", "The window can no longer be dragged.",
                innerWidth, () => Cfg.WindowLockPosition, v => Cfg.WindowLockPosition = v);

            DrawToggleRow(
                "chatbox-lock-size", FontAwesomeIcon.Expand,
                "Lock Size", "The window can no longer be resized.",
                innerWidth, () => Cfg.WindowLockSize, v => Cfg.WindowLockSize = v);

            DrawToggleRow(
                "chatbox-ignore-esc", FontAwesomeIcon.Keyboard,
                "Ignore ESC", "Escape no longer closes the chatbox.",
                innerWidth, () => Cfg.IgnoreEsc, v => Cfg.IgnoreEsc = v);

            DrawToggleRow(
                "chatbox-clickthrough", FontAwesomeIcon.MousePointer,
                "Click Through when unfocused", "Clicks pass to the game while the window is not focused.",
                innerWidth, () => Cfg.ClickThroughWhenUnfocused, v => Cfg.ClickThroughWhenUnfocused = v);

            DrawSliderRow(
                "chatbox-opacity", FontAwesomeIcon.Adjust,
                "Background Opacity", "How solid the window background is drawn.",
                innerWidth, 0.1f, 1f, 0.01f,
                () => Cfg.BackgroundOpacity, v => Cfg.BackgroundOpacity = v, decimals: 2);
        }, "Window");

        Card.Draw("chatbox-game", innerWidth =>
        {
            DrawToggleRow(
                "chatbox-hide-gamechat", FontAwesomeIcon.CommentSlash,
                "Hide Game Chat", "Hides the vanilla chat log while the chatbox is enabled. It comes back when you turn this off.",
                innerWidth, () => Cfg.HideGameChat, v => Cfg.HideGameChat = v);
        }, "Game Chat");

        Card.Draw("chatbox-input", innerWidth =>
        {
            DrawToggleRow(
                "chatbox-keep-focus", FontAwesomeIcon.Crosshairs,
                "Keep Focus after Send", "The input field stays active so you can keep typing.",
                innerWidth, () => Cfg.KeepFocusAfterSend, v => Cfg.KeepFocusAfterSend = v);

            DrawToggleRow(
                "chatbox-focus-sound", FontAwesomeIcon.VolumeUp,
                "Play Sound on Focus",
                "Plays the game's chat click when the input field gains focus, like the vanilla chat log.",
                innerWidth, () => Cfg.PlaySoundOnInputFocus, v => Cfg.PlaySoundOnInputFocus = v);

            DrawToggleRow(
                "chatbox-split-messages", FontAwesomeIcon.Cut,
                "Split long Messages",
                $"Messages longer than {ChatboxConfig.MaxMessageLength} characters are sent in several parts. Off cuts them off instead.",
                innerWidth, () => Cfg.SplitLongMessages, v => Cfg.SplitLongMessages = v);

            DrawToggleRow(
                "chatbox-warn-missing-slash", FontAwesomeIcon.ExclamationTriangle,
                "Warn about Commands without Slash",
                "In Say, Shout and Yell, asks for confirmation when the first word matches a plugin command but the leading slash is missing. Game commands and emotes are ignored.",
                innerWidth, () => Cfg.WarnOnMissingSlash, v => Cfg.WarnOnMissingSlash = v);
        }, "Input");
    }

    private void DrawMasterToggle(float innerWidth)
    {
        bool enabled = Cfg.Enabled;

        var result = Row.Draw(
            id: "chatbox-master",
            icon: FontAwesomeIcon.CommentAlt,
            iconColor: enabled ? Themes.UiTheme.TileGreen : theme.MutedText,
            title: enabled ? "Chatbox is running" : "Chatbox is off",
            subtitle: "Opens the chat window and starts capturing messages. Command: /cordichat",
            toggleValue: enabled,
            onToggle: SetChatboxEnabled,
            rowWidth: innerWidth);

        if (result.RowClicked && !result.ToggleChanged)
            SetChatboxEnabled(!enabled);

        DrawToggleRow(
            "chatbox-open-login", FontAwesomeIcon.SignInAlt,
            "Open on Login", "Shows the chatbox automatically once you log in.",
            innerWidth, () => Cfg.OpenOnLogin, v => Cfg.OpenOnLogin = v);

        DrawToggleRow(
            "chatbox-hide-logged-out", FontAwesomeIcon.EyeSlash,
            "Hide when not logged in", "Keeps the chatbox out of the way on the title screen.",
            innerWidth, () => Cfg.HideWhenNotLoggedIn, v => Cfg.HideWhenNotLoggedIn = v);
    }

    private void SetChatboxEnabled(bool value)
    {
        Cfg.Enabled = value;
        Save();
        plugin.UpdateCommandVisibility();
        plugin.ChatboxWindow.IsOpen = value;
    }
}
