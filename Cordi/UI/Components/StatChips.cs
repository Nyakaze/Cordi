using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Components;

public sealed class StatChip
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    public required FontAwesomeIcon Icon { get; init; }
    public string Tooltip { get; init; } = string.Empty;
}

public sealed class StatChips
{
    private readonly UiTheme theme;

    public StatChips(UiTheme theme)
    {
        this.theme = theme;
    }

    public float Draw(IReadOnlyList<StatChip> chips, Vector2 rightAnchor)
    {
        if (chips.Count == 0)
            return 0f;

        float height = theme.Scaled(UiTheme.StatChipHeight);
        float gap = theme.Gap();
        var widths = new float[chips.Count];
        float total = 0f;

        for (int i = 0; i < chips.Count; i++)
        {
            widths[i] = MeasureWidth(chips[i]);
            total += widths[i] + (i > 0 ? gap : 0f);
        }

        float x = rightAnchor.X - total;
        for (int i = 0; i < chips.Count; i++)
        {
            DrawChip(chips[i], new Vector2(x, rightAnchor.Y), widths[i], height, i);
            x += widths[i] + gap;
        }

        return total;
    }

    private float MeasureWidth(StatChip chip)
    {
        theme.ApplyFontScale(0.88f);
        float labelWidth = ImGui.CalcTextSize(chip.Label).X;
        theme.ApplyFontScale(1.25f);
        float valueWidth = ImGui.CalcTextSize(chip.Value).X;
        theme.ApplyFontScale();

        float textWidth = MathF.Max(labelWidth, valueWidth);
        return theme.Scaled(30f) + textWidth + theme.PadX(2.4f);
    }

    private void DrawChip(StatChip chip, Vector2 min, float width, float height, int index)
    {
        var draw = ImGui.GetWindowDrawList();
        var max = min + new Vector2(width, height);

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton($"##stat-chip-{index}", new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        if (hovered && !string.IsNullOrEmpty(chip.Tooltip))
            ImGui.SetTooltip(chip.Tooltip);

        draw.AddRectFilled(min, max, ImGui.GetColorU32(hovered ? theme.RowHover : theme.CardBg), theme.Radius(1.2f));
        draw.AddRect(min, max, ImGui.GetColorU32(theme.Border), theme.Radius(1.2f));

        float tile = theme.Scaled(26f);
        var tileMin = new Vector2(min.X + theme.PadX(0.9f), min.Y + (height - tile) * 0.5f);
        draw.AddRectFilled(tileMin, tileMin + new Vector2(tile, tile), ImGui.GetColorU32(theme.AccentSoft), theme.Radius(0.8f));

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.Accent))
        {
            var glyph = chip.Icon.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(tileMin + new Vector2((tile - size.X) * 0.5f, (tile - size.Y) * 0.5f));
            ImGui.TextUnformatted(glyph);
        }

        float textX = tileMin.X + tile + theme.Gap(0.9f);

        theme.ApplyFontScale(0.88f);
        ImGui.SetCursorScreenPos(new Vector2(textX, min.Y + theme.Scaled(9f)));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.MutedText))
            ImGui.TextUnformatted(chip.Label);

        theme.ApplyFontScale(1.25f);
        ImGui.SetCursorScreenPos(new Vector2(textX, min.Y + theme.Scaled(25f)));
        ImGui.TextUnformatted(chip.Value);
        theme.ApplyFontScale();
    }
}
