using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Services.Activity;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Cordi.UI.Windows;
using Crovus.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Tabs;

public partial class ActivityTab : ConfigTabBase
{
    private static readonly Dictionary<ActivityType, string[]> PlaceholdersByType = new()
    {
        { ActivityType.Playing, new[] { "{name}", "{details}", "{state}", "{large_image}", "{small_image}", "{elapsed}", "{duration}", "{time_start}", "{time_end}" } },
        { ActivityType.ListeningTo, new[] { "{name}", "{details}", "{track}", "{state}", "{artist}", "{album}", "{large_image}", "{small_image}", "{elapsed}", "{duration}", "{time_start}", "{time_end}" } },
        { ActivityType.Watching, new[] { "{name}", "{details}", "{state}", "{large_image}", "{small_image}", "{elapsed}", "{duration}", "{time_start}", "{time_end}" } },
        { ActivityType.Custom, new[] { "{name}", "{state}", "{large_image}", "{small_image}" } },
    };

    private static readonly string[] DefaultPlaceholders = { "{name}", "{details}", "{state}" };

    private static readonly DropdownItem[] FilterModeItems =
    {
        new() { Key = nameof(FilterMode.Contains), Label = "Contains" },
        new() { Key = nameof(FilterMode.Equals), Label = "Equals" },
        new() { Key = nameof(FilterMode.StartsWith), Label = "Starts With" },
        new() { Key = nameof(FilterMode.EndsWith), Label = "Ends With" },
        new() { Key = nameof(FilterMode.Regex), Label = "Regex" },
    };

    private static readonly DropdownItem[] GradientAnimationItems =
    {
        new() { Key = "0", Label = "Pulse" },
        new() { Key = "1", Label = "Wave" },
        new() { Key = "2", Label = "Static" },
    };

    private const string PlaceholderHelp =
        "Placeholders are swapped for live activity data before the title is sent.\n" +
        "Drag a chip from the list below into any format box to insert it, or type it by hand.\n" +
        "Anything else in the box is kept exactly as written.";

    private const string PlaceholderPayload = "CORDI_ACTIVITY_PLACEHOLDER";

    private static readonly byte[] PlaceholderPayloadData = { 1 };

    private string? draggedPlaceholder;

    private ListPanel? listRenderer;

    private ListPanel Lists => listRenderer ??= new ListPanel(theme);

    private string newGameName = string.Empty;
    private string? editingGame;
    private bool pendingScrollTop;

    public override string Label => "Activity";

    public ActivityTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
    }

    protected override IReadOnlyList<(string Label, Action Draw)> GetSubTabs() => new (string, Action)[]
    {
        ("Overview", DrawOverviewPage),
        ("Playing", DrawPlayingPage),
        ("Listening", DrawListeningPage),
        ("Watching", DrawWatchingPage),
        ("Custom", DrawCustomPage),
    };

    private DiscordActivityConfig Config => plugin.Config.ActivityConfig;

    private static string[] PlaceholdersFor(ActivityType type) =>
        PlaceholdersByType.TryGetValue(type, out var placeholders) ? placeholders : DefaultPlaceholders;

    private static IReadOnlyList<DropdownItem> PlaceholderItems(ActivityType type)
    {
        var placeholders = PlaceholdersFor(type);
        var items = new List<DropdownItem>(placeholders.Length);

        foreach (var placeholder in placeholders)
            items.Add(new DropdownItem { Key = placeholder, Label = placeholder });

        return items;
    }

    private ActivityTypeConfig ConfigFor(ActivityType type, string defaultFormat)
    {
        if (Config.TypeConfigs.TryGetValue(type, out var existing) && existing is not null)
            return existing;

        var created = new ActivityTypeConfig { Enabled = true, Priority = 0, Format = defaultFormat };
        Config.TypeConfigs[type] = created;
        Save();

        return created;
    }

    private void DrawColorRow(
        string id,
        string title,
        float rowWidth,
        Func<Vector3?> get,
        Action<Vector3?> set)
    {
        var current = get();
        bool enabled = current.HasValue;
        var color = current ?? Vector3.One;

        Row.Draw(
            id: id,
            icon: FontAwesomeIcon.Palette,
            iconColor: enabled ? new Vector4(color.X, color.Y, color.Z, 1f) : theme.MutedText,
            title: title,
            subtitle: enabled ? "Overriding the default title colour" : "Using the default title colour",
            controlWidth: 150f,
            drawControl: (pos, width) =>
            {
                float swatchWidth = theme.Scaled(60f);
                var toggleSize = theme.ToggleSize();

                if (enabled)
                {
                    var edited = new Vector4(color.X, color.Y, color.Z, 1f);
                    if (theme.ColorSwatch($"##{id}-swatch", pos, swatchWidth, ref edited))
                    {
                        set(new Vector3(edited.X, edited.Y, edited.Z));
                        Save();
                    }
                }

                bool value = enabled;
                var togglePos = new Vector2(
                    pos.X + width - toggleSize.X,
                    pos.Y + (theme.Scaled(UiTheme.ControlHeight) - toggleSize.Y) * 0.5f);

                if (theme.ToggleSwitch($"##{id}-toggle", togglePos, ref value))
                {
                    set(value ? Vector3.One : null);
                    Save();
                }
            },
            rowWidth: rowWidth);
    }

    private void DrawMissingUserIdNotice()
    {
        if (Config.TargetUserId != 0)
            return;

        Card.Draw(
            "activity-missing-user",
            innerWidth => Row.Draw(
                id: "activity-missing-user-row",
                icon: FontAwesomeIcon.ExclamationTriangle,
                iconColor: UiTheme.TileAmber,
                title: "No Discord account linked",
                subtitle: "Set a Target User ID in Settings, this page stays inactive without it",
                controlWidth: 170f,
                drawControl: (pos, width) =>
                {
                    float buttonHeight = theme.Scaled(32f);
                    ImGui.SetCursorScreenPos(
                        new Vector2(pos.X, pos.Y + (theme.Scaled(UiTheme.ControlHeight) - buttonHeight) * 0.5f));

                    if (theme.SecondaryButton("Open Settings", new Vector2(width, buttonHeight)))
                        plugin.MainConfigWindow.Navigate(PageIds.Settings);
                },
                rowWidth: innerWidth),
            label: "Action Required");
    }

    private void DrawTitlePreview(string id, string label, string title, float width, bool live)
    {
        bool empty = string.IsNullOrWhiteSpace(title);
        int length = title.Length;
        bool overflowing = length > DiscordActivityConfig.MaxTitleLength;

        string shown = overflowing ? ActivityText.Truncate(title, DiscordActivityConfig.MaxTitleLength) : title;
        string counter = $"{length}/{DiscordActivityConfig.MaxTitleLength}";

        var draw = ImGui.GetWindowDrawList();
        float height = theme.Scaled(58f);
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, height);

        draw.AddRectFilled(min, max, ImGui.GetColorU32(theme.RowBg), theme.Radius());
        draw.AddRect(min, max, ImGui.GetColorU32(theme.Border), theme.Radius());

        var accentMin = new Vector2(min.X + theme.Scaled(3f), min.Y + theme.Radius());
        var accentMax = new Vector2(min.X + theme.Scaled(6f), max.Y - theme.Radius());
        draw.AddRectFilled(accentMin, accentMax, ImGui.GetColorU32(live ? UiTheme.TileGreen : theme.Accent), theme.Scaled(1.5f));

        float textX = min.X + theme.PadX(1.4f);

        theme.ApplyFontScale(0.78f);
        using (ImRaii.PushColor(ImGuiCol.Text, theme.FaintText))
        {
            ImGui.SetCursorScreenPos(new Vector2(textX, min.Y + theme.PadY(0.7f)));
            ImGui.TextUnformatted(label.ToUpperInvariant());
        }
        theme.ApplyFontScale();

        using (ImRaii.PushColor(ImGuiCol.Text, empty ? theme.FaintText : theme.Text))
        {
            ImGui.SetCursorScreenPos(new Vector2(textX, min.Y + theme.Scaled(28f)));
            ImGui.TextUnformatted(empty ? "(empty title, nothing is broadcast)" : shown);
        }

        var counterSize = theme.ChipSize(counter);
        theme.ChipAt(
            new Vector2(max.X - theme.PadX(1.1f) - counterSize.X, min.Y + (height - counterSize.Y) * 0.5f),
            counter,
            overflowing ? UiTheme.TileRed : theme.MutedText);

        if (ImGui.IsMouseHoveringRect(min, max) && overflowing)
            theme.Tooltip($"Titles are capped at {DiscordActivityConfig.MaxTitleLength} characters and will be cut off here.");

        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(new Vector2(width, height + theme.Gap(0.4f)));
    }

    private void DrawCountChip(Vector2 rightAnchor, int count, string noun)
    {
        string text = count == 1 ? $"1 {noun}" : $"{count} {noun}s";
        var size = theme.ChipSize(text);
        theme.ChipAt(new Vector2(rightAnchor.X - size.X, rightAnchor.Y), text);
    }
}
