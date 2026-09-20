using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

using Cordi.Configuration;
using Cordi.Core;
using Cordi.UI.Panels;

namespace Cordi.UI.Windows;

public class CordiPeepWindow : ThemedWindow
{
    private readonly Configuration.Configuration _config;
    private readonly CordiPeepPanel _panel;

    public CordiPeepWindow(Configuration.Configuration config, Services.CordiPeepService peeper)
        : base("Peeper###Cordi Peep", ImGuiWindowFlags.None)
    {
        _config = config;
        _panel = new CordiPeepPanel(config, peeper, _theme);
        this.SizeConstraints = new WindowSizeConstraints
        {
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    protected override IWindowChromeConfig Chrome => _config.CordiPeep;

    public override void Draw()
    {
        _theme.ApplyFontScale();
        if (!_config.CordiPeep.WindowEnabled)
        {
            IsOpen = false;
            return;
        }

        _panel.Draw(_config.CordiPeep.TextShadow);
    }
}
