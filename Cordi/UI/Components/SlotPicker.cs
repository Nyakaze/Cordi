using System.Collections.Generic;
using System.Numerics;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Cordi.UI.Components;

public readonly record struct SlotTile(string Label, FontAwesomeIcon Icon, Vector4 Color, int Slot, bool Active);

public sealed class SlotPicker
{
    private const string Payload = "CORDI_SLOT_TILE";
    private const int Columns = 2;
    private const int Rows = 2;
    private const float CellHeight = 62f;

    private static readonly byte[] PayloadData = { 1 };

    private readonly UiTheme theme;

    private int dragged = -1;
    private int selected = -1;

    public SlotPicker(UiTheme theme) => this.theme = theme;

    public bool Draw(string id, float width, IReadOnlyList<SlotTile> tiles, out int tile, out int slot)
    {
        tile = -1;
        slot = -1;

        if (selected >= tiles.Count) selected = -1;
        if (dragged >= tiles.Count) dragged = -1;

        float gap = theme.Gap(0.6f);
        float cellWidth = (width - gap) / Columns;
        float cellHeight = theme.Scaled(CellHeight);
        var origin = ImGui.GetCursorScreenPos();

        for (int index = 0; index < Rows * Columns; index++)
        {
            var min = new Vector2(
                origin.X + index % Columns * (cellWidth + gap),
                origin.Y + index / Columns * (cellHeight + gap));

            DrawCell($"{id}-{index}", min, new Vector2(cellWidth, cellHeight), index, tiles, ref tile, ref slot);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cellHeight * Rows + gap));

        return tile >= 0;
    }

    private void DrawCell(
        string cellId,
        Vector2 min,
        Vector2 size,
        int slot,
        IReadOnlyList<SlotTile> tiles,
        ref int movedTile,
        ref int targetSlot)
    {
        int occupant = -1;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i].Slot == slot) occupant = i;
        }

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton($"##{cellId}", size);

        bool hovered = ImGui.IsItemHovered();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        if (ImGui.IsItemClicked())
        {
            if (selected >= 0)
            {
                if (tiles[selected].Slot != slot)
                {
                    movedTile = selected;
                    targetSlot = slot;
                }

                selected = -1;
            }
            else if (occupant >= 0)
            {
                selected = occupant;
            }
        }

        if (occupant >= 0 && ImGui.BeginDragDropSource())
        {
            dragged = occupant;
            selected = -1;
            ImGui.SetDragDropPayload(Payload, PayloadData, ImGuiCond.Always);
            ImGui.TextUnformatted(tiles[occupant].Label);
            ImGui.EndDragDropSource();
        }

        if (ImGui.BeginDragDropTarget())
        {
            var payload = ImGui.AcceptDragDropPayload(Payload, ImGuiDragDropFlags.None);

            if (!payload.IsNull && dragged >= 0 && tiles[dragged].Slot != slot)
            {
                movedTile = dragged;
                targetSlot = slot;
            }

            ImGui.EndDragDropTarget();
        }

        var max = min + size;
        float rounding = theme.Radius(0.6f);

        if (occupant < 0)
        {
            theme.Surface(min, max, theme.PanelBg, theme.Border, rounding);

            if (hovered)
                theme.SurfaceOutline(min, max, theme.AccentHover, rounding);

            CenteredLabel(min, size, "Empty", theme.FaintText, 0.5f);
            return;
        }

        var occupied = tiles[occupant];
        var accent = occupied.Active ? occupied.Color : theme.MutedText;
        var fill = new Vector4(accent.X, accent.Y, accent.Z, hovered ? 0.26f : 0.16f);
        var border = selected == occupant ? theme.Accent : accent;

        theme.Surface(min, max, fill, border, rounding);

        if (selected == occupant)
            theme.SurfaceOutline(min, max, theme.Accent, rounding, 2f);

        theme.IconGlyph(min + new Vector2(0, size.Y * 0.12f), new Vector2(size.X, size.Y * 0.42f), occupied.Icon, accent);
        CenteredLabel(min, size, occupied.Label, occupied.Active ? theme.Text : theme.MutedText, 0.62f);
    }

    private static void CenteredLabel(Vector2 min, Vector2 size, string text, Vector4 color, float verticalFraction)
    {
        var textSize = ImGui.CalcTextSize(text);

        ImGui.GetWindowDrawList().AddText(
            new Vector2(min.X + (size.X - textSize.X) * 0.5f, min.Y + size.Y * verticalFraction),
            ImGui.GetColorU32(color),
            text);
    }
}
