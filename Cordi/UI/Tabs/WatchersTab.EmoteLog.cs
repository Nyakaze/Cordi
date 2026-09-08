using Cordi.Configuration;
using Cordi.UI.Themes;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class WatchersTab
{
    private void DrawEmoteLogPage()
    {
        var config = plugin.Config.EmoteLog;

        Layout.Draw(
            "Emote Log",
            "Records emotes performed around you and mirrors them to Discord",
            innerWidth => DrawToggleRow(
                "emote-enabled",
                FontAwesomeIcon.TheaterMasks,
                config.Enabled ? UiTheme.TileGreen : theme.MutedText,
                "Emote logging",
                config.Enabled ? "Capturing nearby emotes" : "Logging is off",
                innerWidth,
                () => config.Enabled,
                value => config.Enabled = value));

        DrawEmoteLogBehaviorPanel(config);
        DrawEmoteLogDiscordPanel(config);
        DrawEmoteLogOverlayPanel(config);
        DrawEmoteLogBlacklistPanel(config);
    }

    private void DrawEmoteLogBehaviorPanel(EmoteLogConfig config)
    {
        Card.Draw(
            "emote-behavior",
            innerWidth =>
            {
                DrawToggleRow(
                    "emote-detect-closed",
                    FontAwesomeIcon.EyeSlash,
                    theme.Accent,
                    "Detect while the overlay is closed",
                    "Keeps logging even when the Emote Log window is hidden",
                    innerWidth,
                    () => config.DetectWhenClosed,
                    value => config.DetectWhenClosed = value);

                theme.SpacerY(0.4f);

                Toggles.Draw("emote-behavior-flags", new[]
                {
                    Flag("emote-include-self", "Include yourself", () => config.IncludeSelf, v => config.IncludeSelf = v),
                    Flag("emote-collapse", "Collapse duplicates", () => config.CollapseDuplicates, v => config.CollapseDuplicates = v),
                }, innerWidth);
            },
            label: "Behavior");
    }

    private void DrawEmoteLogDiscordPanel(EmoteLogConfig config)
    {
        Card.Draw(
            "emote-discord",
            innerWidth =>
            {
                DrawToggleRow(
                    "emote-discord-enabled",
                    FontAwesomeIcon.Bell,
                    theme.Accent,
                    "Discord notifications",
                    "Posts logged emotes to a Discord channel",
                    innerWidth,
                    () => config.DiscordEnabled,
                    value => config.DiscordEnabled = value);

                DrawChannelRow(
                    "emoteLogChannel",
                    "Notification channel",
                    config.ChannelId,
                    innerWidth,
                    newId =>
                    {
                        config.ChannelId = newId;
                        Save();
                    });
            },
            label: "Discord Notifications");
    }

    private void DrawEmoteLogOverlayPanel(EmoteLogConfig config) =>
        DrawOverlayPanel(
            "emote",
            "Emote Log",
            () => plugin.EmoteLogWindow,
            () => config.WindowEnabled,
            value => config.WindowEnabled = value,
            () => new[]
            {
                Flag("emote-open-login", "Open on login", () => config.WindowOpenOnLogin, v => config.WindowOpenOnLogin = v),
                Flag("emote-lock-position", "Lock position", () => config.WindowLockPosition, v => config.WindowLockPosition = v),
                Flag("emote-lock-size", "Lock size", () => config.WindowLockSize, v => config.WindowLockSize = v),
                Flag("emote-ignore-esc", "Ignore ESC", () => config.IgnoreEsc, v => config.IgnoreEsc = v),
                Flag("emote-reply-button", "Show reply button", () => config.ShowReplyButton, v => config.ShowReplyButton = v),
                Flag("emote-hide-title", "Hide title bar", () => config.HideTitleBar, v => config.HideTitleBar = v),
                Flag("emote-text-shadow", "Text shadow", () => config.TextShadow, v => config.TextShadow = v),
            },
            () => config.BackgroundOpacity,
            value => config.BackgroundOpacity = value);

    private void DrawEmoteLogBlacklistPanel(EmoteLogConfig config)
    {
        DrawBlacklistPanel(
            id: "emote-blacklist",
            label: "Blacklist",
            emptyHint: "Blacklisted characters are never logged or forwarded to Discord.",
            list: config.Blacklist,
            createEntry: (name, world) => new EmoteLogBlacklistEntry { Name = name, World = world });
    }
}
