using Cordi.Configuration;
using Cordi.UI.Themes;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class WatchersTab
{
    private void DrawCombinedOverlayPage()
    {
        var config = plugin.Config.CombinedWindow;

        Layout.Draw(
            "Combined Overlay",
            "Shows Peeper and the Emote Log side by side in a single window",
            innerWidth => DrawButtonRow(
                "combined-open-window",
                FontAwesomeIcon.Columns,
                UiTheme.TileBlue,
                "Open the overlay",
                "Brings the combined window up right now",
                "Open Now",
                innerWidth,
                () =>
                {
                    if (plugin.CombinedWindow != null)
                        plugin.CombinedWindow.IsOpen = true;
                }));

        DrawCombinedLayoutPanel(config);
        DrawCombinedWindowPanel(config);
    }

    private void DrawCombinedLayoutPanel(CombinedWindowConfig config)
    {
        Card.Draw(
            "combined-layout",
            innerWidth => DrawToggleRow(
                "combined-swap",
                FontAwesomeIcon.ExchangeAlt,
                theme.Accent,
                "Swap panels",
                config.SwapPanels
                    ? "Peeper on the left, Emote Log on the right"
                    : "Emote Log on the left, Peeper on the right",
                innerWidth,
                () => config.SwapPanels,
                value => config.SwapPanels = value),
            label: "Layout");
    }

    private void DrawCombinedWindowPanel(CombinedWindowConfig config)
    {
        Card.Draw(
            "combined-window",
            innerWidth =>
            {
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
