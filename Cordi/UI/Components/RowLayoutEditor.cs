using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Components;

public sealed class RowLayoutEditor
{
    private const string Payload = "CORDI_ROW_FIELD";
    private const float ChipHeight = 28f;
    private const float ZonePadding = 6f;
    private const float SmallFontScale = 0.82f;

    private static readonly byte[] PayloadData = { 1 };

    private static readonly (RowSide Side, string Label, string Hint)[] Zones =
    {
        (RowSide.Left, "Left", "Starts at the left edge"),
        (RowSide.Right, "Right", "Sticks to the right edge"),
        (RowSide.Second, "Second line", "Own line underneath"),
    };

    private readonly UiTheme theme;

    private string dragged = string.Empty;
    private string menuKey = string.Empty;

    public RowLayoutEditor(UiTheme theme) => this.theme = theme;

    public bool Draw(string id, float width, RowLayoutConfig layout, IReadOnlyList<RowFieldInfo> catalog, bool showHistory)
    {
        bool changed = false;
        var origin = ImGui.GetCursorScreenPos();
        float y = origin.Y;

        y = DrawPreview(new Vector2(origin.X, y), width, layout, catalog) + theme.Gap(0.9f);

        foreach (var zone in Zones)
        {
            y = DrawZone($"{id}-{zone.Side}", new Vector2(origin.X, y), width, zone.Label, zone.Hint, layout, catalog, zone.Side, true, ref changed);
            y += theme.Gap(0.6f);
        }

        y = DrawZone($"{id}-hidden", new Vector2(origin.X, y), width, "Hidden", "Not shown at all", layout, catalog, RowSide.Left, false, ref changed);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, y - origin.Y)));

        changed |= DrawFieldMenu(layout, catalog, showHistory);

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) dragged = string.Empty;

        return changed;
    }

    private float DrawPreview(Vector2 pos, float width, RowLayoutConfig layout, IReadOnlyList<RowFieldInfo> catalog)
    {
        float lineHeight = ImGui.GetTextLineHeight();
        bool second = layout.Visible(RowSide.Second).Any();
        float height = lineHeight * (second ? 2 : 1) + theme.Scaled(ZonePadding) * 2f;

        var max = new Vector2(pos.X + width, pos.Y + height);
        theme.Surface(pos, max, theme.FrameBg, theme.Border, theme.Radius(0.6f));

        var draw = ImGui.GetWindowDrawList();
        float pad = theme.Scaled(ZonePadding);
        float gap = ImGui.GetStyle().ItemSpacing.X;
        float textY = pos.Y + pad;

        float x = pos.X + pad;
        foreach (var field in layout.Visible(RowSide.Left))
        {
            PaintPreview(draw, new Vector2(x, textY), catalog, field, lineHeight);
            x += PreviewWidth(catalog, field, lineHeight) + gap;
        }

        x = max.X - pad;
        foreach (var field in layout.Visible(RowSide.Right).Reverse())
        {
            x -= PreviewWidth(catalog, field, lineHeight);
            PaintPreview(draw, new Vector2(x, textY), catalog, field, lineHeight);
            x -= gap;
        }

        if (second)
        {
            x = pos.X + pad;
            foreach (var field in layout.Visible(RowSide.Second))
            {
                PaintPreview(draw, new Vector2(x, textY + lineHeight), catalog, field, lineHeight);
                x += PreviewWidth(catalog, field, lineHeight) + gap;
            }
        }

        return max.Y;
    }

    private static bool IsGlyphField(IReadOnlyList<RowFieldInfo> catalog, RowFieldConfig field) =>
        RowFieldCatalog.Info(catalog, field.Key) is { DefaultFormat.Length: 0 };

    private float PreviewWidth(IReadOnlyList<RowFieldInfo> catalog, RowFieldConfig field, float lineHeight) =>
        IsGlyphField(catalog, field) ? lineHeight : ImGui.CalcTextSize(Sample(catalog, field)).X;

    private void PaintPreview(ImDrawListPtr draw, Vector2 pos, IReadOnlyList<RowFieldInfo> catalog, RowFieldConfig field, float lineHeight)
    {
        if (!IsGlyphField(catalog, field))
        {
            draw.AddText(pos, Color(field), Sample(catalog, field));
            return;
        }

        if (field.Key == RowFieldCatalog.Direction)
        {
            var center = new Vector2(pos.X + lineHeight * 0.5f, pos.Y + lineHeight * 0.5f);
            theme.DirectionArrow(draw, center, lineHeight * 0.28f, 0.9f, Color(field));
            return;
        }

        theme.IconGlyph(pos, new Vector2(lineHeight), FontAwesomeIcon.Reply, ColorVec(field));
    }

    private Vector4 ColorVec(RowFieldConfig field) => field.Style switch
    {
        RowFieldStyle.Muted => theme.MutedText,
        RowFieldStyle.Accent => theme.Accent,
        RowFieldStyle.Custom => field.Color,
        _ => theme.Text,
    };

    private uint Color(RowFieldConfig field) => ImGui.GetColorU32(ColorVec(field));

    private float DrawZone(
        string zoneId,
        Vector2 pos,
        float width,
        string label,
        string hint,
        RowLayoutConfig layout,
        IReadOnlyList<RowFieldInfo> catalog,
        RowSide side,
        bool enabled,
        ref bool changed)
    {
        var fields = enabled
            ? layout.Visible(side).ToList()
            : layout.Fields.Where(f => !f.Enabled).ToList();

        float headerHeight = theme.Scaled(18f);
        DrawZoneHeader(new Vector2(pos.X, pos.Y), width, label, hint);

        float top = pos.Y + headerHeight + theme.Gap(0.3f);
        float pad = theme.Scaled(ZonePadding);
        float chipHeight = theme.Scaled(ChipHeight);
        float gap = theme.Gap(0.4f);

        var widths = fields.Select(f => ChipWidth(Label(catalog, f.Key))).ToList();
        float x = pos.X + pad;
        float y = top + pad;
        float rowLimit = pos.X + width - pad;
        int rows = 1;

        var slots = new List<(float X, float Y, float Width)>();

        for (int i = 0; i < fields.Count; i++)
        {
            if (x > pos.X + pad && x + widths[i] > rowLimit)
            {
                x = pos.X + pad;
                y += chipHeight + gap;
                rows++;
            }

            slots.Add((x, y, widths[i]));
            x += widths[i] + gap;
        }

        float height = pad * 2f + rows * chipHeight + (rows - 1) * gap;
        var min = new Vector2(pos.X, top);
        var max = new Vector2(pos.X + width, top + height);

        var mouse = ImGui.GetMousePos();
        bool dragging = dragged.Length > 0;
        bool over = dragging && mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y;

        theme.Surface(
            min,
            max,
            over ? theme.RowHover : theme.PanelBg,
            over ? theme.Accent : dragging ? theme.AccentHover : theme.Border,
            theme.Radius(0.6f));

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton($"##zone-{zoneId}", new Vector2(width, height));
        ImGui.SetItemAllowOverlap();

        int dropAt = DropIndex(slots, chipHeight, fields.Count);
        AcceptDrop(layout, side, enabled, dropAt, ref changed);

        if (fields.Count == 0)
        {
            theme.ApplyFontScale(SmallFontScale);
            using (ImRaii.PushColor(ImGuiCol.Text, theme.FaintText))
            {
                ImGui.SetCursorScreenPos(new Vector2(min.X + pad, min.Y + (height - ImGui.GetTextLineHeight()) * 0.5f));
                ImGui.TextUnformatted("Drop something here");
            }
            theme.ApplyFontScale();
        }

        for (int i = 0; i < fields.Count; i++)
        {
            var slot = slots[i];
            DrawChip($"{zoneId}-{fields[i].Key}", new Vector2(slot.X, slot.Y), slot.Width, chipHeight, fields[i], catalog, enabled);
        }

        if (over) DrawCaret(slots, dropAt, new Vector2(pos.X + pad, top + pad), chipHeight, gap);

        return max.Y;
    }

    private void DrawCaret(List<(float X, float Y, float Width)> slots, int index, Vector2 fallback, float chipHeight, float gap)
    {
        float x;
        float y;

        if (index < slots.Count)
        {
            x = slots[index].X - gap * 0.5f;
            y = slots[index].Y;
        }
        else if (slots.Count > 0)
        {
            var last = slots[^1];
            x = last.X + last.Width + gap * 0.5f;
            y = last.Y;
        }
        else
        {
            x = fallback.X;
            y = fallback.Y;
        }

        float half = theme.Scaled(1.5f);
        var draw = ImGui.GetWindowDrawList();
        var color = ImGui.GetColorU32(theme.Accent);

        draw.AddRectFilled(new Vector2(x - half, y), new Vector2(x + half, y + chipHeight), color, half);
        draw.AddCircleFilled(new Vector2(x, y), half * 2f, color);
        draw.AddCircleFilled(new Vector2(x, y + chipHeight), half * 2f, color);
    }

    private static int DropIndex(List<(float X, float Y, float Width)> slots, float chipHeight, int count)
    {
        var mouse = ImGui.GetMousePos();

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];

            if (mouse.Y > slot.Y + chipHeight) continue;
            if (mouse.X < slot.X + slot.Width * 0.5f) return i;
        }

        return count;
    }

    private void DrawZoneHeader(Vector2 pos, float width, string label, string hint)
    {
        theme.ApplyFontScale(SmallFontScale);

        using (ImRaii.PushColor(ImGuiCol.Text, theme.FaintText))
        {
            ImGui.SetCursorScreenPos(pos);
            ImGui.TextUnformatted(label.ToUpperInvariant());

            var size = ImGui.CalcTextSize(label.ToUpperInvariant());
            ImGui.SetCursorScreenPos(new Vector2(pos.X + size.X + theme.Gap(0.7f), pos.Y));
            ImGui.TextUnformatted(hint);
        }

        theme.ApplyFontScale();
    }

    private void DrawChip(
        string chipId,
        Vector2 pos,
        float width,
        float height,
        RowFieldConfig field,
        IReadOnlyList<RowFieldInfo> catalog,
        bool enabled)
    {
        var draw = ImGui.GetWindowDrawList();
        var max = pos + new Vector2(width, height);

        ImGui.SetCursorScreenPos(pos);
        bool clicked = ImGui.InvisibleButton($"##chip-{chipId}", new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        bool held = ImGui.IsItemActive();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        if (ImGui.BeginDragDropSource())
        {
            dragged = field.Key;
            ImGui.SetDragDropPayload(Payload, PayloadData, ImGuiCond.Always);
            ImGui.TextUnformatted(Label(catalog, field.Key));
            ImGui.EndDragDropSource();
        }

        var accent = enabled ? theme.Accent : theme.MutedText;
        float rounding = theme.Radius(0.6f);

        draw.AddRectFilled(pos, max, ImGui.GetColorU32(new Vector4(accent.X, accent.Y, accent.Z, held ? 0.42f : hovered ? 0.30f : 0.16f)), rounding);
        draw.AddRect(pos, max, ImGui.GetColorU32(new Vector4(accent.X, accent.Y, accent.Z, hovered ? 0.95f : 0.45f)), rounding);

        theme.ApplyFontScale(SmallFontScale);
        var label = Label(catalog, field.Key);
        using (ImRaii.PushColor(ImGuiCol.Text, enabled ? theme.Text : theme.MutedText))
        {
            ImGui.SetCursorScreenPos(new Vector2(pos.X + theme.PadX(0.7f), pos.Y + (height - ImGui.GetTextLineHeight()) * 0.5f));
            ImGui.TextUnformatted(label);
        }
        theme.ApplyFontScale();

        if (hovered)
            theme.Tooltip($"{Description(catalog, field.Key)}\n\nDrag to move it. Click to change how it looks.");

        if (clicked)
        {
            menuKey = field.Key;
            ImGui.OpenPopup("##row-field-menu");
        }
    }

    private void AcceptDrop(RowLayoutConfig layout, RowSide side, bool enabled, int index, ref bool changed)
    {
        if (!ImGui.BeginDragDropTarget()) return;

        var payload = ImGui.AcceptDragDropPayload(Payload, ImGuiDragDropFlags.None);

        if (!payload.IsNull && dragged.Length > 0)
        {
            if (enabled)
            {
                layout.Move(dragged, side, index);
            }
            else
            {
                var field = layout.Find(dragged);
                if (field != null) field.Enabled = false;
            }

            dragged = string.Empty;
            changed = true;
        }

        ImGui.EndDragDropTarget();
    }

    private bool DrawFieldMenu(RowLayoutConfig layout, IReadOnlyList<RowFieldInfo> catalog, bool showHistory)
    {
        using var popup = ImRaii.Popup("##row-field-menu");
        if (!popup) return false;

        var field = layout.Find(menuKey);
        if (field == null) return false;

        bool changed = false;

        using (ImRaii.PushColor(ImGuiCol.Text, theme.FaintText))
        {
            ImGui.TextUnformatted(Label(catalog, field.Key).ToUpperInvariant());
        }

        ImGui.Separator();

        changed |= DrawFormatEditor(field, RowFieldCatalog.Info(catalog, field.Key));

        foreach (var style in new[] { RowFieldStyle.Normal, RowFieldStyle.Muted, RowFieldStyle.Accent, RowFieldStyle.Custom })
        {
            if (!ImGui.RadioButton(StyleLabel(style), field.Style == style) || field.Style == style) continue;

            field.Style = style;
            changed = true;
        }

        if (field.Style == RowFieldStyle.Custom)
        {
            var color = field.Color;
            if (ImGui.ColorEdit4("Colour", ref color, ImGuiColorEditFlags.NoInputs))
            {
                field.Color = color;
                changed = true;
            }
        }

        if (showHistory)
        {
            ImGui.Separator();

            bool inHistory = field.InHistory;
            if (ImGui.Checkbox("Also in the history", ref inHistory))
            {
                field.InHistory = inHistory;
                changed = true;
            }
        }

        ImGui.Separator();

        if (ImGui.MenuItem(field.Enabled ? "Hide it" : "Show it"))
        {
            field.Enabled = !field.Enabled;
            changed = true;
        }

        return changed;
    }

    private bool DrawFormatEditor(RowFieldConfig field, RowFieldInfo? info)
    {
        if (info == null || info.DefaultFormat.Length == 0) return false;

        bool changed = false;

        ImGui.SetNextItemWidth(theme.Scaled(260f));

        var format = field.Format;
        if (ImGui.InputTextWithHint("##row-format", info.DefaultFormat, ref format, 96))
        {
            field.Format = format;
            changed = true;
        }

        theme.ApplyFontScale(SmallFontScale);

        foreach (var token in info.Tokens)
        {
            if (ImGui.SmallButton($"{{{token.Token}}}##tok-{token.Token}"))
            {
                field.Format += $"{{{token.Token}}}";
                changed = true;
            }

            if (ImGui.IsItemHovered()) theme.Tooltip($"{token.Description}\nLooks like: {token.Sample}");

            ImGui.SameLine();
        }

        if (ImGui.SmallButton("Default##row-format-reset"))
        {
            field.Format = info.DefaultFormat;
            changed = true;
        }

        theme.ApplyFontScale();

        ImGui.Separator();

        return changed;
    }

    private static string StyleLabel(RowFieldStyle style) => style switch
    {
        RowFieldStyle.Muted => "Dimmed",
        RowFieldStyle.Accent => "Highlight colour",
        RowFieldStyle.Custom => "Own colour",
        _ => "Normal",
    };

    private static string Label(IReadOnlyList<RowFieldInfo> catalog, string key) =>
        catalog.FirstOrDefault(f => f.Key == key)?.Label ?? key;

    private static string Description(IReadOnlyList<RowFieldInfo> catalog, string key) =>
        catalog.FirstOrDefault(f => f.Key == key)?.Description ?? string.Empty;

    private static string Sample(IReadOnlyList<RowFieldInfo> catalog, RowFieldConfig field)
    {
        var info = RowFieldCatalog.Info(catalog, field.Key);
        if (info == null) return field.Key;

        return RowFieldCatalog.Format(field.Format, token => info.Tokens.FirstOrDefault(t => t.Token == token)?.Sample)
               ?? string.Empty;
    }

    private float ChipWidth(string label)
    {
        theme.ApplyFontScale(SmallFontScale);
        float text = ImGui.CalcTextSize(label).X;
        theme.ApplyFontScale();

        return theme.PadX(0.7f) * 2f + text;
    }
}
