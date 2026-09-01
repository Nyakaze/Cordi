using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Components;

public enum ListColumnKind
{
    Text,
    Number,
    Option,
}

public sealed class ListColumn<T>
{
    public required string Id { get; init; }
    public ListColumnKind Kind { get; init; } = ListColumnKind.Text;
    public string Header { get; init; } = string.Empty;
    public string Hint { get; init; } = string.Empty;
    public float FixedWidth { get; init; }
    public float Weight { get; init; } = 1f;
    public int MaxLength { get; init; } = 128;
    public int Min { get; init; } = int.MinValue;
    public int Max { get; init; } = int.MaxValue;
    public IReadOnlyList<DropdownItem> Options { get; init; } = Array.Empty<DropdownItem>();
    public Func<T, string>? GetText { get; init; }
    public Action<T, string>? SetText { get; init; }
    public Func<T, int>? GetNumber { get; init; }
    public Action<T, int>? SetNumber { get; init; }
}

public sealed class ListPanel
{
    private const float MinColumnWidth = 90f;

    private readonly UiTheme theme;
    private readonly Panel panel;

    public ListPanel(UiTheme theme)
    {
        this.theme = theme;
        panel = new Panel(theme);
    }

    public void Draw<T>(
        string id,
        string label,
        string description,
        IList<T> items,
        IReadOnlyList<ListColumn<T>> columns,
        Func<T> createItem,
        Action onChanged,
        string addLabel = "Add Entry",
        string emptyText = "Nothing configured yet.",
        string removeTooltip = "Remove",
        Action<Vector2>? drawTrailing = null,
        Action<float>? drawLead = null)
    {
        panel.Draw(
            id,
            innerWidth =>
            {
                if (!string.IsNullOrEmpty(description))
                {
                    ImGui.TextColored(theme.MutedText, description);
                    theme.SpacerY(0.5f);
                }

                if (drawLead is not null)
                {
                    drawLead(innerWidth);
                    theme.SpacerY(0.5f);
                }

                if (items.Count == 0)
                {
                    ImGui.TextColored(theme.FaintText, emptyText);
                }
                else
                {
                    var widths = Measure(columns, innerWidth);

                    DrawHeaders(columns, widths, innerWidth);

                    int? removeIndex = null;

                    for (int index = 0; index < items.Count; index++)
                    {
                        if (DrawRow($"{id}-{index}", items[index], columns, widths, innerWidth, removeTooltip, onChanged))
                            removeIndex = index;
                    }

                    if (removeIndex is { } target)
                    {
                        items.RemoveAt(target);
                        onChanged();
                    }
                }

                theme.SpacerY(0.5f);

                if (theme.SecondaryButton($"+ {addLabel}##{id}-add", new Vector2(innerWidth, theme.Scaled(32f))))
                {
                    items.Add(createItem());
                    onChanged();
                }
                theme.HoverHandIfItem();
            },
            label: label,
            drawTrailing: drawTrailing);
    }

    private float[] Measure<T>(IReadOnlyList<ListColumn<T>> columns, float innerWidth)
    {
        float gap = theme.Gap(0.7f);
        float available = innerWidth - theme.Scaled(UiTheme.ActionButtonSize) - gap * columns.Count;
        float totalWeight = 0f;

        foreach (var column in columns)
        {
            if (column.FixedWidth > 0f)
                available -= theme.Scaled(column.FixedWidth);
            else
                totalWeight += column.Weight;
        }

        var widths = new float[columns.Count];

        for (int index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            widths[index] = column.FixedWidth > 0f
                ? theme.Scaled(column.FixedWidth)
                : MathF.Max(theme.Scaled(MinColumnWidth), available * (column.Weight / MathF.Max(totalWeight, 1f)));
        }

        return widths;
    }

    private void DrawHeaders<T>(IReadOnlyList<ListColumn<T>> columns, float[] widths, float innerWidth)
    {
        bool any = false;
        foreach (var column in columns)
            any |= !string.IsNullOrEmpty(column.Header);

        if (!any)
            return;

        float gap = theme.Gap(0.7f);
        var origin = ImGui.GetCursorScreenPos();
        float x = origin.X;

        theme.ApplyFontScale(0.84f);
        using (ImRaii.PushColor(ImGuiCol.Text, theme.FaintText))
        {
            for (int index = 0; index < columns.Count; index++)
            {
                if (!string.IsNullOrEmpty(columns[index].Header))
                {
                    ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
                    ImGui.TextUnformatted(columns[index].Header);
                }

                x += widths[index] + gap;
            }
        }
        theme.ApplyFontScale();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(innerWidth, ImGui.GetTextLineHeight() + theme.Gap(0.4f)));
    }

    private bool DrawRow<T>(
        string id,
        T item,
        IReadOnlyList<ListColumn<T>> columns,
        float[] widths,
        float innerWidth,
        string removeTooltip,
        Action onChanged)
    {
        var origin = ImGui.GetCursorScreenPos();
        float height = theme.Scaled(UiTheme.ControlHeight);
        float gap = theme.Gap(0.7f);
        float x = origin.X;

        for (int index = 0; index < columns.Count; index++)
        {
            DrawCell($"{id}-{columns[index].Id}", item, columns[index], new Vector2(x, origin.Y), widths[index], onChanged);
            x += widths[index] + gap;
        }

        bool remove = theme.DeleteAction(
            $"{id}-remove",
            new Vector2(origin.X + innerWidth - theme.Scaled(UiTheme.ActionButtonSize), origin.Y),
            removeTooltip,
            theme.Scaled(UiTheme.ActionButtonSize),
            height);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(innerWidth, height + theme.Gap(0.4f)));

        return remove;
    }

    private void DrawCell<T>(string id, T item, ListColumn<T> column, Vector2 pos, float width, Action onChanged)
    {
        switch (column.Kind)
        {
            case ListColumnKind.Number:
            {
                int value = column.GetNumber!(item);
                if (theme.NumberInput($"##{id}", pos, width, ref value, column.Min, column.Max))
                {
                    column.SetNumber!(item, value);
                    onChanged();
                }

                break;
            }

            case ListColumnKind.Option:
            {
                ImGui.SetCursorScreenPos(pos);
                theme.OptionPicker(
                    id,
                    column.GetText!(item),
                    column.Options,
                    key =>
                    {
                        column.SetText!(item, key);
                        onChanged();
                    },
                    width: width);

                break;
            }

            default:
            {
                string value = column.GetText!(item);
                theme.TextInput($"##{id}", pos, width, ref value, column.MaxLength, column.Hint);
                column.SetText!(item, value);

                if (ImGui.IsItemDeactivatedAfterEdit())
                    onChanged();

                break;
            }
        }
    }
}
