using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Cordi.UI.Themes;

public sealed partial class UiTheme
{
    public float RuleThickness() => MathF.Max(1f, ImGuiHelpers.GlobalScale);

    public void RuleH(ImDrawListPtr draw, Vector2 origin, float width, Vector4? color = null)
    {
        var thickness = RuleThickness();

        draw.AddRectFilled(
            origin,
            new Vector2(origin.X + width, origin.Y + thickness),
            ImGui.GetColorU32(color ?? FaintText),
            thickness * 0.5f);
    }

    public void RuleV(ImDrawListPtr draw, Vector2 origin, float height, Vector4? color = null)
    {
        var thickness = RuleThickness();

        draw.AddRectFilled(
            origin,
            new Vector2(origin.X + thickness, origin.Y + height),
            ImGui.GetColorU32(color ?? FaintText),
            thickness * 0.5f);
    }

    public void DividerMark(float width, string tooltip = "")
    {
        var thickness = RuleThickness();
        var height = thickness + Gap(0.8f);
        var origin = ImGui.GetCursorScreenPos();

        ImGui.Dummy(new Vector2(width, height));
        RuleH(ImGui.GetWindowDrawList(), new Vector2(origin.X, origin.Y + (height - thickness) * 0.5f), width);

        if (tooltip.Length > 0 && ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);

        ImGui.Dummy(new Vector2(0, Gap(0.5f)));
    }

    public void DividerRow(string label, float width, float padding)
    {
        var draw = ImGui.GetWindowDrawList();
        var thickness = RuleThickness();

        ImGui.Dummy(new Vector2(0, Gap(0.5f)));

        var origin = ImGui.GetCursorScreenPos();
        var lineY = origin.Y;

        if (label.Length > 0)
        {
            var text = label.ToUpperInvariant();
            var textSize = ImGui.CalcTextSize(text);

            ImGui.Dummy(new Vector2(width, textSize.Y));
            draw.AddText(new Vector2(origin.X + padding, origin.Y), ImGui.GetColorU32(FaintText), text);
            lineY = origin.Y + textSize.Y + Gap(0.25f);
        }

        RuleH(draw, new Vector2(origin.X + padding, lineY), MathF.Max(0f, width - padding * 2f));

        ImGui.Dummy(new Vector2(0, thickness + Gap(0.6f)));
    }

    public void DividerTrailing(string label, float width, Vector4 color)
    {
        var draw = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var height = ImGui.GetTextLineHeight();
        var tint = ImGui.GetColorU32(color);
        var labelWidth = ImGui.CalcTextSize(label).X + PadX(0.6f);
        var y = origin.Y + height * 0.5f;

        draw.AddLine(new Vector2(origin.X, y), new Vector2(origin.X + width - labelWidth, y), tint, 1f);
        draw.AddText(new Vector2(origin.X + width - labelWidth + PadX(0.3f), origin.Y), tint, label);

        ImGui.Dummy(new Vector2(width, height));
    }

    public bool DividerAction(string id, string label, float width)
    {
        var rowWidth = MathF.Max(width, 40f);
        var height = ImGui.GetFrameHeight();
        var origin = ImGui.GetCursorScreenPos();
        var pressed = ImGui.InvisibleButton(id, new Vector2(rowWidth, height));
        var hovered = ImGui.IsItemHovered();

        var draw = ImGui.GetWindowDrawList();
        var textSize = ImGui.CalcTextSize(label);
        var pillHeight = textSize.Y + Gap(0.5f);
        var pillWidth = MathF.Min(rowWidth, textSize.X + PadX(1.2f));
        var pillMin = new Vector2(origin.X + (rowWidth - pillWidth) * 0.5f, origin.Y + (height - pillHeight) * 0.5f);
        var pillMax = pillMin + new Vector2(pillWidth, pillHeight);
        var over = hovered && ImGui.IsMouseHoveringRect(pillMin, pillMax);

        if (over) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        var y = origin.Y + height * 0.5f;
        var rule = ImGui.GetColorU32(FaintText);
        var thickness = RuleThickness();
        var gap = Gap(0.4f);

        draw.AddLine(new Vector2(origin.X, y), new Vector2(pillMin.X - gap, y), rule, thickness);
        draw.AddLine(new Vector2(pillMax.X + gap, y), new Vector2(origin.X + rowWidth, y), rule, thickness);

        draw.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(over ? Hover : FrameBg), pillHeight * 0.5f);
        draw.AddText(pillMin + (pillMax - pillMin - textSize) * 0.5f, ImGui.GetColorU32(over ? Text : MutedText), label);

        return pressed && over;
    }

    public void DividerCell(string label, float width, float height)
    {
        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(width, height));

        var draw = ImGui.GetWindowDrawList();

        if (label.Length > 0)
        {
            var shown = Fit(label.ToUpperInvariant(), width);
            var textSize = ImGui.CalcTextSize(shown);

            draw.AddText(
                new Vector2(origin.X + MathF.Max(0f, (width - textSize.X) * 0.5f), origin.Y + (height - textSize.Y) * 0.5f),
                ImGui.GetColorU32(FaintText),
                shown);

            return;
        }

        var inset = height * 0.2f;
        RuleV(draw, new Vector2(origin.X + (width - RuleThickness()) * 0.5f, origin.Y + inset), height - inset * 2f);
    }
}
