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
    public bool PrimaryButton(string label, Vector2 size = default)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Button, Accent)
            .Push(ImGuiCol.ButtonHovered, Lerp(Accent, Vector4.One, 0.08f))
            .Push(ImGuiCol.ButtonActive, Lerp(Accent, Vector4.Zero, 0.10f));
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, Radius());
        var clicked = Button(label, size);
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return clicked;
    }

    public bool SecondaryButton(string label, Vector2 size = default)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Button, FrameBg)
            .Push(ImGuiCol.ButtonHovered, FrameBgHover)
            .Push(ImGuiCol.ButtonActive, FrameBgActive);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, Radius());
        var clicked = Button(label, size);
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return clicked;
    }

    public bool Button(string label, Vector2 size = default)
    {
        var ret = ImGui.Button(label, size);
        HoverHandIfItem();
        return ret;
    }
    public bool Button(string label, string tooltip, Vector2 size = default)
    {
        var ret = ImGui.Button(label, size);
        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }
        HoverHandIfItem();
        return ret;
    }

    public bool Checkbox(string label, ref bool v)
    {
        var ret = ImGui.Checkbox(label, ref v);
        HoverHandIfItem();
        return ret;
    }
    public bool Checkbox(string label, string tooltip, ref bool v)
    {
        var ret = ImGui.Checkbox(label, ref v);
        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }
        HoverHandIfItem();
        return ret;
    }

    public bool InvisibleButton(string id, Vector2 size = default)
    {
        var ret = ImGui.InvisibleButton(id, size);
        HoverHandIfItem();
        return ret;
    }

    public bool EnumCombo<T>(string id, ref T value) where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        int currentIndex = Array.IndexOf(values, value);
        if (currentIndex < 0) currentIndex = 0;

        string[] names = Enum.GetNames<T>();

        bool changed = false;
        if (ImGui.Combo(id, ref currentIndex, names, names.Length))
        {
            value = values[currentIndex];
            changed = true;
        }
        HoverHandIfItem();
        return changed;
    }

    public bool EnumCombo<T>(string id, string tooltip, ref T value) where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        int currentIndex = Array.IndexOf(values, value);
        if (currentIndex < 0) currentIndex = 0;

        string[] names = Enum.GetNames<T>();

        bool changed = false;
        if (ImGui.Combo(id, ref currentIndex, names, names.Length))
        {
            value = values[currentIndex];
            changed = true;
        }
        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }
        HoverHandIfItem();
        return changed;
    }

    public bool IconButton(string id, FontAwesomeIcon icon, string tooltip = "")
    {
        bool clicked = ImGuiComponents.IconButton(id, icon);

        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }
        HoverHandIfItem();

        return clicked;
    }

    public bool SuccessIconButton(string id, FontAwesomeIcon icon, string tooltip = "")
    {
        using var color = ImRaii.PushColor(ImGuiCol.Button, Accent)
            .Push(ImGuiCol.ButtonHovered, Lerp(Accent, Vector4.One, 0.08f))
            .Push(ImGuiCol.ButtonActive, Lerp(Accent, Vector4.Zero, 0.10f));
        var clicked = IconButton(id, icon, tooltip);
        return clicked;
    }

    public bool SecondaryIconButton(string id, FontAwesomeIcon icon, string tooltip = "")
    {
        using var color = ImRaii.PushColor(ImGuiCol.Button, FrameBg)
            .Push(ImGuiCol.ButtonHovered, FrameBgHover)
            .Push(ImGuiCol.ButtonActive, FrameBgActive);
        var clicked = IconButton(id, icon, tooltip);
        return clicked;
    }

    public bool DangerIconButton(string id, FontAwesomeIcon icon, string tooltip = "")
    {
        using var color = ImRaii.PushColor(ImGuiCol.Button, ColorDanger)
            .Push(ImGuiCol.ButtonHovered, Lerp(ColorDanger, Vector4.One, 0.08f))
            .Push(ImGuiCol.ButtonActive, Lerp(ColorDanger, Vector4.Zero, 0.10f));
        var clicked = IconButton(id, icon, tooltip);
        return clicked;
    }

    public bool IconToggleButton(string id, ref bool state, float size = 28f)
    {
        float scale = ImGuiHelpers.GlobalScale;
        var s = new Vector2(size * scale, size * scale);


        var colText = new Vector4(1f, 1f, 1f, 1f);

        bool pressed = Button(id, s);
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);


        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var center = (pos + max) * 0.5f;

        if (!state)
        {
            var a = center + new Vector2(-4f * scale, -6f * scale);
            var b = center + new Vector2(-4f * scale, 6f * scale);
            var c = center + new Vector2(6f * scale, 0f * scale);
            draw.AddTriangleFilled(a, b, c, ImGui.GetColorU32(colText));
        }
        else
        {
            float r = 6f * scale;
            draw.AddRectFilled(center - new Vector2(r, r), center + new Vector2(r, r), ImGui.GetColorU32(colText), 2f * scale);
        }

        if (pressed)
            state = !state;

        return pressed;
    }



}
