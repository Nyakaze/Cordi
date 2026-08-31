using System;
using System.Numerics;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Components;

public readonly struct SettingsRowResult
{
    public bool RowClicked { get; init; }
    public bool ToggleChanged { get; init; }
    public bool ChevronClicked { get; init; }
}

public sealed class SettingsRow
{
    private readonly UiTheme theme;

    public SettingsRow(UiTheme theme)
    {
        this.theme = theme;
    }

    public SettingsRowResult Draw(
        string id,
        FontAwesomeIcon icon,
        Vector4 iconColor,
        string title,
        string subtitle,
        float controlWidth = 0f,
        Action<Vector2, float>? drawControl = null,
        bool? toggleValue = null,
        Action<bool>? onToggle = null,
        bool showChevron = false,
        string toggleTooltip = "",
        float rowWidth = 0f,
        float rowHeight = 0f,
        IDalamudTextureWrap? iconTexture = null,
        Action<Vector2, float>? drawTitleBadge = null,
        bool toggleEnabled = true,
        string toggleDisabledTooltip = "")
    {
        var draw = ImGui.GetWindowDrawList();
        float height = rowHeight > 0f ? theme.Scaled(rowHeight) : theme.Scaled(UiTheme.SettingsRowHeight);
        float width = rowWidth > 0f ? rowWidth : ImGui.GetContentRegionAvail().X;
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, height);

        bool rowClicked = ImGui.InvisibleButton($"##row-{id}", new Vector2(width, height));
        ImGui.SetItemAllowOverlap();
        bool hovered = ImGui.IsItemHovered();
        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        draw.AddRectFilled(min, max, ImGui.GetColorU32(hovered ? theme.RowHover : theme.RowBg), theme.Radius());

        float tile = theme.Scaled(UiTheme.IconTileSize);
        var tileMin = new Vector2(min.X + theme.PadX(0.8f), min.Y + (height - tile) * 0.5f);
        var tileBg = new Vector4(iconColor.X, iconColor.Y, iconColor.Z, 0.16f);
        draw.AddRectFilled(tileMin, tileMin + new Vector2(tile, tile), ImGui.GetColorU32(tileBg), theme.Radius());

        if (iconTexture != null)
        {
            draw.AddImageRounded(
                iconTexture.Handle,
                tileMin,
                tileMin + new Vector2(tile, tile),
                Vector2.Zero,
                Vector2.One,
                0xFFFFFFFF,
                theme.Radius());
        }
        else
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            using (ImRaii.PushColor(ImGuiCol.Text, iconColor))
            {
                var glyph = icon.ToIconString();
                var size = ImGui.CalcTextSize(glyph);
                ImGui.SetCursorScreenPos(tileMin + new Vector2((tile - size.X) * 0.5f, (tile - size.Y) * 0.5f));
                ImGui.TextUnformatted(glyph);
            }
        }

        float textX = tileMin.X + tile + theme.Gap(1.2f);
        bool hasSubtitle = !string.IsNullOrEmpty(subtitle);
        var titleSize = ImGui.CalcTextSize(title);
        float titleTop;

        if (hasSubtitle)
        {
            float lineHeight = ImGui.GetTextLineHeight();
            float blockHeight = lineHeight * 1.86f + theme.Scaled(4f);
            titleTop = min.Y + (height - blockHeight) * 0.5f;

            ImGui.SetCursorScreenPos(new Vector2(textX, titleTop));
            ImGui.TextUnformatted(title);

            theme.ApplyFontScale(0.86f);
            ImGui.SetCursorScreenPos(new Vector2(textX, titleTop + lineHeight + theme.Scaled(4f)));
            using (ImRaii.PushColor(ImGuiCol.Text, theme.MutedText))
                ImGui.TextUnformatted(subtitle);
            theme.ApplyFontScale();
        }
        else
        {
            titleTop = min.Y + (height - titleSize.Y) * 0.5f;

            ImGui.SetCursorScreenPos(new Vector2(textX, titleTop));
            ImGui.TextUnformatted(title);
        }

        drawTitleBadge?.Invoke(new Vector2(textX + titleSize.X + theme.Gap(0.8f), titleTop), titleSize.Y);

        float cursorRight = max.X - theme.PadX(0.8f);
        bool chevronClicked = false;

        if (showChevron)
        {
            float chevronWidth = theme.Scaled(16f);
            var chevronMin = new Vector2(cursorRight - chevronWidth, min.Y);

            ImGui.SetCursorScreenPos(chevronMin);
            chevronClicked = ImGui.InvisibleButton($"##row-chevron-{id}", new Vector2(chevronWidth, height));
            bool chevronHovered = ImGui.IsItemHovered();
            if (chevronHovered)
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

            using (ImRaii.PushFont(UiBuilder.IconFont))
            using (ImRaii.PushColor(ImGuiCol.Text, chevronHovered ? theme.Text : theme.FaintText))
            {
                var glyph = FontAwesomeIcon.ChevronRight.ToIconString();
                var size = ImGui.CalcTextSize(glyph);
                ImGui.SetCursorScreenPos(new Vector2(chevronMin.X + (chevronWidth - size.X) * 0.5f, min.Y + (height - size.Y) * 0.5f));
                ImGui.TextUnformatted(glyph);
            }

            cursorRight = chevronMin.X - theme.Gap();
        }

        bool toggleChanged = false;

        if (toggleValue.HasValue)
        {
            float toggleWidth = theme.Scaled(38f);
            float toggleHeight = theme.Scaled(20f);
            var togglePos = new Vector2(cursorRight - toggleWidth, min.Y + (height - toggleHeight) * 0.5f);

            bool value = toggleValue.Value;
            bool toggled = DrawToggle($"##row-toggle-{id}", togglePos, toggleWidth, toggleHeight, ref value, toggleEnabled);

            string tooltip = toggleEnabled ? toggleTooltip : toggleDisabledTooltip;
            if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
                ImGui.SetTooltip(tooltip);

            if (toggled)
            {
                toggleChanged = true;
                onToggle?.Invoke(value);
            }

            cursorRight = togglePos.X - theme.Gap(1.5f);
        }

        if (drawControl != null)
        {
            float actualWidth = controlWidth > 0 ? theme.Scaled(controlWidth) : theme.Scaled(180f);
            float controlHeight = ImGui.GetFrameHeight();
            var controlPos = new Vector2(cursorRight - actualWidth, min.Y + (height - controlHeight) * 0.5f);
            ImGui.SetCursorScreenPos(controlPos);
            drawControl(controlPos, actualWidth);
        }

        ImGui.SetCursorScreenPos(new Vector2(min.X, max.Y + theme.Gap(0.4f)));

        return new SettingsRowResult
        {
            RowClicked = rowClicked,
            ToggleChanged = toggleChanged,
            ChevronClicked = chevronClicked,
        };
    }

    public bool DrawToggle(string id, Vector2 pos, float width, float height, ref bool value, bool enabled = true)
    {
        var draw = ImGui.GetWindowDrawList();

        ImGui.SetCursorScreenPos(pos);
        bool clicked = ImGui.InvisibleButton(id, new Vector2(width, height)) && enabled;
        bool hovered = ImGui.IsItemHovered() && enabled;
        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        if (clicked)
            value = !value;

        float radius = height * 0.5f;
        float alpha = enabled ? 1f : 0.4f;
        var max = pos + new Vector2(width, height);

        var track = value
            ? new Vector4(theme.Accent.X, theme.Accent.Y, theme.Accent.Z, hovered ? 0.32f : 0.22f)
            : hovered ? theme.FrameBgHover : theme.FrameBg;

        var border = value ? theme.Accent : theme.Border;
        var knob = value ? theme.Accent : theme.MutedText;

        draw.AddRectFilled(pos, max, Faded(track, alpha), radius);
        draw.AddRect(pos, max, Faded(border, alpha), radius);

        float knobRadius = radius - theme.Scaled(4f);
        float knobX = value ? max.X - radius : pos.X + radius;
        draw.AddCircleFilled(new Vector2(knobX, pos.Y + radius), knobRadius, Faded(knob, alpha));

        return clicked;
    }

    private static uint Faded(Vector4 color, float alpha) =>
        ImGui.GetColorU32(new Vector4(color.X, color.Y, color.Z, color.W * alpha));
}
