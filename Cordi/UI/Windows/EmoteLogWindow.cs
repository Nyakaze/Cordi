using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

using Cordi.Core;
using Cordi.UI.Panels;

namespace Cordi.UI.Windows;

public class EmoteLogWindow : Window, IDisposable
{
    private readonly CordiPlugin _plugin;
    private readonly EmoteLogPanel _panel;

    public EmoteLogWindow(CordiPlugin plugin) : base("Emote Log##CordiEmoteLog", ImGuiWindowFlags.None)
    {
        _plugin = plugin;
        _panel = new EmoteLogPanel(plugin);

        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(250, 100),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void Draw()
    {
        if (!_plugin.Config.EmoteLog.WindowEnabled)
        {
            IsOpen = false;
            return;
        }

        _panel.Draw();
    }

    public override void PreDraw()
    {
        RespectCloseHotkey = !_plugin.Config.EmoteLog.IgnoreEsc;

        if (_plugin.Config.EmoteLog.WindowLockPosition)
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }

        if (_plugin.Config.EmoteLog.WindowLockSize)
        {
            Flags |= ImGuiWindowFlags.NoResize;
        }
        else
        {
            Flags &= ~ImGuiWindowFlags.NoResize;
        }
    }

    public void Dispose()
    {
    }
}
