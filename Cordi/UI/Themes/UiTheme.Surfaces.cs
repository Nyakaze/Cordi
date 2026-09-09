using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Cordi.UI.Themes;

public sealed partial class UiTheme
{
    public void GridCell(
        Vector2 min,
        Vector2 max,
        bool hovered,
        bool marked,
        IDalamudTextureWrap? image = null,
        float inset = 0f,
        Action<IDalamudTextureWrap?, Vector2, Vector2>? onImage = null)
    {
        var draw = ImGui.GetWindowDrawList();

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            draw.AddRectFilled(min, max, ImGui.GetColorU32(AccentSelected), Radius(0.4f));
        }

        if (hovered || marked)
            draw.AddRect(min, max, ImGui.GetColorU32(hovered ? AccentHover : Accent), Radius(0.4f), ImDrawFlags.None, 1f);

        if (image == null) return;

        var imageMin = min + new Vector2(inset, inset);
        var imageMax = max - new Vector2(inset, inset);

        onImage?.Invoke(image, imageMin, imageMax);
        draw.AddImage(image.Handle, imageMin, imageMax);
    }

    public void Surface(Vector2 min, Vector2 max, Vector4 fill, Vector4? border = null, float? rounding = null)
    {
        var draw = ImGui.GetWindowDrawList();
        var radius = rounding ?? Radius();

        draw.AddRectFilled(min, max, ImGui.GetColorU32(fill), radius);

        if (border.HasValue)
            draw.AddRect(min, max, ImGui.GetColorU32(border.Value), radius);
    }

    public void SurfaceOutline(Vector2 min, Vector2 max, Vector4 color, float? rounding = null, float thickness = 1f) =>
        ImGui.GetWindowDrawList().AddRect(
            min,
            max,
            ImGui.GetColorU32(color),
            rounding ?? Radius(),
            ImDrawFlags.None,
            thickness);

    public void AccentCard(ImDrawListPtr draw, Vector2 min, Vector2 max, Vector4 fill, Vector4 barColor, float barWidth)
    {
        draw.AddRectFilled(min, max, ImGui.GetColorU32(fill), Radius(0.5f));

        draw.AddRectFilled(
            min,
            new Vector2(min.X + barWidth, max.Y),
            ImGui.GetColorU32(barColor),
            Radius(0.5f),
            ImDrawFlags.RoundCornersLeft);
    }

    public void OverlayIconButton(Vector2 min, float size, Dalamud.Interface.FontAwesomeIcon icon, bool hovered, float alpha = 0.55f)
    {
        var box = new Vector2(size, size);

        ImGui.GetWindowDrawList().AddRectFilled(
            min,
            min + box,
            ImGui.GetColorU32(hovered ? ColorDanger : new Vector4(0f, 0f, 0f, alpha)),
            Radius(0.3f));

        IconGlyph(min, box, icon, Vector4.One);
    }

    public void HighlightRow(ImDrawListPtr draw, Vector2 min, Vector2 max, Vector4 fill, Vector4? accent = null)
    {
        draw.AddRectFilled(min, max, ImGui.GetColorU32(fill));

        if (!accent.HasValue) return;

        draw.AddRectFilled(
            min,
            new Vector2(min.X + RuleThickness() * 2f, max.Y),
            ImGui.GetColorU32(accent.Value));
    }

    public void HoverRow(ImDrawListPtr draw, Vector2 min, Vector2 max) =>
        draw.AddRectFilled(min, max, ImGui.GetColorU32(Hover), Radius(0.4f));

    public void Avatar(
        Vector2 min,
        Vector2 max,
        IDalamudTextureWrap? image,
        float rounding,
        Vector4 fallback,
        string initials = "",
        Action<IDalamudTextureWrap?, Vector2, Vector2>? onImage = null)
    {
        var draw = ImGui.GetWindowDrawList();

        if (image != null)
        {
            onImage?.Invoke(image, min, max);
            draw.AddImageRounded(image.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFF, rounding);
            return;
        }

        draw.AddRectFilled(min, max, ImGui.GetColorU32(fallback), rounding);

        if (initials.Length == 0) return;

        var textSize = ImGui.CalcTextSize(initials);
        draw.AddText(min + (max - min - textSize) * 0.5f, 0xFFFFFFFF, initials);
    }

    public void DirectionArrow(ImDrawListPtr draw, Vector2 center, float size, float angle, uint color)
    {
        var screenAngle = angle - MathF.PI / 2f;
        var dir = new Vector2(MathF.Cos(screenAngle), MathF.Sin(screenAngle));
        var tip = center + dir * size;
        var baseCenter = center - dir * size * 0.4f;
        var perp = new Vector2(-dir.Y, dir.X);

        draw.AddTriangleFilled(tip, baseCenter + perp * size * 0.5f, baseCenter - perp * size * 0.5f, color);
    }

    public void RowSurface(Vector2 min, Vector2 max, bool selected)
    {
        if (selected) Surface(min, max, AccentSoft, AccentBorder);
        else Surface(min, max, RowBg);
    }

    public void AccentBar(Vector2 min, Vector2 max, Vector4 color) =>
        ImGui.GetWindowDrawList().AddRectFilled(min, max, ImGui.GetColorU32(color), Scaled(1.5f));

    public void AccentSwatch(Vector2 pos, float size, Vector4 color, bool selected, bool hovered)
    {
        Surface(pos, pos + new Vector2(size, size), color, rounding: Radius(0.9f));

        if (!selected && !hovered) return;

        var ring = Scaled(3f);

        SurfaceOutline(
            pos - new Vector2(ring, ring),
            pos + new Vector2(size + ring, size + ring),
            selected ? Text : MutedText,
            Radius(1.1f),
            Scaled(2f));
    }
}
