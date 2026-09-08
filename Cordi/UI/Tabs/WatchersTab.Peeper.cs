using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.Configuration;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class WatchersTab
{
    private void DrawPeeperPage()
    {
        var config = plugin.Config.CordiPeep;

        Layout.Draw(
            "Peeper",
            "Alerts you when another player targets your character",
            innerWidth => DrawPeeperStatusRow(config, innerWidth));

        DrawPeeperDetectionPanel(config);
        DrawPeeperDiscordPanel(config);
        DrawPeeperAudioPanel(config);
        DrawPeeperOverlayPanel(config);
        DrawPeeperDisplayPanel(config);
        DrawPeeperHighlightPanel(config);
        DrawPeeperDotPanel(config);
        DrawPeeperBlacklistPanel(config);
    }

    private void DrawPeeperStatusRow(CordiPeepConfig config, float rowWidth)
    {
        DrawToggleRow(
            "peep-enabled",
            FontAwesomeIcon.Eye,
            config.Enabled ? UiTheme.TileGreen : theme.MutedText,
            "Peeper detection",
            config.Enabled ? "Watching for players targeting you" : "Detection is off",
            rowWidth,
            () => config.Enabled,
            value => config.Enabled = value);
    }

    private void DrawPeeperDetectionPanel(CordiPeepConfig config)
    {
        Card.Draw(
            "peep-detection",
            innerWidth =>
            {
                DrawToggleRow(
                    "peep-detect-closed",
                    FontAwesomeIcon.EyeSlash,
                    theme.Accent,
                    "Detect while the overlay is closed",
                    "Keeps tracking even when the Peeper window is hidden",
                    innerWidth,
                    () => config.DetectWhenClosed,
                    value => config.DetectWhenClosed = value);

                theme.SpacerY(0.4f);

                Toggles.Draw("peep-filters", new[]
                {
                    Flag("log-party", "Log party members", () => config.LogParty, v => config.LogParty = v),
                    Flag("log-alliance", "Log alliance members", () => config.LogAlliance, v => config.LogAlliance = v),
                    Flag("log-combat", "Combat targeters only", () => config.LogCombat, v => config.LogCombat = v),
                    Flag("include-self", "Include yourself", () => config.IncludeSelf, v => config.IncludeSelf = v),
                }, innerWidth);

                theme.SpacerY(0.4f);

                DrawToggleRow(
                    "peep-skip-repeated",
                    FontAwesomeIcon.Redo,
                    theme.Accent,
                    "Skip repeated alerts",
                    "Suppresses further alerts from the same player for a while",
                    innerWidth,
                    () => config.SkipRepeatedNotifications,
                    value => config.SkipRepeatedNotifications = value);

                if (!config.SkipRepeatedNotifications)
                    return;

                Row.Draw(
                    id: "peep-cooldown",
                    icon: FontAwesomeIcon.Stopwatch,
                    iconColor: theme.Accent,
                    title: "Repeat cooldown",
                    subtitle: "Seconds before the same player can alert again",
                    controlWidth: 150f,
                    drawControl: (pos, width) =>
                    {
                        int cooldown = config.RepeatedNotificationsCooldown;
                        if (theme.NumberInput("##peep-cooldown", pos, width, ref cooldown, 5, 3600))
                        {
                            config.RepeatedNotificationsCooldown = cooldown;
                            Save();
                        }
                    },
                    rowWidth: innerWidth);
            },
            label: "Detection",
            drawTrailing: anchor => theme.HelpPill(
                "peep-detection-help",
                anchor,
                "Peeper watches the object table for players whose target is you.\n" +
                "Filters decide which of those are worth reporting."));
    }

    private void DrawPeeperDiscordPanel(CordiPeepConfig config)
    {
        Card.Draw(
            "peep-discord",
            innerWidth =>
            {
                DrawToggleRow(
                    "peep-discord-enabled",
                    FontAwesomeIcon.Bell,
                    theme.Accent,
                    "Discord notifications",
                    "Posts an embed when someone targets you",
                    innerWidth,
                    () => config.DiscordEnabled,
                    value => config.DiscordEnabled = value);

                DrawChannelRow(
                    "peepChannel",
                    "Notification channel",
                    config.DiscordChannelId,
                    innerWidth,
                    newId =>
                    {
                        config.DiscordChannelId = newId;
                        Save();
                    });

                theme.SpacerY(0.4f);

                Toggles.Draw("peep-discord-flags", new[]
                {
                    Flag("discord-combat", "Mute in combat", () => config.DisableDiscordInCombat, v => config.DisableDiscordInCombat = v),
                    Flag("discord-pvp", "Mute in PvP", () => config.DisableDiscordInPvP, v => config.DisableDiscordInPvP = v),
                }, innerWidth);
            },
            label: "Discord Notifications");
    }

    private void DrawPeeperAudioPanel(CordiPeepConfig config)
    {
        Card.Draw(
            "peep-audio",
            innerWidth =>
            {
                DrawToggleRow(
                    "peep-sound-enabled",
                    FontAwesomeIcon.VolumeUp,
                    theme.Accent,
                    "Sound alert",
                    "Plays a sound when a new peeper is detected",
                    innerWidth,
                    () => config.SoundEnabled,
                    value => config.SoundEnabled = value);

                theme.SpacerY(0.4f);

                Toggles.Draw("peep-sound-flags", new[]
                {
                    Flag("sound-combat", "Mute in combat", () => config.DisableSoundInCombat, v => config.DisableSoundInCombat = v),
                    Flag("sound-pvp", "Mute in PvP", () => config.DisableSoundInPvP, v => config.DisableSoundInPvP = v),
                }, innerWidth);

                theme.SpacerY(0.4f);

                Row.Draw(
                    id: "peep-sound-path",
                    icon: FontAwesomeIcon.FileAudio,
                    iconColor: theme.Accent,
                    title: "Alert sound",
                    subtitle: string.IsNullOrWhiteSpace(config.SoundPath)
                        ? "Using the built-in alert"
                        : config.SoundPath,
                    controlWidth: 300f,
                    drawControl: (pos, width) =>
                    {
                        string path = config.SoundPath;
                        if (theme.PathInput(
                                "##peep-sound-path",
                                pos,
                                width,
                                ref path,
                                () => BrowseForSound(selected =>
                                {
                                    config.SoundPath = selected;
                                    Save();
                                }),
                                "Built-in alert"))
                        {
                            config.SoundPath = path;
                            Save();
                        }
                    },
                    rowWidth: innerWidth);

                DrawSliderRow(
                    "peep-volume",
                    FontAwesomeIcon.SlidersH,
                    theme.Accent,
                    "Volume",
                    $"Plays through {plugin.Audio.DescribeConfiguredDevice()}",
                    innerWidth,
                    0f, 100f, 1f,
                    () => config.SoundVolume * 100f,
                    value => config.SoundVolume = value / 100f,
                    suffix: "%");

                DrawButtonRow(
                    "peep-sound-test",
                    FontAwesomeIcon.Play,
                    UiTheme.TileGreen,
                    "Test alert",
                    "Plays the configured sound at the current volume",
                    "Play",
                    innerWidth,
                    () => plugin.Audio.Play(config.SoundPath, config.SoundVolume));
            },
            label: "Audio",
            drawTrailing: anchor => theme.HelpPill(
                "peep-audio-help",
                anchor,
                "Supported formats: .wav and .mp3\n" +
                "The output device is shared by the whole plugin and\n" +
                "lives under Settings > Audio."));
    }

    private void DrawPeeperOverlayPanel(CordiPeepConfig config) =>
        DrawOverlayPanel(
            "peep",
            "Peeper",
            () => plugin.CordiPeepWindow,
            () => config.WindowEnabled,
            value => config.WindowEnabled = value,
            () => new[]
            {
                Flag("open-login", "Open on login", () => config.OpenOnLogin, v => config.OpenOnLogin = v),
                Flag("lock-position", "Lock position", () => config.WindowLocked, v => config.WindowLocked = v),
                Flag("lock-size", "Lock size", () => config.WindowNoResize, v => config.WindowNoResize = v),
                Flag("ignore-esc", "Ignore ESC", () => config.IgnoreEsc, v => config.IgnoreEsc = v),
                Flag("focus-hover", "Focus on hover", () => config.FocusOnHover, v => config.FocusOnHover = v),
                Flag("alt-examine", "Alt-click examine", () => config.AltClickExamine, v => config.AltClickExamine = v),
                Flag("hide-title", "Hide title bar", () => config.HideTitleBar, v => config.HideTitleBar = v),
                Flag("text-shadow", "Text shadow", () => config.TextShadow, v => config.TextShadow = v),
            },
            () => config.BackgroundOpacity,
            value => config.BackgroundOpacity = value);

    private void DrawPeeperDisplayPanel(CordiPeepConfig config)
    {
        Card.Draw(
            "peep-display",
            innerWidth => Toggles.Draw("peep-display-flags", new[]
            {
                Flag("show-direction", "Show direction arrow", () => config.ShowDirection, v => config.ShowDirection = v),
                Flag("show-distance", "Show distance", () => config.ShowDistance, v => config.ShowDistance = v),
                Flag("show-target", "Show peeper's target", () => config.ShowCurrentTarget, v => config.ShowCurrentTarget = v),
                Flag("history-direction", "Direction in history", () => config.ShowDirectionInHistory, v => config.ShowDirectionInHistory = v),
                Flag("history-distance", "Distance in history", () => config.ShowDistanceInHistory, v => config.ShowDistanceInHistory = v),
            }, innerWidth),
            label: "Overlay Display");
    }

    private void DrawPeeperHighlightPanel(CordiPeepConfig config)
    {
        Card.Draw(
            "peep-highlight",
            innerWidth =>
            {
                DrawColorRow(
                    "peep-highlight-color",
                    "Targeting highlight",
                    "Tint applied to players currently targeting you",
                    innerWidth,
                    () => config.TargetingHighlightColor,
                    value => config.TargetingHighlightColor = value);

                DrawToggleRow(
                    "peep-glow-enabled",
                    FontAwesomeIcon.Certificate,
                    theme.Accent,
                    "Outline glow",
                    "Draws a glowing outline around targeting players",
                    innerWidth,
                    () => config.TargetingGlowEnabled,
                    value => config.TargetingGlowEnabled = value);

                if (!config.TargetingGlowEnabled)
                    return;

                DrawColorRow(
                    "peep-glow-color",
                    "Glow color",
                    "Color of the outline glow",
                    innerWidth,
                    () => config.TargetingGlowColor,
                    value => config.TargetingGlowColor = value);

                DrawSliderRow(
                    "peep-glow-thickness",
                    FontAwesomeIcon.Ruler,
                    theme.Accent,
                    "Glow thickness",
                    "Width of the outline in pixels",
                    innerWidth,
                    1f, 10f, 0.5f,
                    () => config.TargetingGlowThickness,
                    value => config.TargetingGlowThickness = value,
                    decimals: 1,
                    suffix: " px");
            },
            label: "Highlight & Glow");
    }

    private void DrawPeeperDotPanel(CordiPeepConfig config)
    {
        Card.Draw(
            "peep-dot",
            innerWidth =>
            {
                DrawToggleRow(
                    "peep-dot-enabled",
                    FontAwesomeIcon.Circle,
                    theme.Accent,
                    "Targeting dot",
                    "Marks targeting players with a dot above their head",
                    innerWidth,
                    () => config.ShowTargetingDot,
                    value => config.ShowTargetingDot = value);

                if (!config.ShowTargetingDot)
                    return;

                DrawColorRow(
                    "peep-dot-color",
                    "Dot color",
                    "Color of the targeting dot",
                    innerWidth,
                    () => config.TargetingDotColor,
                    value => config.TargetingDotColor = value);

                DrawSliderRow(
                    "peep-dot-size",
                    FontAwesomeIcon.Expand,
                    theme.Accent,
                    "Dot size",
                    "Radius of the dot in pixels",
                    innerWidth,
                    2f, 20f, 0.5f,
                    () => config.TargetingDotSize,
                    value => config.TargetingDotSize = value,
                    decimals: 1,
                    suffix: " px");

                DrawSliderRow(
                    "peep-dot-offset",
                    FontAwesomeIcon.ArrowsAltV,
                    theme.Accent,
                    "Vertical offset",
                    "Moves the dot up or down relative to the head",
                    innerWidth,
                    -2.5f, 2.5f, 0.1f,
                    () => config.TargetingDotYOffset,
                    value => config.TargetingDotYOffset = value,
                    decimals: 1);
            },
            label: "Targeting Dot");
    }

    private void DrawPeeperBlacklistPanel(CordiPeepConfig config)
    {
        DrawBlacklistPanel(
            id: "peep-blacklist",
            label: "Blacklist",
            emptyHint: "Blacklisted characters never trigger Peeper alerts.",
            list: config.Blacklist,
            createEntry: (name, world) => new CordiPeepBlacklistEntry { Name = name, World = world },
            extraColumns: new[]
            {
                new BlacklistFlagColumn<CordiPeepBlacklistEntry>
                {
                    Id = "no-sound",
                    Tooltip = "No sound alert",
                    Icon = FontAwesomeIcon.VolumeMute,
                    Get = entry => entry.DisableSound,
                    Set = (entry, value) => entry.DisableSound = value,
                },
            });
    }

    private static void BrowseForSound(Action<string> onPicked)
    {
        var thread = new System.Threading.Thread(() =>
        {
            using var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Audio Files|*.wav;*.mp3|All files|*.*",
                CheckFileExists = true,
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                onPicked(dialog.FileName);
        });

        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
    }
}
