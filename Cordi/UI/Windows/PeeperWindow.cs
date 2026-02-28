using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

using Cordi.Core;
using Cordi.UI.Panels;

namespace Cordi.UI.Windows;

public class CordiPeepWindow : Window
{
    private readonly CordiPlugin _plugin;
    private readonly CordiPeepPanel _panel;

    public CordiPeepWindow(CordiPlugin plugin) : base("Peeper###Cordi Peep", ImGuiWindowFlags.None)
    {
        _plugin = plugin;
        _panel = new CordiPeepPanel(plugin);
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(250, 100),
            MaximumSize = new Vector2(400, 1000)
        };
    }

    public override void PreDraw()
    {
        base.PreDraw();
        RespectCloseHotkey = !_plugin.Config.CordiPeep.IgnoreEsc;
        Flags = ImGuiWindowFlags.None;
        if (_plugin.Config.CordiPeep.WindowLocked) Flags |= ImGuiWindowFlags.NoMove;
        if (_plugin.Config.CordiPeep.WindowNoResize) Flags |= ImGuiWindowFlags.NoResize;
    }

    public override void Draw()
    {
        if (!_plugin.Config.CordiPeep.WindowEnabled)
        {
            IsOpen = false;
            return;
        }

        _panel.Draw();
    }
}
