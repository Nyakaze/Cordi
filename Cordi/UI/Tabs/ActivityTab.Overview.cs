using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Services.Activity;
using Cordi.UI.Themes;
using Crovus.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Tabs;

public partial class ActivityTab
{
    private void DrawOverviewPage()
    {
        Layout.Draw(
            "Activity Overview",
            "What Cordi is reading from Discord and putting on your title",
            innerWidth => DrawToggleRow(
                "activity-prefix",
                FontAwesomeIcon.QuoteLeft,
                Config.PrefixTitle ? UiTheme.TileTeal : theme.MutedText,
                "Prefix mode",
                Config.PrefixTitle
                    ? "The title is shown above your character name"
                    : "The title is shown below your character name",
                innerWidth,
                () => Config.PrefixTitle,
                value => Config.PrefixTitle = value));

        DrawLiveCard();
        DrawBroadcastCard();
        DrawReplacementsCard();
    }

    private void DrawLiveCard()
    {
        var manager = plugin.ActivityManager;
        var candidate = manager?.CurrentCandidate;
        IReadOnlyList<DiscordActivity> activities = manager?.CurrentPresence?.Activities ?? [];

        Card.Draw(
            "activity-live",
            innerWidth =>
            {
                if (Config.TargetUserId == 0)
                {
                    Row.Draw(
                        id: "activity-live-nouser",
                        icon: FontAwesomeIcon.ExclamationTriangle,
                        iconColor: UiTheme.TileAmber,
                        title: "No Discord account linked",
                        subtitle: "Only the Custom page works until a Target User ID is set in Settings",
                        rowWidth: innerWidth);
                }

                bool customWins = candidate is { Activity: null };

                if (activities.Count == 0 && !customWins)
                {
                    Row.Draw(
                        id: "activity-live-empty",
                        icon: FontAwesomeIcon.Moon,
                        iconColor: theme.MutedText,
                        title: "Nothing detected",
                        subtitle: Config.TargetUserId == 0
                            ? "Cordi is not watching a Discord account"
                            : "The watched account is not reporting any activity",
                        rowWidth: innerWidth);

                    return;
                }

                for (int index = 0; index < activities.Count; index++)
                    DrawActivityRow(activities[index], index, candidate, innerWidth);

                if (customWins)
                    DrawCustomActivityRow(candidate!.Value, innerWidth);
            },
            label: "Live",
            drawTrailing: anchor => theme.HelpPill(
                "activity-live-help",
                anchor,
                "Every activity the watched Discord account reports, in the order Discord sends them.\n" +
                "The highlighted row is the one Cordi picked, decided by priority and by which types you enabled."));
    }

    private void DrawActivityRow(DiscordActivity activity, int index, ActivityCandidate? candidate, float rowWidth)
    {
        bool wins = candidate is { Activity: { } winner } && ReferenceEquals(winner, activity);
        bool supported = Config.TypeConfigs.TryGetValue(activity.Type, out var conf) && conf is not null;
        bool enabled = supported && conf!.Enabled;

        string subtitle = wins
            ? $"{TypeLabel(activity.Type)}  -  priority {candidate!.Value.Priority}"
            : enabled
                ? $"{TypeLabel(activity.Type)}  -  {DescribeActivity(activity)}"
                : $"{TypeLabel(activity.Type)}  -  this type is turned off";

        Row.Draw(
            id: $"activity-live-{index}",
            icon: IconFor(activity.Type),
            iconColor: wins ? UiTheme.TileGreen : enabled ? UiTheme.TileBlue : theme.MutedText,
            title: string.IsNullOrWhiteSpace(activity.Name) ? TypeLabel(activity.Type) : activity.Name,
            subtitle: subtitle,
            rowWidth: rowWidth,
            drawTitleBadge: (pos, lineHeight) => DrawWinnerBadge(pos, lineHeight, wins));
    }

    private void DrawCustomActivityRow(ActivityCandidate candidate, float rowWidth)
    {
        Row.Draw(
            id: "activity-live-custom",
            icon: FontAwesomeIcon.CommentDots,
            iconColor: UiTheme.TileGreen,
            title: "Custom status",
            subtitle: $"Custom  -  priority {candidate.Priority}",
            rowWidth: rowWidth,
            drawTitleBadge: (pos, lineHeight) => DrawWinnerBadge(pos, lineHeight, true));
    }

    private void DrawWinnerBadge(Vector2 pos, float lineHeight, bool wins)
    {
        if (!wins)
            return;

        var size = theme.ChipSize("Selected");
        theme.ChipAt(new Vector2(pos.X, pos.Y + (lineHeight - size.Y) * 0.5f), "Selected", UiTheme.TileGreen);
    }

    private void DrawBroadcastCard()
    {
        var manager = plugin.ActivityManager;
        var candidate = manager?.CurrentCandidate;
        string title = manager?.CurrentTitle ?? string.Empty;

        Card.Draw(
            "activity-broadcast",
            innerWidth =>
            {
                DrawTitlePreview("activity-live-title", "Broadcast title", title, innerWidth, !string.IsNullOrEmpty(title));

                if (candidate is not { } winner)
                    return;

                var conf = winner.Config;

                if (!conf.EnableCycling || conf.CycleFormats is not { Count: > 0 })
                    return;

                theme.SpacerY(0.5f);
                DrawCycleSequence(winner, manager!.CurrentCycleIndex, innerWidth);
            },
            label: "Output",
            drawTrailing: anchor => theme.HelpPill(
                "activity-broadcast-help",
                anchor,
                "The title Cordi is sending to Honorific right now.\n" +
                "When cycling is on, the full rotation is listed with the step that comes next."));
    }

    private void DrawCycleSequence(ActivityCandidate candidate, int cycleIndex, float innerWidth)
    {
        var conf = candidate.Config;
        var formats = conf.CycleFormats!;
        var placeholders = ActivityPlaceholders.From(candidate.Activity);

        int steps = formats.Count + 1;
        int current = cycleIndex < 0 || cycleIndex >= formats.Count ? 0 : cycleIndex + 1;

        theme.ApplyFontScale(0.84f);
        using (ImRaii.PushColor(ImGuiCol.Text, theme.FaintText))
            ImGui.TextUnformatted($"ROTATION  -  {conf.CycleIntervalSeconds}s PER STEP");
        theme.ApplyFontScale();

        theme.SpacerY(0.3f);

        for (int offset = 0; offset < steps; offset++)
        {
            int step = (current + offset) % steps;
            int renderIndex = step - 1;

            string rendered = ActivityTitleRenderer.Render(
                placeholders, conf, renderIndex, Config.Replacements, null);

            string label = offset switch
            {
                0 => "Now",
                1 => "Next",
                _ => $"+{offset}",
            };

            DrawCycleStep(label, rendered, innerWidth, offset == 0);
        }
    }

    private void DrawCycleStep(string label, string title, float width, bool active)
    {
        bool empty = string.IsNullOrWhiteSpace(title);
        int length = title.Length;
        bool overflowing = length > DiscordActivityConfig.MaxTitleLength;

        var draw = ImGui.GetWindowDrawList();
        float height = theme.Scaled(UiTheme.ControlHeight);
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, height);

        if (active)
        {
            draw.AddRectFilled(min, max, ImGui.GetColorU32(theme.AccentSoft), theme.Radius());
            draw.AddRect(min, max, ImGui.GetColorU32(theme.AccentBorder), theme.Radius());
        }
        else
        {
            draw.AddRectFilled(min, max, ImGui.GetColorU32(theme.RowBg), theme.Radius());
        }

        var labelSize = theme.ChipSize(label);
        theme.ChipAt(
            new Vector2(min.X + theme.PadX(0.7f), min.Y + (height - labelSize.Y) * 0.5f),
            label,
            active ? theme.Accent : theme.MutedText);

        string counter = $"{length}/{DiscordActivityConfig.MaxTitleLength}";
        var counterSize = theme.ChipSize(counter);
        theme.ChipAt(
            new Vector2(max.X - theme.PadX(0.7f) - counterSize.X, min.Y + (height - counterSize.Y) * 0.5f),
            counter,
            overflowing ? UiTheme.TileRed : theme.FaintText);

        float textX = min.X + theme.PadX(0.7f) + labelSize.X + theme.Gap(0.8f);
        float textWidth = max.X - theme.PadX(0.7f) - counterSize.X - theme.Gap(0.8f) - textX;

        draw.PushClipRect(new Vector2(textX, min.Y), new Vector2(textX + textWidth, max.Y), true);
        using (ImRaii.PushColor(ImGuiCol.Text, empty ? theme.FaintText : active ? theme.Text : theme.MutedText))
        {
            ImGui.SetCursorScreenPos(new Vector2(textX, min.Y + (height - ImGui.GetTextLineHeight()) * 0.5f));
            ImGui.TextUnformatted(empty ? "(empty step, nothing is shown)" : title);
        }
        draw.PopClipRect();

        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(new Vector2(width, height + theme.Gap(0.4f)));
    }

    private static FontAwesomeIcon IconFor(ActivityType type) => type switch
    {
        ActivityType.ListeningTo => FontAwesomeIcon.Music,
        ActivityType.Watching => FontAwesomeIcon.Video,
        ActivityType.Custom => FontAwesomeIcon.CommentDots,
        _ => FontAwesomeIcon.Gamepad,
    };

    private static string TypeLabel(ActivityType type) => type switch
    {
        ActivityType.ListeningTo => "Listening",
        ActivityType.Watching => "Watching",
        ActivityType.Custom => "Custom",
        _ => "Playing",
    };

    private static string DescribeActivity(DiscordActivity? activity)
    {
        if (activity is null)
            return "Nothing is being reported right now";

        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(activity.Details))
            parts.Add(activity.Details);

        if (!string.IsNullOrWhiteSpace(activity.State))
            parts.Add(activity.State);

        return parts.Count == 0 ? "No details reported" : string.Join("  -  ", parts);
    }

    private void DrawReplacementsCard()
    {
        Card.Draw(
            "activity-replacements",
            innerWidth =>
            {
                theme.PushInputScope();

                var keys = Config.Replacements.Keys.ToList();

                if (keys.Count == 0)
                    ImGui.TextColored(theme.FaintText, "No replacements yet, text is used exactly as Discord reports it.");
                else
                    DrawReplacementHeaders(innerWidth);

                string? removeKey = null;
                string? renameFrom = null;
                string? renameTo = null;

                for (int index = 0; index < keys.Count; index++)
                {
                    var outcome = DrawReplacementRow(keys[index], index, innerWidth);

                    if (outcome.Remove)
                        removeKey = keys[index];

                    if (outcome.RenamedTo != null)
                    {
                        renameFrom = keys[index];
                        renameTo = outcome.RenamedTo;
                    }
                }

                if (renameFrom != null && renameTo != null && !Config.Replacements.ContainsKey(renameTo))
                {
                    var value = Config.Replacements[renameFrom];
                    Config.Replacements.Remove(renameFrom);
                    Config.Replacements[renameTo] = value;
                    Save();
                }

                if (removeKey != null)
                {
                    Config.Replacements.Remove(removeKey);
                    Save();
                }

                theme.SpacerY(0.5f);

                if (theme.SecondaryButton("+ Add Replacement##activity-replacement-add", new Vector2(innerWidth, theme.Scaled(32f))))
                {
                    string key = "New";
                    int suffix = 1;

                    while (Config.Replacements.ContainsKey(key))
                        key = $"New{suffix++}";

                    Config.Replacements[key] = string.Empty;
                    Save();
                }
                theme.HoverHandIfItem();

                theme.PopInputScope();
            },
            label: "Text Replacements",
            drawTrailing: anchor => DrawCountChip(anchor, Config.Replacements.Count, "rule"));
    }

    private void DrawReplacementHeaders(float innerWidth)
    {
        var origin = ImGui.GetCursorScreenPos();
        MeasureReplacementColumns(innerWidth, out float keyWidth, out _, out float gap);

        theme.ApplyFontScale(0.84f);
        using (ImRaii.PushColor(ImGuiCol.Text, theme.FaintText))
        {
            ImGui.SetCursorScreenPos(origin);
            ImGui.TextUnformatted("Original Text");

            ImGui.SetCursorScreenPos(new Vector2(origin.X + keyWidth + gap, origin.Y));
            ImGui.TextUnformatted("Replacement");
        }
        theme.ApplyFontScale();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(innerWidth, ImGui.GetTextLineHeight() + theme.Gap(0.4f)));
    }

    private (bool Remove, string? RenamedTo) DrawReplacementRow(string key, int index, float innerWidth)
    {
        var origin = ImGui.GetCursorScreenPos();
        MeasureReplacementColumns(innerWidth, out float keyWidth, out float valueWidth, out float gap);

        float actionWidth = theme.Scaled(UiTheme.ActionButtonSize);
        float height = theme.Scaled(UiTheme.ControlHeight);

        string editedKey = key;
        string? renamedTo = null;

        theme.TextInput($"##activity-replacement-key-{index}", origin, keyWidth, ref editedKey, 64, "Original text");

        if (ImGui.IsItemDeactivatedAfterEdit() && editedKey != key && !string.IsNullOrWhiteSpace(editedKey))
            renamedTo = editedKey;

        string value = Config.Replacements[key];

        theme.TextInput(
            $"##activity-replacement-value-{index}",
            new Vector2(origin.X + keyWidth + gap, origin.Y),
            valueWidth,
            ref value,
            64,
            "Replacement");

        Config.Replacements[key] = value;

        if (ImGui.IsItemDeactivatedAfterEdit())
            Save();

        bool remove = theme.DeleteAction(
            $"activity-replacement-del-{index}",
            new Vector2(origin.X + innerWidth - actionWidth, origin.Y),
            "Remove replacement",
            actionWidth,
            height);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(innerWidth, height + theme.Gap(0.4f)));

        return (remove, renamedTo);
    }

    private void MeasureReplacementColumns(float innerWidth, out float keyWidth, out float valueWidth, out float gap)
    {
        gap = theme.Gap(0.7f);
        float available = innerWidth - theme.Scaled(UiTheme.ActionButtonSize) - gap * 2f;
        keyWidth = available * 0.5f;
        valueWidth = available - keyWidth;
    }
}
