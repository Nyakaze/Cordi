using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

using Cordi.Configuration;
using Cordi.Core;
using Cordi.UI.Panels;

namespace Cordi.UI.Windows;

public class CordiPeepWindow : ThemedWindow
{
    private readonly CordiPlugin _plugin;
    private readonly CordiPeepPanel _panel;

    public CordiPeepWindow(CordiPlugin plugin) : base("Peeper###Cordi Peep", ImGuiWindowFlags.None)
    {
        _plugin = plugin;
        _panel = new CordiPeepPanel(plugin, _theme);
        this.SizeConstraints = new WindowSizeConstraints
        {
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    protected override IWindowChromeConfig Chrome => _plugin.Config.CordiPeep;

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
