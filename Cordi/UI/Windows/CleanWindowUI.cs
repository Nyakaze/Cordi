using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace Cordi.UI.Windows;

public unsafe class CleanWindowUI : Window
{
    private bool _pushedStyleVar;

    public CleanWindowUI() : base("Cordi — Clean View", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(256, 144),
            MaximumSize = new Vector2(3840, 2160)
        };
        RespectCloseHotkey = false;
    }

    private static readonly ImDrawCallback _dummyCallback = (parent_list, cmd) => { };

    public override void PreDraw()
    {
        var size = Core.CordiPlugin.Plugin.CleanWindowService.GetViewSize();
        var movable = Core.CordiPlugin.Plugin.Config.CleanWindow.MovableWindow;

        _pushedStyleVar = false;
        if (!movable && size.X > 0)
        {
            Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar;
            Size = size;
            SizeCondition = ImGuiCond.Always;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            _pushedStyleVar = true;
        }
        else
        {
            Flags &= ~(ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar);
            Size = null;
            SizeCondition = ImGuiCond.Appearing;
        }
    }

    public override void PostDraw()
    {
        if (_pushedStyleVar)
        {
            ImGui.PopStyleVar();
        }
    }

    public override void Draw()
    {
        var srv = Core.CordiPlugin.Plugin.CleanWindowService.GetShaderResourceView();
        var size = Core.CordiPlugin.Plugin.CleanWindowService.GetViewSize();

        if (srv != nint.Zero && size.X > 0)
        {
            unsafe
            {
                delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void> cbOpaque = &Services.CleanWindow.CleanWindowService.OpaqueBlendCallback;
                delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void> cbRestore = &Services.CleanWindow.CleanWindowService.RestoreBlendCallback;

                var drawList = ImGui.GetWindowDrawList();

                // Add Opaque Blend callback. We pass a dummy delegate to ensure ImGui pushes the command properly.
                drawList.AddCallback(_dummyCallback, (void*)IntPtr.Zero);
                var cmdBufferPtr = (ImDrawCmd*)drawList.CmdBuffer.Data;
                // AddCallback pushes the callback command, and then immediately pushes an empty geometry command.
                // Therefore, the callback command is at Size - 2, and Size - 1 is the new geometry command.
                cmdBufferPtr[drawList.CmdBuffer.Size - 2].UserCallback = (void*)cbOpaque;

                // Draw the actual image
                ImGui.Image(new ImTextureID(srv), size);

                // Add Restore Blend callback
                drawList.AddCallback(_dummyCallback, (void*)IntPtr.Zero);
                cmdBufferPtr = (ImDrawCmd*)drawList.CmdBuffer.Data;
                cmdBufferPtr[drawList.CmdBuffer.Size - 2].UserCallback = (void*)cbRestore;
            }
        }
        else
        {
            ImGui.Text("Waiting for GPU frame... Have you enabled the hook in General Settings?");
        }
    }
}
