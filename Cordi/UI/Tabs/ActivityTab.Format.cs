using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.Configuration;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Crovus.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ActivityTab
{
    private void DrawTypePage(
        string id,
        ActivityType type,
        string title,
        string subtitle,
        ActivityTypeConfig conf,
        bool requiresUserId,
        Action? drawLead = null,
        Action? drawExtra = null)
    {
        Layout.Draw(title, subtitle, innerWidth =>
        {
            theme.PushInputScope();

            DrawToggleRow(
                $"{id}-enabled",
                FontAwesomeIcon.PowerOff,
                conf.Enabled ? UiTheme.TileGreen : theme.MutedText,
                "Report this activity",
                conf.Enabled ? "Titles are generated from this activity type" : "This activity type is ignored",
                innerWidth,
                () => conf.Enabled,
                value => conf.Enabled = value);

            DrawNumberRow(
                $"{id}-priority",
                FontAwesomeIcon.SortAmountUp,
                UiTheme.TilePurple,
                "Priority",
                "Higher values win when several activities are active at once",
                innerWidth,
                -99,
                99,
                () => conf.Priority,
                value => conf.Priority = value);

            theme.PopInputScope();
        });

        if (requiresUserId)
            DrawMissingUserIdNotice();

        drawLead?.Invoke();

        DrawFormatCard(id, type, conf);
        DrawLimitsCard(id, type, conf);
        DrawAppearanceCard(id, conf);
        DrawFiltersCard(id, type, conf);

        drawExtra?.Invoke();
    }

    private void DrawFormatCard(string id, ActivityType type, ActivityTypeConfig conf)
    {
        Card.Draw(
            $"{id}-format",
            innerWidth =>
            {
                theme.PushInputScope();

                var origin = ImGui.GetCursorScreenPos();
                string format = conf.Format ?? string.Empty;

                theme.TextInput($"##{id}-format-input", origin, innerWidth, ref format, 128, "Playing {name}");

                bool edited = ImGui.IsItemDeactivatedAfterEdit();
                bool dropped = AcceptPlaceholderDrop(ref format);

                if (!string.Equals(format, conf.Format, StringComparison.Ordinal))
                    conf.Format = format;

                if (edited || dropped)
                    Save();

                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(innerWidth, theme.Scaled(UiTheme.ControlHeight) + theme.Gap(0.6f)));

                DrawPlaceholderChips(id, type, innerWidth);

                theme.SpacerY(0.6f);
                DrawCyclingSection(id, conf, innerWidth);

                theme.PopInputScope();
            },
            label: "Title Format",
            drawTrailing: anchor => theme.HelpPill($"{id}-format-help", anchor, PlaceholderHelp));
    }

    private void DrawPlaceholderChips(string id, ActivityType type, float innerWidth)
    {
        var placeholders = PlaceholdersFor(type);
        var origin = ImGui.GetCursorScreenPos();
        float gap = theme.Gap(0.5f);
        float x = origin.X;
        float y = origin.Y;
        float lineHeight = 0f;

        foreach (var placeholder in placeholders)
        {
            var size = theme.ChipSize(placeholder);

            if (x > origin.X && x + size.X > origin.X + innerWidth)
            {
                x = origin.X;
                y += size.Y + gap;
            }

            var pos = new Vector2(x, y);

            ImGui.SetCursorScreenPos(pos);
            ImGui.InvisibleButton($"##{id}-chip-{placeholder}", size);

            bool hovered = ImGui.IsItemHovered();

            if (hovered)
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

            if (ImGui.BeginDragDropSource())
            {
                draggedPlaceholder = placeholder;
                ImGui.SetDragDropPayload(PlaceholderPayload, PlaceholderPayloadData, ImGuiCond.Always);
                ImGui.TextUnformatted(placeholder);
                ImGui.EndDragDropSource();
            }
            else if (hovered)
            {
                theme.Tooltip("Drag onto a format box to insert this placeholder.");
            }

            theme.ChipAt(pos, placeholder, hovered ? theme.Accent : theme.MutedText);

            x += size.X + gap;
            lineHeight = size.Y;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(innerWidth, y - origin.Y + lineHeight));
    }

    private bool AcceptPlaceholderDrop(ref string value)
    {
        if (!ImGui.BeginDragDropTarget())
            return false;

        bool applied = false;
        var payload = ImGui.AcceptDragDropPayload(PlaceholderPayload, ImGuiDragDropFlags.None);

        if (!payload.IsNull && draggedPlaceholder is { } placeholder)
        {
            value += placeholder;
            draggedPlaceholder = null;
            applied = true;
        }

        ImGui.EndDragDropTarget();

        return applied;
    }

    private void DrawCyclingSection(string id, ActivityTypeConfig conf, float innerWidth)
    {
        DrawToggleRow(
            $"{id}-cycling-enabled",
            FontAwesomeIcon.Sync,
            conf.EnableCycling ? UiTheme.TileTeal : theme.MutedText,
            "Cycle through formats",
            "Rotate between several title formats while the activity runs",
            innerWidth,
            () => conf.EnableCycling,
            value => conf.EnableCycling = value);

        if (!conf.EnableCycling)
            return;

        conf.CycleFormats ??= new List<string>();

        theme.SpacerY(0.4f);
        DrawCycleFormats(id, conf, innerWidth);
        theme.SpacerY(0.4f);

        DrawNumberRow(
            $"{id}-cycle-interval",
            FontAwesomeIcon.Stopwatch,
            UiTheme.TileTeal,
            "Switch interval",
            "Seconds each format stays on screen",
            innerWidth,
            3,
            300,
            () => conf.CycleIntervalSeconds,
            value => conf.CycleIntervalSeconds = value);
    }

    private void DrawCycleFormats(string id, ActivityTypeConfig conf, float innerWidth)
    {
        var formats = conf.CycleFormats!;

        if (formats.Count == 0)
            ImGui.TextColored(theme.FaintText, "No cycle formats yet, the main format is used on its own.");

        int? removeIndex = null;
        float actionWidth = theme.Scaled(UiTheme.ActionButtonSize);
        float height = theme.Scaled(UiTheme.ControlHeight);
        float gap = theme.Gap(0.7f);

        for (int index = 0; index < formats.Count; index++)
        {
            var origin = ImGui.GetCursorScreenPos();
            string value = formats[index] ?? string.Empty;

            theme.TextInput($"##{id}-cycle-{index}", origin, innerWidth - actionWidth - gap, ref value, 128, "Format string");

            bool edited = ImGui.IsItemDeactivatedAfterEdit();
            bool dropped = AcceptPlaceholderDrop(ref value);

            formats[index] = value;

            if (edited || dropped)
                Save();

            if (theme.DeleteAction(
                    $"{id}-cycle-del-{index}",
                    new Vector2(origin.X + innerWidth - actionWidth, origin.Y),
                    "Remove format",
                    actionWidth,
                    height))
                removeIndex = index;

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(innerWidth, height + theme.Gap(0.4f)));
        }

        if (removeIndex is { } target)
        {
            formats.RemoveAt(target);
            Save();
        }

        if (theme.SecondaryButton($"+ Add Format##{id}-cycle-add", new Vector2(innerWidth, theme.Scaled(32f))))
        {
            formats.Add(string.Empty);
            Save();
        }
        theme.HoverHandIfItem();
    }

    private void DrawLimitsCard(string id, ActivityType type, ActivityTypeConfig conf)
    {
        conf.CharLimits ??= new List<CharLimitRule>();

        if (conf.CharLimits.Count == 0 && (conf.TrackLimit > 0 || conf.ArtistLimit > 0))
        {
            if (conf.TrackLimit > 0)
                conf.CharLimits.Add(new CharLimitRule { TargetPlaceholder = "{track}", Limit = conf.TrackLimit });

            if (conf.ArtistLimit > 0)
                conf.CharLimits.Add(new CharLimitRule { TargetPlaceholder = "{artist}", Limit = conf.ArtistLimit });

            conf.TrackLimit = 0;
            conf.ArtistLimit = 0;
            Save();
        }

        var placeholders = PlaceholderItems(type);

        Lists.Draw(
            $"{id}-limits",
            "Character Limits",
            conf.DynamicTrim
                ? $"Soft caps. Placeholders are shortened only as far as needed to fit the {DiscordActivityConfig.MaxTitleLength} character title, and leftover room is handed back to whichever placeholder can use it."
                : $"Hard caps. Each placeholder is cut to its own maximum before the title is built, and the whole title is capped at {DiscordActivityConfig.MaxTitleLength}.",
            conf.CharLimits,
            new[]
            {
                new ListColumn<CharLimitRule>
                {
                    Id = "field",
                    Kind = ListColumnKind.Option,
                    Header = "Field",
                    Options = placeholders,
                    GetText = rule => rule.TargetPlaceholder,
                    SetText = (rule, value) => rule.TargetPlaceholder = value,
                },
                new ListColumn<CharLimitRule>
                {
                    Id = "limit",
                    Kind = ListColumnKind.Number,
                    Header = "Max",
                    FixedWidth = 100f,
                    Min = 0,
                    Max = DiscordActivityConfig.MaxTitleLength,
                    GetNumber = rule => rule.Limit,
                    SetNumber = (rule, value) => rule.Limit = value,
                },
            },
            () => new CharLimitRule { TargetPlaceholder = placeholders[0].Key },
            Save,
            addLabel: "Add Limit",
            emptyText: conf.DynamicTrim
                ? "No limits set, placeholders share the whole title evenly."
                : "No limits set, placeholders are used in full.",
            removeTooltip: "Remove limit",
            drawLead: innerWidth => DrawToggleRow(
                $"{id}-dynamic-trim",
                FontAwesomeIcon.ArrowsAltH,
                conf.DynamicTrim ? UiTheme.TileTeal : theme.MutedText,
                "Dynamic trim",
                conf.DynamicTrim
                    ? "Static text is never cut, spare characters go to the placeholders that need them"
                    : "Every maximum is enforced exactly as written",
                innerWidth,
                () => conf.DynamicTrim,
                value => conf.DynamicTrim = value));
    }

    private void DrawFiltersCard(string id, ActivityType type, ActivityTypeConfig conf)
    {
        conf.Filters ??= new List<FilterRule>();

        var placeholders = PlaceholderItems(type);

        Lists.Draw(
            $"{id}-filters",
            "Blacklist Filters",
            "Activities matching any rule below are ignored completely.",
            conf.Filters,
            new[]
            {
                new ListColumn<FilterRule>
                {
                    Id = "field",
                    Kind = ListColumnKind.Option,
                    Header = "Field",
                    FixedWidth = 130f,
                    Options = placeholders,
                    GetText = rule => rule.TargetPlaceholder,
                    SetText = (rule, value) => rule.TargetPlaceholder = value,
                },
                new ListColumn<FilterRule>
                {
                    Id = "mode",
                    Kind = ListColumnKind.Option,
                    Header = "Mode",
                    FixedWidth = 130f,
                    Options = FilterModeItems,
                    GetText = rule => rule.Mode.ToString(),
                    SetText = (rule, value) =>
                    {
                        if (Enum.TryParse<FilterMode>(value, out var mode))
                            rule.Mode = mode;
                    },
                },
                new ListColumn<FilterRule>
                {
                    Id = "value",
                    Header = "Value",
                    Hint = "Text to match",
                    GetText = rule => rule.Value,
                    SetText = (rule, value) => rule.Value = value,
                },
            },
            () => new FilterRule { TargetPlaceholder = placeholders[0].Key },
            Save,
            addLabel: "Add Filter",
            emptyText: "No filters yet, every activity of this type is accepted.",
            removeTooltip: "Remove filter",
            drawTrailing: anchor => DrawCountChip(anchor, conf.Filters.Count, "rule"));
    }

    private void DrawAppearanceCard(string id, ActivityTypeConfig conf)
    {
        Card.Draw(
            $"{id}-appearance",
            innerWidth =>
            {
                theme.PushInputScope();

                DrawColorRow(
                    $"{id}-color",
                    "Title colour",
                    innerWidth,
                    () => conf.Color,
                    value => conf.Color = value);

                DrawColorRow(
                    $"{id}-glow",
                    "Title glow",
                    innerWidth,
                    () => conf.Glow,
                    value => conf.Glow = value);

                bool gradient = conf.GradientColourSet.HasValue;

                DrawToggleRow(
                    $"{id}-gradient",
                    FontAwesomeIcon.Rainbow,
                    gradient ? UiTheme.TilePink : theme.MutedText,
                    "Gradient title",
                    "Animate the title through a Honorific gradient set",
                    innerWidth,
                    () => conf.GradientColourSet.HasValue,
                    value =>
                    {
                        conf.GradientColourSet = value ? 0 : null;
                        conf.GradientAnimationStyle = value ? 0 : null;
                    });

                if (!conf.GradientColourSet.HasValue)
                {
                    theme.PopInputScope();
                    return;
                }

                DrawNumberRow(
                    $"{id}-gradient-set",
                    FontAwesomeIcon.Swatchbook,
                    UiTheme.TilePink,
                    "Colour set",
                    "Honorific gradient index, -1 uses the title colour and glow above",
                    innerWidth,
                    -1,
                    99,
                    () => conf.GradientColourSet ?? 0,
                    value => conf.GradientColourSet = value);

                Row.Draw(
                    id: $"{id}-gradient-anim",
                    icon: FontAwesomeIcon.Wind,
                    iconColor: UiTheme.TilePink,
                    title: "Animation",
                    subtitle: "How the gradient moves across the title",
                    controlWidth: 150f,
                    drawControl: (pos, width) =>
                    {
                        ImGui.SetCursorScreenPos(pos);
                        theme.OptionPicker(
                            $"{id}-gradient-anim-picker",
                            (conf.GradientAnimationStyle ?? 0).ToString(),
                            GradientAnimationItems,
                            key =>
                            {
                                if (int.TryParse(key, out var style))
                                {
                                    conf.GradientAnimationStyle = style;
                                    Save();
                                }
                            },
                            width: width);
                    },
                    rowWidth: innerWidth);

                theme.PopInputScope();
            },
            label: "Appearance");
    }
}
