using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Components;

public sealed class ToggleGridEntry
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required bool Value { get; init; }
    public required Action<bool> OnToggle { get; init; }
    public string Tooltip { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
}

public sealed class ToggleGrid
{
    private const float MinColumnWidth = 220f;

    private readonly UiTheme theme;

    public ToggleGrid(UiTheme theme)
    {
        this.theme = theme;
    }

    public void Draw(string id, IReadOnlyList<ToggleGridEntry> entries, float width)
    {
        if (entries.Count == 0)
            return;

        int columns = width >= theme.Scaled(MinColumnWidth) * 2f + theme.Gap(2f) ? 2 : 1;
        float columnWidth = (width - theme.Gap(2f) * (columns - 1)) / columns;
        float rowHeight = theme.Scaled(UiTheme.ControlHeight);
        int rows = (entries.Count + columns - 1) / columns;

        var origin = ImGui.GetCursorScreenPos();

        for (int index = 0; index < entries.Count; index++)
        {
            int column = index % columns;
            int row = index / columns;
            var pos = new Vector2(
                origin.X + column * (columnWidth + theme.Gap(2f)),
                origin.Y + row * (rowHeight + theme.Gap(0.4f)));

            DrawEntry($"{id}-{entries[index].Id}", entries[index], pos, columnWidth, rowHeight);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * rowHeight + (rows - 1) * theme.Gap(0.4f)));
    }

    private void DrawEntry(string id, ToggleGridEntry entry, Vector2 pos, float width, float height)
    {
        var draw = ImGui.GetWindowDrawList();
        var max = pos + new Vector2(width, height);

        ImGui.SetCursorScreenPos(pos);
        bool rowClicked = ImGui.InvisibleButton($"##togglegrid-{id}", new Vector2(width, height));
        ImGui.SetItemAllowOverlap();
        bool hovered = ImGui.IsItemHovered();
        if (hovered && entry.Enabled)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        if (hovered)
            draw.AddRectFilled(pos, max, ImGui.GetColorU32(theme.RowHover), theme.Radius());

        if (!string.IsNullOrEmpty(entry.Tooltip) && hovered)
            theme.Tooltip(entry.Tooltip);

        var toggleSize = theme.ToggleSize();
        var togglePos = new Vector2(max.X - theme.PadX(0.6f) - toggleSize.X, pos.Y + (height - toggleSize.Y) * 0.5f);

        var labelSize = ImGui.CalcTextSize(entry.Label);
        using (ImRaii.PushColor(ImGuiCol.Text, entry.Enabled ? theme.Text : theme.FaintText))
        {
            ImGui.SetCursorScreenPos(new Vector2(pos.X + theme.PadX(0.6f), pos.Y + (height - labelSize.Y) * 0.5f));
            ImGui.TextUnformatted(entry.Label);
        }

        bool value = entry.Value;
        bool toggled = theme.ToggleSwitch($"##togglegrid-switch-{id}", togglePos, ref value, entry.Enabled);

        if (rowClicked && entry.Enabled && !toggled)
        {
            value = !entry.Value;
            toggled = true;
        }

        if (toggled)
            entry.OnToggle(value);
    }
}
