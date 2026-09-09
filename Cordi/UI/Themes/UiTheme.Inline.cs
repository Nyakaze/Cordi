using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Themes;

public sealed partial class UiTheme
{
    public static readonly Vector2 TextShadowOffset = new(1f, 1f);

    public void ShadowedText(string text, bool shadow = true, bool disabled = false)
    {
        if (shadow) TextShadowAt(ImGui.GetWindowDrawList(), ImGui.GetCursorScreenPos(), text);

        if (disabled) ImGui.TextDisabled(text);
        else ImGui.TextUnformatted(text);
    }

    public void TextShadowAt(ImDrawListPtr draw, Vector2 position, string text) =>
        draw.AddText(position + TextShadowOffset, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.8f)), text);

    public void ShadowedTextAt(ImDrawListPtr draw, Vector2 position, string text, uint color, bool shadow)
    {
        if (shadow) TextShadowAt(draw, position, text);

        draw.AddText(position, color, text);
    }

    public void TextGlow(ImDrawListPtr draw, Vector2 position, string text, Vector4 color, float thickness)
    {
        const int layers = 3;
        const int directions = 8;

        for (var layer = layers; layer >= 1; layer--)
        {
            var radius = thickness * (layer / (float)layers);
            var alpha = color.W * (1f - (layer - 1) / (float)layers) * 0.5f;
            var tint = ImGui.GetColorU32(new Vector4(color.X, color.Y, color.Z, alpha));

            for (var d = 0; d < directions; d++)
            {
                var angle = MathF.Tau * d / directions;
                draw.AddText(position + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius), tint, text);
            }
        }
    }

    public void IconGlyph(Vector2 min, Vector2 size, FontAwesomeIcon icon, Vector4 color)
    {
        using var font = ImRaii.PushFont(UiBuilder.IconFont);

        var glyph = icon.ToIconString();
        var glyphSize = ImGui.CalcTextSize(glyph);

        ImGui.GetWindowDrawList().AddText(min + (size - glyphSize) * 0.5f, ImGui.GetColorU32(color), glyph);
    }

    public float ChipInlinePadding() => ImGui.GetStyle().FramePadding.X * 0.6f;

    public float ChipInlineWidth(string text) => ImGui.CalcTextSize(text).X + ChipInlinePadding() * 2f;

    public UiRect ChipInlineBounds(string text, Vector2 origin, float lineHeight)
    {
        var textSize = ImGui.CalcTextSize(text);
        var top = origin.Y + (lineHeight - textSize.Y) * 0.5f - 1f;

        return new UiRect(
            new Vector2(origin.X, top),
            new Vector2(origin.X + textSize.X + ChipInlinePadding() * 2f, top + textSize.Y + 2f));
    }

    public void ChipInline(
        ImDrawListPtr draw,
        UiRect bounds,
        string text,
        Vector4 foreground,
        Vector4? background = null,
        float rounding = 0f)
    {
        if (background.HasValue)
            draw.AddRectFilled(bounds.Min, bounds.Max, ImGui.GetColorU32(background.Value), rounding);

        draw.AddText(
            new Vector2(bounds.Min.X + ChipInlinePadding(), bounds.Min.Y + 1f),
            ImGui.GetColorU32(foreground),
            text);
    }

    public uint TextOutlineColor(float alpha) =>
        ImGui.GetColorU32(new Vector4(0f, 0f, 0f, Math.Clamp(alpha, 0f, 1f) * 0.9f));

    public void TextOutline(ImDrawListPtr draw, Vector2 position, ReadOnlySpan<char> text, float alpha)
    {
        var shade = TextOutlineColor(alpha);

        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                draw.AddText(position + new Vector2(dx, dy), shade, text);
            }
        }
    }

    public void TextUnderline(ImDrawListPtr draw, Vector2 min, Vector2 max, Vector4 color, bool outlined = false)
    {
        var start = new Vector2(min.X, max.Y - 1f);
        var end = new Vector2(max.X, max.Y - 1f);

        if (outlined)
            draw.AddLine(start + Vector2.One, end + Vector2.One, TextOutlineColor(color.W));

        draw.AddLine(start, end, ImGui.GetColorU32(color));
    }
}
