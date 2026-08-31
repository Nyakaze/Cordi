using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.Core;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class WatchersTab : ConfigTabBase
{
    private SettingsRow? rowRenderer;
    private PageHeader? layoutRenderer;
    private Panel? panelRenderer;
    private ToggleGrid? toggleGridRenderer;

    private SettingsRow Row => rowRenderer ??= new SettingsRow(theme);
    private PageHeader Layout => layoutRenderer ??= new PageHeader(theme);
    private Panel Card => panelRenderer ??= new Panel(theme);
    private ToggleGrid Toggles => toggleGridRenderer ??= new ToggleGrid(theme);

    public override string Label => "Watchers";

    public WatchersTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
    }

    protected override IReadOnlyList<(string Label, Action Draw)> GetSubTabs() => new (string, Action)[]
    {
        ("Peeper", DrawPeeperPage),
        ("Emote Log", DrawEmoteLogPage),
        ("Combined Overlay", DrawCombinedOverlayPage),
    };

    private void Save() => plugin.Config.Save();

    private ToggleGridEntry Flag(string id, string label, Func<bool> get, Action<bool> set, string tooltip = "") =>
        new()
        {
            Id = id,
            Label = label,
            Value = get(),
            Tooltip = tooltip,
            OnToggle = value =>
            {
                set(value);
                Save();
            },
        };

    private void DrawToggleRow(
        string id,
        FontAwesomeIcon icon,
        Vector4 iconColor,
        string title,
        string subtitle,
        float rowWidth,
        Func<bool> get,
        Action<bool> set)
    {
        bool value = get();

        var result = Row.Draw(
            id: id,
            icon: icon,
            iconColor: iconColor,
            title: title,
            subtitle: subtitle,
            toggleValue: value,
            onToggle: newValue =>
            {
                set(newValue);
                Save();
            },
            rowWidth: rowWidth);

        if (result.RowClicked && !result.ToggleChanged)
        {
            set(!value);
            Save();
        }
    }

    private void DrawSliderRow(
        string id,
        FontAwesomeIcon icon,
        Vector4 iconColor,
        string title,
        string subtitle,
        float rowWidth,
        float min,
        float max,
        float step,
        Func<float> get,
        Action<float> set,
        Func<float, string> format)
    {
        Row.Draw(
            id: id,
            icon: icon,
            iconColor: iconColor,
            title: title,
            subtitle: subtitle,
            controlWidth: 240f,
            drawControl: (pos, width) =>
            {
                float value = get();
                string valueText = format(value);
                var chipSize = theme.ChipSize(valueText);
                float sliderWidth = MathF.Max(theme.Scaled(60f), width - chipSize.X - theme.Gap());

                if (theme.RangeSlider($"##{id}-slider", pos, sliderWidth, ref value, min, max, step))
                {
                    set(value);
                    Save();
                }

                theme.ChipAt(
                    new Vector2(
                        pos.X + sliderWidth + theme.Gap(),
                        pos.Y + (theme.Scaled(UiTheme.ControlHeight) - chipSize.Y) * 0.5f),
                    valueText);
            },
            rowWidth: rowWidth);
    }

    private void DrawPercentRow(
        string id,
        FontAwesomeIcon icon,
        Vector4 iconColor,
        string title,
        string subtitle,
        float rowWidth,
        Func<float> get,
        Action<float> set) =>
        DrawSliderRow(
            id, icon, iconColor, title, subtitle, rowWidth,
            0f, 100f, 1f,
            () => get() * 100f,
            value => set(value / 100f),
            value => $"{value:F0}%");

    private void DrawColorRow(
        string id,
        string title,
        string subtitle,
        float rowWidth,
        Func<Vector4> get,
        Action<Vector4> set)
    {
        var color = get();

        Row.Draw(
            id: id,
            icon: FontAwesomeIcon.Palette,
            iconColor: color,
            title: title,
            subtitle: subtitle,
            controlWidth: 90f,
            drawControl: (pos, width) =>
            {
                var edited = color;
                if (theme.ColorSwatch($"##{id}-swatch", pos, width, ref edited))
                {
                    set(edited);
                    Save();
                }
            },
            rowWidth: rowWidth);
    }

    private void DrawChannelRow(
        string id,
        string title,
        string currentChannelId,
        float rowWidth,
        Action<string> onSelect)
    {
        bool hasChannel = !string.IsNullOrEmpty(currentChannelId);

        Row.Draw(
            id: id,
            icon: hasChannel ? FontAwesomeIcon.Hashtag : FontAwesomeIcon.ExclamationTriangle,
            iconColor: hasChannel ? UiTheme.TileGreen : UiTheme.TileAmber,
            title: title,
            subtitle: hasChannel ? "Notifications are delivered here" : "No channel selected yet",
            controlWidth: 240f,
            drawControl: (pos, width) =>
            {
                ImGui.SetCursorScreenPos(pos);
                theme.ChannelPicker(
                    id,
                    currentChannelId,
                    plugin.Channels.TextChannels,
                    onSelect,
                    defaultLabel: "None",
                    showLabel: false,
                    width: width);
            },
            rowWidth: rowWidth);
    }

    private void DrawButtonRow(
        string id,
        FontAwesomeIcon icon,
        Vector4 iconColor,
        string title,
        string subtitle,
        string buttonLabel,
        float rowWidth,
        Action onClick,
        float controlWidth = 150f)
    {
        Row.Draw(
            id: id,
            icon: icon,
            iconColor: iconColor,
            title: title,
            subtitle: subtitle,
            controlWidth: controlWidth,
            drawControl: (pos, width) =>
            {
                float buttonHeight = theme.Scaled(32f);
                ImGui.SetCursorScreenPos(
                    new Vector2(pos.X, pos.Y + (theme.Scaled(UiTheme.ControlHeight) - buttonHeight) * 0.5f));

                if (theme.SecondaryButton(buttonLabel, new Vector2(width, buttonHeight)))
                    onClick();
            },
            rowWidth: rowWidth);
    }

    private void DrawCountChip(Vector2 rightAnchor, int count, string noun)
    {
        string text = count == 1 ? $"1 {noun}" : $"{count} {noun}s";
        var size = theme.ChipSize(text);
        theme.ChipAt(new Vector2(rightAnchor.X - size.X, rightAnchor.Y), text);
    }
}
