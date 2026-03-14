using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

using Cordi.Core;
using Cordi.UI.Panels;
using Cordi.UI.Themes;

namespace Cordi.UI.Windows;

public class CordiPeepWindow : Window
{
    private readonly CordiPlugin _plugin;
    private readonly CordiPeepPanel _panel;
    private readonly UiTheme _theme = new UiTheme();
    private ImRaii.Color? _opacityScope;
    private ImRaii.Style? _borderScope;

    public CordiPeepWindow(CordiPlugin plugin) : base("Peeper###Cordi Peep", ImGuiWindowFlags.None)
    {
        _plugin = plugin;
        _panel = new CordiPeepPanel(plugin);
        this.SizeConstraints = new WindowSizeConstraints
        {
            // MinimumSize = new Vector2(250, 100),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void PreDraw()
    {
        base.PreDraw();
        var cfg = _plugin.Config.CordiPeep;
        RespectCloseHotkey = !cfg.IgnoreEsc;
        Flags = ImGuiWindowFlags.None;
        if (cfg.WindowLocked) Flags |= ImGuiWindowFlags.NoMove;
        if (cfg.WindowNoResize) Flags |= ImGuiWindowFlags.NoResize;
        if (cfg.HideTitleBar) Flags |= ImGuiWindowFlags.NoTitleBar;

        _theme.PushWindow();

        if (cfg.BackgroundOpacity < 1.0f)
        {
            var bg = _theme.WindowBg;
            bg.W *= cfg.BackgroundOpacity;
            _opacityScope = ImRaii.PushColor(ImGuiCol.WindowBg, bg);
        }

        if (cfg.HideTitleBar)
        {
            _borderScope = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 0f);
        }
    }

    public override void PostDraw()
    {
        _borderScope?.Dispose();
        _borderScope = null;
        _opacityScope?.Dispose();
        _opacityScope = null;
        _theme.PopWindow();
        base.PostDraw();
    }

    public override void Draw()
    {
        _theme.ApplyFontScale();
        if (!_plugin.Config.CordiPeep.WindowEnabled)
        {
            IsOpen = false;
            return;
        }

        _panel.Draw(_plugin.Config.CordiPeep.TextShadow);
    }
}
