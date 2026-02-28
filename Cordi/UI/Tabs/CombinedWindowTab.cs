using System.Numerics;
using Dalamud.Bindings.ImGui;

using Cordi.Core;
using Cordi.UI.Themes;

namespace Cordi.UI.Tabs;

public class CombinedWindowTab : ConfigTabBase
{
    public override string Label => "Combined Win";

    public CombinedWindowTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
    }

    public override void Draw()
    {
        var cfg = plugin.Config.CombinedWindow;

        bool enabled = true;
        theme.DrawPluginCardAuto(
            id: "combined-window",
            enabled: ref enabled,
            showCheckbox: false,
            title: "Combined Window",
            drawContent: (avail) =>
        {
            var swap = cfg.SwapPanels;
            if (ImGui.Checkbox("Swap Panels (Peeper links, Emote Log rechts)##comboSwap", ref swap))
            {
                cfg.SwapPanels = swap;
                plugin.Config.Save();
            }

            var openOnLogin = cfg.OpenOnLogin;
            if (ImGui.Checkbox("Open on Login##comboLogin", ref openOnLogin))
            {
                cfg.OpenOnLogin = openOnLogin;
                plugin.Config.Save();
            }

            var locked = cfg.WindowLocked;
            if (ImGui.Checkbox("Lock Position##comboLock", ref locked))
            {
                cfg.WindowLocked = locked;
                plugin.Config.Save();
            }

            var noResize = cfg.WindowNoResize;
            if (ImGui.Checkbox("Lock Size##comboResize", ref noResize))
            {
                cfg.WindowNoResize = noResize;
                plugin.Config.Save();
            }

            var ignoreEsc = cfg.IgnoreEsc;
            if (ImGui.Checkbox("Ignore ESC##comboEsc", ref ignoreEsc))
            {
                cfg.IgnoreEsc = ignoreEsc;
                plugin.Config.Save();
            }
        });

        ImGui.TextDisabled("Default: Emote Log links, Peeper rechts.");
        ImGui.TextDisabled("Command: /cordicombo");
    }
}
