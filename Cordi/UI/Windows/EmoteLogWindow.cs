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
    private readonly Configuration.Configuration _config;
    private readonly EmoteLogPanel _panel;

    public EmoteLogWindow(Configuration.Configuration config, Services.Features.EmoteLogService emoteLog)
        : base("Emote Log##CordiEmoteLog", ImGuiWindowFlags.None)
    {
        _config = config;
        _panel = new EmoteLogPanel(config, emoteLog, _theme);

        this.SizeConstraints = new WindowSizeConstraints
        {
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    protected override IWindowChromeConfig Chrome => _config.EmoteLog;

    public override void Draw()
    {
        _theme.ApplyFontScale();
        if (!_config.EmoteLog.WindowEnabled)
        {
            IsOpen = false;
            return;
        }

        _panel.Draw(_config.EmoteLog.TextShadow);
    }

    public void Dispose()
    {
    }
}
