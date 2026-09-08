using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.Core;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Cordi.UI.Windows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class WatchersTab : ConfigTabBase
{
    private ToggleGrid? toggleGridRenderer;

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

    private void DrawOverlayPanel(
        string idPrefix,
        string windowName,
        Func<ThemedWindow?> window,
        Func<bool> enabled,
        Action<bool> setEnabled,
        Func<ToggleGridEntry[]> flags,
        Func<float> opacity,
        Action<float> setOpacity)
    {
        Card.Draw(
            $"{idPrefix}-overlay",
            innerWidth =>
            {
                DrawToggleRow(
                    $"{idPrefix}-window-enabled",
                    FontAwesomeIcon.WindowMaximize,
                    theme.Accent,
                    "In-game overlay",
                    $"Shows the {windowName} window in game",
                    innerWidth,
                    enabled,
                    value =>
                    {
                        setEnabled(value);

                        var target = window();
                        if (target != null)
                            target.IsOpen = value;

                        plugin.UpdateCommandVisibility();
                    });

                theme.SpacerY(0.4f);

                Toggles.Draw($"{idPrefix}-window-flags", flags(), innerWidth);

                theme.SpacerY(0.4f);

                DrawPercentRow(
                    $"{idPrefix}-opacity",
                    FontAwesomeIcon.Adjust,
                    theme.Accent,
                    "Background opacity",
                    "Transparency of the overlay background",
                    innerWidth,
                    opacity,
                    setOpacity);

                DrawButtonRow(
                    $"{idPrefix}-open-window",
                    FontAwesomeIcon.ExternalLinkAlt,
                    UiTheme.TileBlue,
                    "Open the overlay",
                    $"Brings the {windowName} window up right now",
                    "Open Now",
                    innerWidth,
                    () =>
                    {
                        var target = window();
                        if (target != null)
                            target.IsOpen = true;
                    });
            },
            label: "Overlay");
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

    private void DrawCountChip(Vector2 rightAnchor, int count, string noun)
    {
        string text = count == 1 ? $"1 {noun}" : $"{count} {noun}s";
        var size = theme.ChipSize(text);
        theme.ChipAt(new Vector2(rightAnchor.X - size.X, rightAnchor.Y), text);
    }
}
