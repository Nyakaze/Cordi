using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

using Cordi.Core;
using Cordi.UI.Panels;
using Cordi.UI.Themes;

namespace Cordi.UI.Windows;

public class NearbyWindow : Window
{
    private readonly CordiPlugin _plugin;
    private readonly NearbyPanel _panel;
    private readonly UiTheme _theme = new UiTheme();

    public NearbyWindow(CordiPlugin plugin) : base("Nearby Players###Cordi Nearby", ImGuiWindowFlags.None)
    {
        _plugin = plugin;
        _panel = new NearbyPanel(plugin);
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(250, 100),
            MaximumSize = new Vector2(2000, 2000)
        };
    }

    public override void PreDraw()
    {
        base.PreDraw();
        RespectCloseHotkey = !_plugin.Config.Nearby.IgnoreEsc;
        Flags = ImGuiWindowFlags.None;
        if (_plugin.Config.Nearby.WindowLocked) Flags |= ImGuiWindowFlags.NoMove;
        if (_plugin.Config.Nearby.WindowNoResize) Flags |= ImGuiWindowFlags.NoResize;

        _theme.PushWindow();
    }

    public override void PostDraw()
    {
        _theme.PopWindow();
        base.PostDraw();
    }

    public override void Draw()
    {
        if (!_plugin.Config.Nearby.WindowEnabled)
        {
            IsOpen = false;
            return;
        }

        _panel.Draw();
    }
}
