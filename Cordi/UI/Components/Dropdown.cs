using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Components;

public readonly struct DropdownItem
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public string? Group { get; init; }
}

public sealed class Dropdown
{
    private readonly UiTheme theme;

    public Dropdown(UiTheme theme)
    {
        this.theme = theme;
    }

    public void Draw(
        string id,
        float width,
        string preview,
        bool hasValue,
        IReadOnlyList<DropdownItem> items,
        string selectedKey,
        Action<string> onSelect)
    {
        var (popupId, min, max) = DrawHeader(id, width, preview, hasValue);
        DrawPopup(popupId, min, max, width, items, key => key == selectedKey, key =>
        {
            onSelect(key);
            ImGui.CloseCurrentPopup();
        });
    }

    public void DrawMulti(
        string id,
        float width,
        string preview,
        bool hasValue,
        IReadOnlyList<DropdownItem> items,
        Func<string, bool> isSelected,
        Action<string> onToggle)
    {
        var (popupId, min, max) = DrawHeader(id, width, preview, hasValue);
        DrawPopup(popupId, min, max, width, items, isSelected, onToggle);
    }

    public const float CaptionFontScale = 0.78f;

    public float CaptionHeight()
    {
        theme.ApplyFontScale(CaptionFontScale);
        float height = ImGui.GetTextLineHeight() + theme.Scaled(3f);
        theme.ApplyFontScale();

        return height;
    }

    public void DrawIconPicker(
        string id,
        Vector2 size,
        FontAwesomeIcon icon,
        string caption,
        float captionWidth,
        IReadOnlyList<DropdownItem> items,
        string selectedKey,
        Action<string> onSelect,
        float popupWidth,
        bool enabled = true,
        string tooltip = "",
        Vector4? accent = null)
    {
        string popupId = $"##dropdown-popup-{id}";
        var draw = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var max = min + size;
        float alpha = enabled ? 1f : 0.5f;
        var captionColor = accent ?? theme.MutedText;

        if (!string.IsNullOrEmpty(caption))
        {
            theme.ApplyFontScale(CaptionFontScale);
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(captionColor.X, captionColor.Y, captionColor.Z, captionColor.W * alpha)))
            {
                ImGui.SetCursorScreenPos(new Vector2(min.X, min.Y - ImGui.GetTextLineHeight() - theme.Scaled(3f)));
                theme.FittedText(caption, captionWidth);
            }
            theme.ApplyFontScale();
        }

        bool open = ImGui.IsPopupOpen(popupId);

        ImGui.SetCursorScreenPos(min);
        bool clicked = ImGui.InvisibleButton($"##dropdown-{id}", size) && enabled;
        bool hovered = ImGui.IsItemHovered();
        var afterButton = ImGui.GetCursorScreenPos();

        if (hovered && enabled)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        var fill = (hovered && enabled) || open ? theme.FrameBgHover : theme.FrameBg;

        draw.AddRectFilled(min, max, Fade(fill, alpha), theme.Radius());
        draw.AddRect(min, max, Fade(open ? theme.Accent : accent ?? theme.Border, alpha), theme.Radius());

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = icon.ToIconString();
            var glyphSize = ImGui.CalcTextSize(glyph);
            draw.AddText(
                min + (size - glyphSize) * 0.5f,
                Fade((hovered && enabled) || open ? accent ?? theme.Text : captionColor, alpha),
                glyph);
        }

        if (!string.IsNullOrEmpty(tooltip) && hovered)
            theme.Tooltip(tooltip);

        if (clicked)
            ImGui.OpenPopup(popupId);

        ImGui.SetCursorScreenPos(afterButton);

        DrawPopup(popupId, min, max, popupWidth, items, key => key == selectedKey, key =>
        {
            onSelect(key);
            ImGui.CloseCurrentPopup();
        }, above: true);
    }

    private static uint Fade(Vector4 color, float alpha) =>
        ImGui.GetColorU32(new Vector4(color.X, color.Y, color.Z, color.W * alpha));

    private (string PopupId, Vector2 Min, Vector2 Max) DrawHeader(
        string id,
        float width,
        string preview,
        bool hasValue)
    {
        string popupId = $"##dropdown-popup-{id}";
        var draw = ImGui.GetWindowDrawList();
        float height = theme.Scaled(UiTheme.ControlHeight);
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, height);

        bool open = ImGui.IsPopupOpen(popupId);

        bool clicked = ImGui.InvisibleButton($"##dropdown-{id}", new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        var afterButton = ImGui.GetCursorScreenPos();
        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        draw.AddRectFilled(min, max, ImGui.GetColorU32(hovered || open ? theme.FrameBgHover : theme.FrameBg), theme.Radius());
        draw.AddRect(min, max, ImGui.GetColorU32(open ? theme.Accent : theme.Border), theme.Radius());

        float chevronSpace = theme.Scaled(28f);
        var textPos = new Vector2(min.X + theme.PadX(0.8f), min.Y + (height - ImGui.GetTextLineHeight()) * 0.5f);
        var shown = theme.Fit(preview, max.X - chevronSpace - textPos.X);

        draw.AddText(textPos, ImGui.GetColorU32(hasValue ? theme.Text : theme.FaintText), shown);

        if (hovered && shown != preview)
            theme.Tooltip(preview);

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, hovered || open ? theme.Text : theme.FaintText))
        {
            var glyph = FontAwesomeIcon.ChevronDown.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(new Vector2(max.X - theme.PadX(0.8f) - size.X, min.Y + (height - size.Y) * 0.5f));
            ImGui.TextUnformatted(glyph);
        }

        if (clicked)
            ImGui.OpenPopup(popupId);

        ImGui.SetCursorScreenPos(afterButton);
        return (popupId, min, max);
    }

    private void DrawPopup(
        string popupId,
        Vector2 min,
        Vector2 max,
        float width,
        IReadOnlyList<DropdownItem> items,
        Func<string, bool> isSelected,
        Action<string> onSelect,
        bool above = false)
    {
        float rowHeight = theme.Scaled(30f);
        float spacing = theme.Gap(0.2f);
        float headerHeight = GroupHeaderHeight();
        float wanted = items.Count * (rowHeight + spacing) + GroupCount(items) * (headerHeight + spacing) + theme.PadY(1.2f);
        float capped = MathF.Min(wanted, theme.Scaled(320f));

        ImGui.SetNextWindowPos(above
            ? new Vector2(min.X, min.Y - capped - theme.Gap(0.35f))
            : new Vector2(min.X, max.Y + theme.Gap(0.35f)));
        ImGui.SetNextWindowSize(new Vector2(width, capped));

        using (ImRaii.PushColor(ImGuiCol.PopupBg, theme.CardBg))
        using (ImRaii.PushColor(ImGuiCol.Border, theme.Border))
        using (ImRaii.PushStyle(ImGuiStyleVar.PopupRounding, theme.Radius(1.2f)))
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(theme.PadX(0.4f), theme.PadY(0.6f))))
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(0, spacing)))
        {
            using var popup = ImRaii.Popup(popupId, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings);
            if (!popup)
                return;

            string? group = null;

            for (int i = 0; i < items.Count; i++)
            {
                if (!string.IsNullOrEmpty(items[i].Group) && items[i].Group != group)
                {
                    group = items[i].Group;
                    DrawGroupHeader(group!, headerHeight);
                }

                if (DrawRow(items[i], isSelected(items[i].Key), rowHeight, i))
                    onSelect(items[i].Key);
            }
        }
    }

    private float GroupHeaderHeight()
    {
        theme.ApplyFontScale(CaptionFontScale);
        float height = ImGui.GetTextLineHeight() + theme.Gap(0.6f);
        theme.ApplyFontScale();

        return height;
    }

    private static int GroupCount(IReadOnlyList<DropdownItem> items)
    {
        int count = 0;
        string? group = null;

        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.Group) || item.Group == group)
                continue;

            group = item.Group;
            count++;
        }

        return count;
    }

    private void DrawGroupHeader(string label, float height)
    {
        var min = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;

        ImGui.Dummy(new Vector2(width, height));
        var afterDummy = ImGui.GetCursorScreenPos();

        theme.ApplyFontScale(CaptionFontScale);
        using (ImRaii.PushColor(ImGuiCol.Text, theme.FaintText))
        {
            ImGui.SetCursorScreenPos(new Vector2(min.X + theme.PadX(0.7f), min.Y + height - ImGui.GetTextLineHeight()));
            theme.FittedText(label.ToUpperInvariant(), width - theme.PadX(1.4f));
        }
        theme.ApplyFontScale();

        ImGui.SetCursorScreenPos(afterDummy);
    }

    private bool DrawRow(DropdownItem item, bool selected, float height, int index)
    {
        var draw = ImGui.GetWindowDrawList();
        float width = ImGui.GetContentRegionAvail().X;
        var min = ImGui.GetCursorScreenPos();

        bool clicked = ImGui.InvisibleButton($"##dropdown-row-{index}", new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        var afterButton = ImGui.GetCursorScreenPos();
        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        if (hovered || selected)
            draw.AddRectFilled(min, min + new Vector2(width, height), ImGui.GetColorU32(selected ? theme.AccentSoft : theme.RowHover), theme.Radius(0.8f));

        float checkSpace = theme.Scaled(22f);
        var textPos = new Vector2(min.X + theme.PadX(0.7f), min.Y + (height - ImGui.GetTextLineHeight()) * 0.5f);
        var shown = theme.Fit(item.Label, min.X + width - checkSpace - textPos.X);

        draw.AddText(textPos, ImGui.GetColorU32(theme.Text), shown);

        if (hovered && shown != item.Label)
            theme.Tooltip(item.Label);

        if (selected)
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            using (ImRaii.PushColor(ImGuiCol.Text, theme.Accent))
            {
                var glyph = FontAwesomeIcon.Check.ToIconString();
                var size = ImGui.CalcTextSize(glyph);
                ImGui.SetCursorScreenPos(new Vector2(min.X + width - theme.PadX(0.5f) - size.X, min.Y + (height - size.Y) * 0.5f));
                ImGui.TextUnformatted(glyph);
            }
        }

        ImGui.SetCursorScreenPos(afterButton);
        return clicked;
    }
}
