using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

using Cordi.Configuration;
using Cordi.Core;
using Cordi.UI.Panels;

namespace Cordi.UI.Windows;

public class PeeperWindow : ThemedWindow
{
    private readonly Configuration.Configuration _config;
    private readonly PeeperPanel _panel;

    public PeeperWindow(Configuration.Configuration config, Services.Features.PeeperService peeper)
        : base("Peeper###Cordi Peep", ImGuiWindowFlags.None)
    {
        _config = config;
        _panel = new PeeperPanel(config, peeper, _theme);
        this.SizeConstraints = new WindowSizeConstraints
        {
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    protected override IWindowChromeConfig Chrome => _config.Peeper;

    public override void Draw()
    {
        _theme.ApplyFontScale();
        if (!_config.Peeper.WindowEnabled)
        {
            IsOpen = false;
            return;
        }

        _panel.Draw(_config.Peeper.TextShadow);
    }
}
