using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Components;

public sealed class ChipSelectorGroup
{
    public required string Label { get; init; }
    public required IReadOnlyList<DropdownItem> Items { get; init; }
}

public sealed class ChipSelector
{
    private const float ChipHeight = 30f;
    private const float GroupHeaderHeight = 26f;
    private const float MarkSize = 12f;
    private const float ChipFontScale = 0.9f;
    private const float SmallFontScale = 0.82f;
    private const int SearchMaxLength = 64;

    private readonly UiTheme theme;
    private readonly Dictionary<string, string> searches = new(StringComparer.Ordinal);
    private readonly HashSet<string> collapsed = new(StringComparer.Ordinal);

    public ChipSelector(UiTheme theme)
    {
        this.theme = theme;
    }

    public void Draw(
        string id,
        float width,
        IReadOnlyList<ChipSelectorGroup> groups,
        Func<string, bool> isSelected,
        Action<string> onToggle,
        Action<IReadOnlyList<string>, bool>? onSetMany = null,
        bool advanced = false,
        Action<bool>? onViewChanged = null,
        Func<string, bool>? isPartial = null,
        string noun = "chat type")
    {
        var origin = ImGui.GetCursorScreenPos();
        float controlHeight = theme.Scaled(UiTheme.ControlHeight);

        if (!searches.TryGetValue(id, out var search))
            search = string.Empty;

        int total = groups.Sum(g => g.Items.Count);
        int selected = groups.Sum(g => g.Items.Count(i => isSelected(i.Key)));

        float clearWidth = MiniWidth("Clear all");
        float searchWidth = MathF.Max(theme.Scaled(120f), width - clearWidth - theme.Gap());

        theme.TextInput($"##chip-search-{id}", origin, searchWidth, ref search, SearchMaxLength, $"Search {noun}s");
        searches[id] = search;

        if (MiniButton(
                $"chip-clear-{id}",
                new Vector2(origin.X + width - clearWidth, origin.Y),
                clearWidth,
                controlHeight,
                "Clear all")
            && selected > 0)
        {
            SetMany(groups.SelectMany(g => g.Items).Select(i => i.Key).ToList(), false, isSelected, onToggle, onSetMany);
        }

        float y = origin.Y + controlHeight + theme.Gap(0.7f);
        float summaryHeight = theme.Scaled(22f);

        theme.ApplyFontScale(SmallFontScale);
        using (ImRaii.PushColor(ImGuiCol.Text, theme.FaintText))
        {
            ImGui.SetCursorScreenPos(new Vector2(origin.X, y + (summaryHeight - ImGui.GetTextLineHeight()) * 0.5f));
            ImGui.TextUnformatted($"{selected} of {total} selected");
        }
        theme.ApplyFontScale();

        if (onViewChanged != null)
            DrawViewToggle(id, new Vector2(origin.X + width, y), summaryHeight, advanced, onViewChanged);

        y += summaryHeight + theme.Gap(0.8f);

        bool searching = !string.IsNullOrWhiteSpace(search);
        bool any = false;

        foreach (var group in groups)
        {
            var visible = Filter(group.Items, search);
            if (visible.Count == 0)
                continue;

            any = true;
            string key = $"{id}::{group.Label}";
            bool open = searching || !collapsed.Contains(key);
            int groupSelected = visible.Count(i => isSelected(i.Key));

            y = DrawGroupHeader(
                key,
                group.Label,
                new Vector2(origin.X, y),
                width,
                groupSelected,
                visible.Count,
                open,
                value => SetMany(visible.Select(i => i.Key).ToList(), value, isSelected, onToggle, onSetMany));

            if (open)
                y = DrawChips(key, visible, new Vector2(origin.X, y), width, isSelected, onToggle, isPartial);
        }

        if (!any)
        {
            theme.ApplyFontScale(SmallFontScale);
            using (ImRaii.PushColor(ImGuiCol.Text, theme.FaintText))
            {
                ImGui.SetCursorScreenPos(new Vector2(origin.X, y));
                ImGui.TextUnformatted($"No {noun} matches \"{search}\".");
            }
            y += ImGui.GetTextLineHeight();
            theme.ApplyFontScale();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, y - origin.Y)));
    }

    private void DrawViewToggle(string id, Vector2 rightAnchor, float height, bool advanced, Action<bool> onChanged)
    {
        float simpleWidth = MiniWidth("Simple");
        float advancedWidth = MiniWidth("Advanced");
        float width = simpleWidth + advancedWidth;
        var pos = new Vector2(rightAnchor.X - width, rightAnchor.Y);
        var draw = ImGui.GetWindowDrawList();

        draw.AddRectFilled(pos, pos + new Vector2(width, height), ImGui.GetColorU32(theme.FrameBg), theme.Radius(0.6f));
        draw.AddRect(pos, pos + new Vector2(width, height), ImGui.GetColorU32(theme.Border), theme.Radius(0.6f));

        if (DrawViewSegment($"{id}-simple", pos, simpleWidth, height, "Simple", !advanced) && advanced)
            onChanged(false);

        if (DrawViewSegment($"{id}-advanced", new Vector2(pos.X + simpleWidth, pos.Y), advancedWidth, height, "Advanced", advanced) && !advanced)
            onChanged(true);
    }

    private bool DrawViewSegment(string id, Vector2 pos, float width, float height, string label, bool active)
    {
        var draw = ImGui.GetWindowDrawList();

        ImGui.SetCursorScreenPos(pos);
        bool clicked = ImGui.InvisibleButton($"##view-{id}", new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        if (active)
        {
            draw.AddRectFilled(
                pos,
                pos + new Vector2(width, height),
                ImGui.GetColorU32(new Vector4(theme.Accent.X, theme.Accent.Y, theme.Accent.Z, 0.28f)),
                theme.Radius(0.6f));
        }

        theme.ApplyFontScale(SmallFontScale);
        var size = ImGui.CalcTextSize(label);
        using (ImRaii.PushColor(ImGuiCol.Text, active ? theme.Text : hovered ? theme.Text : theme.MutedText))
        {
            ImGui.SetCursorScreenPos(new Vector2(pos.X + (width - size.X) * 0.5f, pos.Y + (height - size.Y) * 0.5f));
            ImGui.TextUnformatted(label);
        }
        theme.ApplyFontScale();

        return clicked;
    }

    private static void SetMany(
        IReadOnlyList<string> keys,
        bool value,
        Func<string, bool> isSelected,
        Action<string> onToggle,
        Action<IReadOnlyList<string>, bool>? onSetMany)
    {
        if (onSetMany != null)
        {
            onSetMany(keys, value);
            return;
        }

        foreach (var key in keys)
        {
            if (isSelected(key) != value)
                onToggle(key);
        }
    }

    private static List<DropdownItem> Filter(IReadOnlyList<DropdownItem> items, string search) =>
        string.IsNullOrWhiteSpace(search)
            ? items.ToList()
            : items
                .Where(i => i.Label.Contains(search, StringComparison.OrdinalIgnoreCase)
                            || i.Key.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

    private float DrawGroupHeader(
        string key,
        string label,
        Vector2 pos,
        float width,
        int selected,
        int total,
        bool open,
        Action<bool> onSetAll)
    {
        float height = theme.Scaled(GroupHeaderHeight);

        ImGui.SetCursorScreenPos(pos);
        bool clicked = ImGui.InvisibleButton($"##chip-group-{key}", new Vector2(width, height));
        ImGui.SetItemAllowOverlap();
        bool hovered = ImGui.IsItemHovered();
        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        float chevron = theme.Scaled(14f);

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, hovered ? theme.Text : theme.FaintText))
        {
            theme.ApplyFontScale(0.75f);
            var glyph = (open ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight).ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(new Vector2(pos.X + (chevron - size.X) * 0.5f, pos.Y + (height - size.Y) * 0.5f));
            ImGui.TextUnformatted(glyph);
            theme.ApplyFontScale();
        }

        float labelX = pos.X + chevron + theme.Gap(0.5f);

        theme.ApplyFontScale(SmallFontScale);
        var caption = label.ToUpperInvariant();
        var labelSize = ImGui.CalcTextSize(caption);
        using (ImRaii.PushColor(ImGuiCol.Text, hovered ? theme.Text : theme.FaintText))
        {
            ImGui.SetCursorScreenPos(new Vector2(labelX, pos.Y + (height - labelSize.Y) * 0.5f));
            ImGui.TextUnformatted(caption);
        }
        theme.ApplyFontScale();

        var count = $"{selected}/{total}";
        var countSize = theme.ChipSize(count);
        theme.ChipAt(
            new Vector2(labelX + labelSize.X + theme.Gap(0.7f), pos.Y + (height - countSize.Y) * 0.5f),
            count,
            selected > 0 ? theme.Accent : theme.FaintText);

        float noneWidth = MiniWidth("None");
        float allWidth = MiniWidth("All");
        float buttonHeight = theme.Scaled(20f);
        float buttonY = pos.Y + (height - buttonHeight) * 0.5f;
        float noneX = pos.X + width - noneWidth;
        float allX = noneX - theme.Gap(0.4f) - allWidth;

        if (MiniButton($"chip-all-{key}", new Vector2(allX, buttonY), allWidth, buttonHeight, "All"))
            onSetAll(true);

        if (MiniButton($"chip-none-{key}", new Vector2(noneX, buttonY), noneWidth, buttonHeight, "None"))
            onSetAll(false);

        if (clicked)
        {
            if (!collapsed.Add(key))
                collapsed.Remove(key);
        }

        return pos.Y + height + theme.Gap(0.4f);
    }

    private float DrawChips(
        string key,
        IReadOnlyList<DropdownItem> items,
        Vector2 pos,
        float width,
        Func<string, bool> isSelected,
        Action<string> onToggle,
        Func<string, bool>? isPartial)
    {
        float height = theme.Scaled(ChipHeight);
        float gap = theme.Gap(0.5f);
        float columnWidth = 0f;

        foreach (var item in items)
            columnWidth = MathF.Max(columnWidth, ChipWidth(item.Label));

        columnWidth = MathF.Min(columnWidth, width);
        int columns = Math.Max(1, (int)MathF.Floor((width + gap) / (columnWidth + gap)));
        float y = pos.Y;

        for (int index = 0; index < items.Count; index++)
        {
            int column = index % columns;
            if (column == 0 && index > 0)
                y += height + gap;

            var chipPos = new Vector2(pos.X + column * (columnWidth + gap), y);
            bool selected = isSelected(items[index].Key);
            bool partial = !selected && isPartial?.Invoke(items[index].Key) == true;

            if (DrawChip($"{key}-{items[index].Key}", chipPos, columnWidth, height, items[index].Label, selected, partial))
                onToggle(items[index].Key);
        }

        return y + height + theme.Gap(1f);
    }

    private bool DrawChip(string id, Vector2 pos, float width, float height, string label, bool selected, bool partial)
    {
        var draw = ImGui.GetWindowDrawList();

        ImGui.SetCursorScreenPos(pos);
        bool clicked = ImGui.InvisibleButton($"##chip-{id}", new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        var max = pos + new Vector2(width, height);
        var fill = selected
            ? new Vector4(theme.Accent.X, theme.Accent.Y, theme.Accent.Z, hovered ? 0.34f : 0.22f)
            : partial
                ? new Vector4(theme.Accent.X, theme.Accent.Y, theme.Accent.Z, hovered ? 0.20f : 0.12f)
                : hovered ? theme.RowHover : theme.RowBg;

        draw.AddRectFilled(pos, max, ImGui.GetColorU32(fill), theme.Radius(0.8f));
        draw.AddRect(pos, max, ImGui.GetColorU32(selected || partial ? theme.Accent : theme.Border), theme.Radius(0.8f));

        float mark = theme.Scaled(MarkSize);
        var markPos = new Vector2(pos.X + theme.PadX(0.6f), pos.Y + (height - mark) * 0.5f);

        if (partial)
        {
            float inset = theme.Scaled(3f);
            float bar = MathF.Max(1f, theme.Scaled(2f));
            draw.AddRect(markPos, markPos + new Vector2(mark, mark), ImGui.GetColorU32(theme.Accent), theme.Radius(0.35f));
            draw.AddRectFilled(
                new Vector2(markPos.X + inset, markPos.Y + (mark - bar) * 0.5f),
                new Vector2(markPos.X + mark - inset, markPos.Y + (mark + bar) * 0.5f),
                ImGui.GetColorU32(theme.Accent));
        }
        else if (selected)
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            using (ImRaii.PushColor(ImGuiCol.Text, theme.Accent))
            {
                theme.ApplyFontScale(0.7f);
                var glyph = FontAwesomeIcon.Check.ToIconString();
                var size = ImGui.CalcTextSize(glyph);
                ImGui.SetCursorScreenPos(new Vector2(markPos.X + (mark - size.X) * 0.5f, pos.Y + (height - size.Y) * 0.5f));
                ImGui.TextUnformatted(glyph);
                theme.ApplyFontScale();
            }
        }
        else
        {
            draw.AddRect(markPos, markPos + new Vector2(mark, mark), ImGui.GetColorU32(theme.Border), theme.Radius(0.35f));
        }

        float textX = markPos.X + mark + theme.Gap(0.5f);
        float limit = MathF.Max(theme.Scaled(16f), max.X - theme.PadX(0.6f) - textX);

        theme.ApplyFontScale(ChipFontScale);
        var text = theme.Fit(label, limit);
        using (ImRaii.PushColor(ImGuiCol.Text, selected || partial ? theme.Text : theme.MutedText))
        {
            ImGui.SetCursorScreenPos(new Vector2(textX, pos.Y + (height - ImGui.GetTextLineHeight()) * 0.5f));
            ImGui.TextUnformatted(text);
        }
        theme.ApplyFontScale();

        if (hovered && !string.Equals(text, label, StringComparison.Ordinal))
            theme.Tooltip(label);

        return clicked;
    }

    private float ChipWidth(string label)
    {
        theme.ApplyFontScale(ChipFontScale);
        float text = ImGui.CalcTextSize(label).X;
        theme.ApplyFontScale();

        return theme.PadX(0.6f) * 2f + theme.Scaled(MarkSize) + theme.Gap(0.5f) + text;
    }

    private float MiniWidth(string label)
    {
        theme.ApplyFontScale(SmallFontScale);
        float text = ImGui.CalcTextSize(label).X;
        theme.ApplyFontScale();

        return text + theme.PadX(0.9f) * 2f;
    }

    private bool MiniButton(string id, Vector2 pos, float width, float height, string label)
    {
        var draw = ImGui.GetWindowDrawList();

        ImGui.SetCursorScreenPos(pos);
        bool clicked = ImGui.InvisibleButton($"##mini-{id}", new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        var max = pos + new Vector2(width, height);
        draw.AddRectFilled(pos, max, ImGui.GetColorU32(hovered ? theme.RowHover : theme.RowBg), theme.Radius(0.6f));
        draw.AddRect(pos, max, ImGui.GetColorU32(theme.Border), theme.Radius(0.6f));

        theme.ApplyFontScale(SmallFontScale);
        var size = ImGui.CalcTextSize(label);
        using (ImRaii.PushColor(ImGuiCol.Text, hovered ? theme.Text : theme.MutedText))
        {
            ImGui.SetCursorScreenPos(new Vector2(pos.X + (width - size.X) * 0.5f, pos.Y + (height - size.Y) * 0.5f));
            ImGui.TextUnformatted(label);
        }
        theme.ApplyFontScale();

        return clicked;
    }
}
