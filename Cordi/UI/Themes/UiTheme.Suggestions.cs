using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Themes;

public sealed partial class UiTheme
{
    public const float SuggestionScrollbarWidth = 4f;

    public float SuggestionRowHeight() => ImGui.GetTextLineHeight() + PadY(0.55f);

    public float SuggestionPanelHeight(int rows, bool hasHint) =>
        (rows + (hasHint ? 1 : 0)) * SuggestionRowHeight() + PadY(0.3f) * 2f;

    public UiSuggestionHit SuggestionPanel(
        Vector2 min,
        Vector2 max,
        IReadOnlyList<UiSuggestionItem> rows,
        int first,
        int total,
        int selected,
        string hint = "",
        Action<IDalamudTextureWrap?, Vector2, Vector2>? onImage = null)
    {
        var draw = ImGui.GetWindowDrawList();
        var padding = PadY(0.3f);
        var rowHeight = SuggestionRowHeight();
        var scrollable = total > rows.Count;
        var scrollbar = scrollable ? Scaled(SuggestionScrollbarWidth) + Gap(0.2f) : 0f;

        draw.AddRectFilled(min, max, ImGui.GetColorU32(PanelBg), Radius(0.5f));
        draw.AddRect(min, max, ImGui.GetColorU32(Border), Radius(0.5f));

        var interactive = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows);
        var hovered = -1;
        var clicked = -1;

        for (var row = 0; row < rows.Count; row++)
        {
            var index = first + row;
            var rowMin = new Vector2(min.X + padding, min.Y + padding + row * rowHeight);
            var rowMax = new Vector2(max.X - padding - scrollbar, rowMin.Y + rowHeight);

            if (interactive && ImGui.IsMouseHoveringRect(rowMin, rowMax))
            {
                hovered = index;
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) clicked = index;
            }

            if (index == selected)
                draw.AddRectFilled(rowMin, rowMax, ImGui.GetColorU32(AccentSelected), Radius(0.35f));

            SuggestionRow(draw, rows[row], rowMin, rowMax.X, rowHeight, onImage);
        }

        if (scrollable)
            SuggestionScrollbar(draw, min, max, padding, rows.Count * rowHeight, first, rows.Count, total);

        if (hint.Length > 0)
            SuggestionHint(draw, new Vector2(min.X + padding, max.Y - padding - rowHeight), max.X - padding, rowHeight, hint);

        return new UiSuggestionHit { Hovered = hovered, Clicked = clicked };
    }

    private void SuggestionRow(
        ImDrawListPtr draw,
        UiSuggestionItem item,
        Vector2 rowMin,
        float right,
        float rowHeight,
        Action<IDalamudTextureWrap?, Vector2, Vector2>? onImage)
    {
        var glyph = ImGui.GetTextLineHeight();
        var iconMin = new Vector2(rowMin.X + PadX(0.4f), rowMin.Y + (rowHeight - glyph) * 0.5f);

        if (item.Image != null)
        {
            var imageMax = iconMin + new Vector2(glyph, glyph);
            onImage?.Invoke(item.Image, iconMin, imageMax);
            draw.AddImage(item.Image.Handle, iconMin, imageMax);
        }
        else if (item.Icon != default)
        {
            using var font = ImRaii.PushFont(UiBuilder.IconFont);
            draw.AddText(iconMin, ImGui.GetColorU32(MutedText), item.Icon.ToIconString());
        }

        var textX = iconMin.X + glyph + PadX(0.5f);
        var available = right - textX - PadX(0.4f);
        var detail = item.Detail ?? string.Empty;

        if (detail.Length > 0)
        {
            var labelWidth = ImGui.CalcTextSize(item.Label).X;
            var detailWidth = MathF.Max(0f, available - labelWidth - Gap(0.8f));

            if (detailWidth > glyph * 2f)
            {
                var shown = Fit(detail, detailWidth);
                var detailX = right - PadX(0.4f) - ImGui.CalcTextSize(shown).X;
                draw.AddText(new Vector2(detailX, iconMin.Y), ImGui.GetColorU32(FaintText), shown);
                available = detailX - textX - Gap(0.8f);
            }
        }

        draw.AddText(new Vector2(textX, iconMin.Y), ImGui.GetColorU32(Text), Fit(item.Label, available));
    }

    private void SuggestionHint(ImDrawListPtr draw, Vector2 rowMin, float right, float rowHeight, string hint)
    {
        var textY = rowMin.Y + (rowHeight - ImGui.GetTextLineHeight()) * 0.5f;

        draw.AddLine(rowMin, new Vector2(right, rowMin.Y), ImGui.GetColorU32(Border));

        draw.AddText(
            new Vector2(rowMin.X + PadX(0.4f), textY),
            ImGui.GetColorU32(FaintText),
            Fit(hint, right - rowMin.X - PadX(0.8f)));
    }

    private void SuggestionScrollbar(
        ImDrawListPtr draw,
        Vector2 min,
        Vector2 max,
        float padding,
        float listHeight,
        int first,
        int visible,
        int total)
    {
        var width = MathF.Max(2f, Scaled(SuggestionScrollbarWidth));
        var right = max.X - padding;
        var top = min.Y + padding;

        var thumbHeight = MathF.Max(Scaled(14f), listHeight * visible / total);
        var travel = MathF.Max(0f, listHeight - thumbHeight);
        var progress = total > visible ? first / (float)(total - visible) : 0f;
        var thumbTop = top + travel * progress;

        draw.AddRectFilled(
            new Vector2(right - width, top),
            new Vector2(right, top + listHeight),
            ImGui.GetColorU32(Border),
            width * 0.5f);

        draw.AddRectFilled(
            new Vector2(right - width, thumbTop),
            new Vector2(right, thumbTop + thumbHeight),
            ImGui.GetColorU32(MutedText),
            width * 0.5f);
    }
}
