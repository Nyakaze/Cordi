using System;
using System.Numerics;
using Cordi.Core;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;

namespace Cordi.UI.Tabs;

public class NearbyTab
{
    private readonly CordiPlugin plugin;
    private readonly UiTheme theme = new UiTheme();

    public NearbyTab(CordiPlugin plugin)
    {
        this.plugin = plugin;
    }

    public void Draw()
    {
        ImGui.SetWindowFontScale(1.3f);
        ImGui.TextUnformatted("Nearby Players");
        ImGui.SetWindowFontScale(1f);
        ImGui.TextColored(theme.MutedText, "Tracks distance and direction of all locally rendered characters.");

        ImGui.Dummy(new Vector2(0, theme.Gap(2f)));
        DrawConfigCard();
    }

    private void DrawConfigCard()
    {
        bool enabled = plugin.Config.Nearby.Enabled;

        theme.DrawPluginCardAuto(
            id: "cordi-nearby-general",
            title: "General Settings",
            enabled: ref enabled,
            drawContent: (avail) =>
            {
                ImGui.BeginGroup();
                bool trackingEnabled = plugin.Config.Nearby.Enabled;
                theme.ConfigCheckbox("Enable Nearby Tracking", ref trackingEnabled, () =>
                {
                    plugin.Config.Nearby.Enabled = trackingEnabled;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                bool winEnabled = plugin.Config.Nearby.WindowEnabled;
                theme.ConfigCheckbox("Enable Window", ref winEnabled, () =>
                {
                    plugin.Config.Nearby.WindowEnabled = winEnabled;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                bool openOnLogin = plugin.Config.Nearby.OpenOnLogin;
                theme.ConfigCheckbox("Open on Login", ref openOnLogin, () =>
                {
                    plugin.Config.Nearby.OpenOnLogin = openOnLogin;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                bool ignoreEsc = plugin.Config.Nearby.IgnoreEsc;
                theme.ConfigCheckbox("Ignore ESC Key", ref ignoreEsc, () =>
                {
                    plugin.Config.Nearby.IgnoreEsc = ignoreEsc;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();
                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + theme.Gap(2f));

                ImGui.BeginGroup();
                bool winLocked = plugin.Config.Nearby.WindowLocked;
                theme.ConfigCheckbox("Lock Window", ref winLocked, () =>
                {
                    plugin.Config.Nearby.WindowLocked = winLocked;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                bool winNoResize = plugin.Config.Nearby.WindowNoResize;
                theme.ConfigCheckbox("Disable Resizing", ref winNoResize, () =>
                {
                    plugin.Config.Nearby.WindowNoResize = winNoResize;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                bool includeSelf = plugin.Config.Nearby.IncludeSelf;
                theme.ConfigCheckbox("Include Self", ref includeSelf, () =>
                {
                    plugin.Config.Nearby.IncludeSelf = includeSelf;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();
                ImGui.EndGroup();

                theme.SpacerY(1f);
                ImGui.Separator();
                theme.SpacerY(0.5f);
                ImGui.TextColored(theme.MutedText, "Overlay Display");
                theme.SpacerY(0.5f);

                ImGui.BeginGroup();
                bool showDir = plugin.Config.Nearby.ShowDirection;
                theme.ConfigCheckbox("Show Direction Arrow", ref showDir, () =>
                {
                    plugin.Config.Nearby.ShowDirection = showDir;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                bool showDist = plugin.Config.Nearby.ShowDistance;
                theme.ConfigCheckbox("Show Distance", ref showDist, () =>
                {
                    plugin.Config.Nearby.ShowDistance = showDist;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();
                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + theme.Gap(2f));

                ImGui.BeginGroup();
                bool showTarget = plugin.Config.Nearby.ShowCurrentTarget;
                theme.ConfigCheckbox("Show Player Target", ref showTarget, () =>
                {
                    plugin.Config.Nearby.ShowCurrentTarget = showTarget;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                bool prioritizeTarget = plugin.Config.Nearby.PrioritizeTargetingMe;
                theme.ConfigCheckbox("Prioritize players targeting you", ref prioritizeTarget, () =>
                {
                    plugin.Config.Nearby.PrioritizeTargetingMe = prioritizeTarget;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();
                ImGui.EndGroup();

                theme.SpacerY(0.7f);

                if (theme.SecondaryButton(plugin.NearbyWindow != null && plugin.NearbyWindow.IsOpen ? "Close Window" : "Open Window Now", new Vector2(avail, 28)))
                {
                    if (plugin.NearbyWindow != null)
                        plugin.NearbyWindow.IsOpen = !plugin.NearbyWindow.IsOpen;
                }
                theme.HoverHandIfItem();
            }
        );
    }
}
