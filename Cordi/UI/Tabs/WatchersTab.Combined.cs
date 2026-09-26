using System;
using Cordi.Configuration;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class WatchersTab
{
    private const string CombinedHelp =
        "The Combined Overlay only draws what Peeper and the Emote Log collect.\n\n"
        + "Everything you set on the Peeper and Emote Log pages - filters, blacklists, colours, "
        + "sounds, Discord alerts - applies inside this window too. There are no separate settings here.\n\n"
        + "If a half stays empty, the matching watcher is switched off.";

    private void DrawCombinedOverlayPage()
    {
        var config = plugin.Config.CombinedWindow;

        Layout.Draw(
            "Combined Overlay",
            "Shows Peeper and the Emote Log together in a single window",
            innerWidth => DrawToggleRow(
                "combined-enabled",
                FontAwesomeIcon.Columns,
                config.Enabled ? UiTheme.TileGreen : theme.MutedText,
                "Combined overlay",
                config.Enabled
                    ? "Available in game through /cordicombo"
                    : "Turned off, the window and /cordicombo are unavailable",
                innerWidth,
                () => config.Enabled,
                value =>
                {
                    config.Enabled = value;
                    plugin.CombinedWindow.IsOpen = value && plugin.CombinedWindow.IsOpen;
                    plugin.UpdateCommandVisibility();
                }));

        DrawCombinedSourcesPanel();
        DrawCombinedLayoutPanel(config);
        DrawCombinedWindowPanel(config);
    }

    private void DrawCombinedSourcesPanel()
    {
        var peeper = plugin.Config.Peeper;
        var emoteLog = plugin.Config.EmoteLog;

        Card.Draw(
            "combined-sources",
            innerWidth =>
            {
                theme.WrappedText(
                    "Both watchers have to be switched on to fill their half of the window. "
                    + "Their own settings apply here as well.",
                    innerWidth,
                    theme.MutedText);

                theme.SpacerY(0.4f);

                DrawToggleRow(
                    "combined-source-emotelog",
                    FontAwesomeIcon.TheaterMasks,
                    emoteLog.Enabled ? UiTheme.TilePurple : theme.MutedText,
                    "Emote Log",
                    emoteLog.Enabled
                        ? "On, emotes aimed at you show up here"
                        : "Off, its half of the window stays empty",
                    innerWidth,
                    () => emoteLog.Enabled,
                    value => emoteLog.Enabled = value);

                theme.SpacerY(0.3f);

                DrawToggleRow(
                    "combined-source-peeper",
                    FontAwesomeIcon.Eye,
                    peeper.Enabled ? UiTheme.TileGreen : theme.MutedText,
                    "Peeper",
                    peeper.Enabled
                        ? "On, players targeting you show up here"
                        : "Off, its half of the window stays empty",
                    innerWidth,
                    () => peeper.Enabled,
                    value => peeper.Enabled = value);
            },
            label: "What this overlay shows",
            drawTrailing: anchor => theme.HelpPill("combined-sources-help", anchor, CombinedHelp));
    }

    private void DrawCombinedLayoutPanel(CombinedWindowConfig config)
    {
        Card.Draw(
            "combined-layout",
            innerWidth =>
            {
                theme.WrappedText(
                    "Drag a tile into any corner, or click one and then click where it should go.",
                    innerWidth,
                    theme.MutedText);

                theme.SpacerY(0.4f);

                var tiles = new[]
                {
                    new SlotTile(
                        "Emote Log",
                        FontAwesomeIcon.TheaterMasks,
                        UiTheme.TilePurple,
                        (int)config.EmoteLogSlot,
                        plugin.Config.EmoteLog.Enabled),
                    new SlotTile(
                        "Peeper",
                        FontAwesomeIcon.Eye,
                        UiTheme.TileGreen,
                        (int)config.PeeperSlot,
                        plugin.Config.Peeper.Enabled),
                };

                if (Slots.Draw("combined-slots", innerWidth, tiles, out var tile, out var slot))
                {
                    MoveCombinedTile(config, tile, (OverlaySlot)slot);
                    Save();
                }

                theme.SpacerY(0.4f);
                theme.WrappedText(DescribeCombinedLayout(config), innerWidth, theme.FaintText);
            },
            label: "Layout");
    }

    private static void MoveCombinedTile(CombinedWindowConfig config, int tile, OverlaySlot target)
    {
        var emote = config.EmoteLogSlot;
        var peep = config.PeeperSlot;

        if (tile == 0)
        {
            config.EmoteLogSlot = target;
            if (peep == target) config.PeeperSlot = emote;
        }
        else
        {
            config.PeeperSlot = target;
            if (emote == target) config.EmoteLogSlot = peep;
        }
    }

    private static string DescribeCombinedLayout(CombinedWindowConfig config)
    {
        var emote = config.EmoteLogSlot;
        var peep = config.PeeperSlot;

        var shape = CombinedWindowConfig.RowOf(emote) == CombinedWindowConfig.RowOf(peep)
            ? "side by side"
            : CombinedWindowConfig.ColumnOf(emote) == CombinedWindowConfig.ColumnOf(peep)
                ? "stacked above each other"
                : "in opposite corners";

        return $"Emote Log {SlotName(emote)}, Peeper {SlotName(peep)} - {shape}.";
    }

    private static string SlotName(OverlaySlot slot) => slot switch
    {
        OverlaySlot.TopLeft => "top left",
        OverlaySlot.TopRight => "top right",
        OverlaySlot.BottomLeft => "bottom left",
        _ => "bottom right",
    };

    private void DrawCombinedWindowPanel(CombinedWindowConfig config)
    {
        Card.Draw(
            "combined-window",
            innerWidth =>
            {
                DrawButtonRow(
                    "combined-open-window",
                    FontAwesomeIcon.WindowMaximize,
                    UiTheme.TileBlue,
                    "Open the overlay",
                    config.Enabled
                        ? "Brings the combined window up right now"
                        : "Switch the overlay on first",
                    "Open Now",
                    innerWidth,
                    () =>
                    {
                        if (config.Enabled)
                            plugin.CombinedWindow.IsOpen = true;
                    });

                theme.SpacerY(0.4f);

                Toggles.Draw("combined-window-flags", new[]
                {
                    Flag("combined-open-login", "Open on login", () => config.OpenOnLogin, v => config.OpenOnLogin = v),
                    Flag("combined-lock-position", "Lock position", () => config.WindowLocked, v => config.WindowLocked = v),
                    Flag("combined-lock-size", "Lock size", () => config.WindowNoResize, v => config.WindowNoResize = v),
                    Flag("combined-ignore-esc", "Ignore ESC", () => config.IgnoreEsc, v => config.IgnoreEsc = v),
                    Flag("combined-hide-title", "Hide title bar", () => config.HideTitleBar, v => config.HideTitleBar = v),
                    Flag("combined-text-shadow", "Text shadow", () => config.TextShadow, v => config.TextShadow = v),
                }, innerWidth);

                theme.SpacerY(0.4f);

                DrawPercentRow(
                    "combined-opacity",
                    FontAwesomeIcon.Adjust,
                    theme.Accent,
                    "Background opacity",
                    "Transparency of the overlay background",
                    innerWidth,
                    () => config.BackgroundOpacity,
                    value => config.BackgroundOpacity = value);
            },
            label: "Window");
    }
}
