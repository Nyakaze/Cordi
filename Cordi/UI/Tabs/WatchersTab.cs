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
