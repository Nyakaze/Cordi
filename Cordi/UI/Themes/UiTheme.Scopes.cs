using System;
using System.Numerics;
using Cordi.Extensions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Themes;
using Cordi.Configuration;
using Dalamud.Interface.Components;

public sealed partial class UiTheme
{
    private IDisposable? _activeWindowColorScope;
    private IDisposable? _activeWindowStyleScope;

    public void ApplyFontScale(float extraMul = 1f)
    {
        ImGui.SetWindowFontScale(GlobalFontScale * extraMul);
    }

    public void PushWindow()
    {
        _activeWindowColorScope = ImRaii.PushColor(ImGuiCol.WindowBg, WindowBg)
            .Push(ImGuiCol.Border, Border)
            .Push(ImGuiCol.TitleBg, TitleBg)
            .Push(ImGuiCol.TitleBgActive, TitleBgActive)
            .Push(ImGuiCol.FrameBg, FrameBg)
            .Push(ImGuiCol.FrameBgHovered, FrameBgHover)
            .Push(ImGuiCol.FrameBgActive, FrameBgActive)
            .Push(ImGuiCol.PopupBg, CardBg)
            .Push(ImGuiCol.Text, Text)
            .Push(ImGuiCol.TextDisabled, FaintText)
            .Push(ImGuiCol.CheckMark, Accent)
            .Push(ImGuiCol.Button, FrameBg)
            .Push(ImGuiCol.ButtonHovered, FrameBgHover)
            .Push(ImGuiCol.ButtonActive, FrameBgActive)
            .Push(ImGuiCol.ScrollbarBg, new Vector4(0f, 0f, 0f, 0f))
            .Push(ImGuiCol.ScrollbarGrab, FrameBgActive)
            .Push(ImGuiCol.ScrollbarGrabHovered, RowHover)
            .Push(ImGuiCol.ScrollbarGrabActive, Accent)
            .Push(ImGuiCol.DragDropTarget, Accent);
        _activeWindowStyleScope = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, Radius(1.2f))
            .Push(ImGuiStyleVar.WindowBorderSize, 1f * ImGuiHelpers.GlobalScale)
            .Push(ImGuiStyleVar.WindowPadding, new Vector2(PadX(), PadY()))
            .Push(ImGuiStyleVar.FrameRounding, Radius())
            .Push(ImGuiStyleVar.FrameBorderSize, 1f * ImGuiHelpers.GlobalScale)
            .Push(ImGuiStyleVar.FramePadding, new Vector2(PadX(0.8f), PadY(0.6f)))
            .Push(ImGuiStyleVar.PopupRounding, Radius(1.2f))
            .Push(ImGuiStyleVar.ScrollbarRounding, Radius())
            .Push(ImGuiStyleVar.ScrollbarSize, 10f * ImGuiHelpers.GlobalScale)
            .Push(ImGuiStyleVar.GrabRounding, Radius(0.8f));
    }

    public IDisposable PushTitleFlash(Vector4 color) =>
        ImRaii.PushColor(ImGuiCol.TitleBg, color)
            .Push(ImGuiCol.TitleBgActive, color)
            .Push(ImGuiCol.TitleBgCollapsed, color);

    public void PopWindow()
    {
        _activeWindowStyleScope?.Dispose();
        _activeWindowColorScope?.Dispose();
        _activeWindowStyleScope = null;
        _activeWindowColorScope = null;
    }


    private sealed class ActionDisposable : IDisposable
    {
        private readonly Action _onDispose;
        public ActionDisposable(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }

    public IDisposable CardScope(string id, Vector2 minSize = default, bool border = true)
    {
        var avail = ImGui.GetContentRegionAvail();
        if (minSize.X <= 0) minSize.X = avail.X;

        var style = ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, Radius())
            .Push(ImGuiStyleVar.FramePadding, new Vector2(PadX(0.6f), PadY(0.6f)));

        var color = ImRaii.PushColor(ImGuiCol.ChildBg, CardBg);
        if (border)
            color.Push(ImGuiCol.Border, WindowBorder);

        ImGui.BeginChild(id, minSize, border);
        float gap = Gap(0.25f);

        return new ActionDisposable(() =>
        {
            ImGui.EndChild();
            color.Dispose();
            style.Dispose();
            ImGui.Dummy(new Vector2(0, gap));
        });
    }



}
