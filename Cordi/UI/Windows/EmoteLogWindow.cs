using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

using Cordi.Configuration;
using Cordi.Core;
using Cordi.UI.Panels;

namespace Cordi.UI.Windows;

public class EmoteLogWindow : ThemedWindow, IDisposable
{
    private readonly CordiPlugin _plugin;
    private readonly EmoteLogPanel _panel;

    public EmoteLogWindow(CordiPlugin plugin) : base("Emote Log##CordiEmoteLog", ImGuiWindowFlags.None)
    {
        _plugin = plugin;
        _panel = new EmoteLogPanel(plugin);

        this.SizeConstraints = new WindowSizeConstraints
        {
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    protected override IWindowChromeConfig Chrome => _plugin.Config.EmoteLog;

    public override void Draw()
    {
        _theme.ApplyFontScale();
        if (!_plugin.Config.EmoteLog.WindowEnabled)
        {
            IsOpen = false;
            return;
        }

        _panel.Draw(_plugin.Config.EmoteLog.TextShadow);
    }

    public void Dispose()
    {
    }
}
