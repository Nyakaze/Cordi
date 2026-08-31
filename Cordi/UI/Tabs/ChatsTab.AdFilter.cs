using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Services.Discord;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Cordi.UI.Windows;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Tabs;

public partial class ChatsTab
{
    private static readonly IReadOnlyList<DropdownItem> PatternKindItems = new[]
    {
        new DropdownItem { Key = nameof(FilterPatternKind.Keyword), Label = "Keyword" },
        new DropdownItem { Key = nameof(FilterPatternKind.Regex), Label = "Regex" },
    };

    private static readonly IReadOnlyList<DropdownItem> PatternWeightItems = new[]
    {
        new DropdownItem { Key = nameof(FilterPatternWeight.High), Label = "High  +2" },
        new DropdownItem { Key = nameof(FilterPatternWeight.Medium), Label = "Medium  +1" },
    };

    private string patternSearch = string.Empty;
    private string newPatternValue = string.Empty;
    private FilterPatternKind newPatternKind = FilterPatternKind.Keyword;
    private FilterPatternWeight newPatternWeight = FilterPatternWeight.High;
    private string newWhitelistValue = string.Empty;
    private string filterTestMessage = string.Empty;

    private void DrawAdvertisementFilterPage(AdvertisementFilterConfig config)
    {
        Layout.Draw(
            "Advertisement Filter",
            "Score-based filtering for advertisement spam",
            innerWidth => DrawFilterScopeRow(innerWidth));

        DrawDetectionPanel(config);
        DrawPatternsPanel(config);
        DrawWhitelistPanel(config);
        DrawFilterTestPanel(config);
    }

    private void DrawFilterScopeRow(float rowWidth)
    {
        int active = plugin.Config.Chat.Mappings.Count(m =>
            m.EnableAdvertisementFilter && !string.IsNullOrEmpty(m.DiscordChannelId));

        string subtitle = active == 0
            ? "No chat type uses the filter yet"
            : active == 1 ? "Active on 1 chat type" : $"Active on {active} chat types";

        Row.Draw(
            id: "ad-filter-scope",
            icon: active == 0 ? FontAwesomeIcon.ExclamationTriangle : FontAwesomeIcon.Filter,
            iconColor: active == 0 ? UiTheme.TileAmber : UiTheme.TileGreen,
            title: "Filtered chat types",
            subtitle: subtitle,
            controlWidth: 210f,
            drawControl: (pos, width) =>
            {
                float buttonHeight = theme.Scaled(32f);
                ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + (theme.Scaled(UiTheme.ControlHeight) - buttonHeight) * 0.5f));

                if (theme.SecondaryButton("Open Channel Mappings", new Vector2(width, buttonHeight)))
                    plugin.MainConfigWindow.Navigate(PageIds.ChannelMappings);
            },
            rowWidth: rowWidth);
    }

    private void DrawDetectionPanel(AdvertisementFilterConfig config)
    {
        Card.Draw(
            "ad-filter-detection",
            innerWidth =>
            {
                Row.Draw(
                    id: "ad-filter-threshold",
                    icon: FontAwesomeIcon.Crosshairs,
                    iconColor: theme.Accent,
                    title: "Detection threshold",
                    subtitle: DescribeThreshold(config.ScoreThreshold),
                    controlWidth: 240f,
                    drawControl: (pos, width) =>
                    {
                        string valueText = config.ScoreThreshold.ToString();
                        var chipSize = theme.ChipSize(valueText);
                        float sliderWidth = width - chipSize.X - theme.Gap();

                        int threshold = config.ScoreThreshold;
                        if (theme.ScoreSlider("##ad-threshold", pos, sliderWidth, ref threshold, 1, 10))
                        {
                            config.ScoreThreshold = threshold;
                            plugin.Config.Save();
                        }

                        theme.ChipAt(
                            new Vector2(pos.X + sliderWidth + theme.Gap(), pos.Y + (theme.Scaled(UiTheme.ControlHeight) - chipSize.Y) * 0.5f),
                            valueText);
                    },
                    rowWidth: innerWidth);

                theme.SpacerY(0.4f);
                DrawScoringLegend(innerWidth);
            },
            label: "Detection",
            drawTrailing: DrawFilterHelpPill);
    }

    private static string DescribeThreshold(int threshold) => threshold switch
    {
        <= 2 => "Very strict, expect false positives",
        <= 4 => "Balanced, recommended",
        <= 6 => "Relaxed, only obvious ads",
        _ => "Very permissive, rarely blocks",
    };

    private void DrawScoringLegend(float innerWidth)
    {
        var entries = new (string Text, Vector4 Color)[]
        {
            ("High pattern  +2", UiTheme.TileRed),
            ("Medium pattern  +1", UiTheme.TileAmber),
            ("3+ high keywords  +2", UiTheme.TileRed),
            ("2+ medium keywords  +1", UiTheme.TileAmber),
            ("5+ shouted words  +1", UiTheme.TileBlue),
            ("Symbol spam  +1", UiTheme.TileTeal),
        };

        var start = ImGui.GetCursorScreenPos();
        float x = start.X;
        float y = start.Y;
        float lineHeight = 0f;

        foreach (var (text, color) in entries)
        {
            var size = theme.ChipSize(text);

            if (x > start.X && x + size.X > start.X + innerWidth)
            {
                x = start.X;
                y += size.Y + theme.Gap(0.5f);
            }

            theme.ChipAt(new Vector2(x, y), text, color);
            x += size.X + theme.Gap(0.5f);
            lineHeight = size.Y;
        }

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(innerWidth, y - start.Y + lineHeight));
    }

    private void DrawPatternsPanel(AdvertisementFilterConfig config)
    {
        Card.Draw(
            "ad-filter-patterns",
            innerWidth =>
            {
                DrawPatternSearchBar(innerWidth);
                theme.SpacerY(0.5f);

                var visible = config.Patterns
                    .Select((pattern, index) => (Pattern: pattern, Index: index))
                    .Where(entry => string.IsNullOrWhiteSpace(patternSearch)
                                    || entry.Pattern.Value.Contains(patternSearch, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (visible.Count == 0)
                {
                    ImGui.TextDisabled(config.Patterns.Count == 0
                        ? "No patterns yet. Add one below."
                        : "No pattern matches your search.");
                }

                int removeAt = -1;
                foreach (var entry in visible)
                {
                    if (DrawPatternRow(entry.Pattern, entry.Index, innerWidth))
                        removeAt = entry.Index;
                }

                if (removeAt >= 0)
                {
                    config.Patterns.RemoveAt(removeAt);
                    plugin.Config.Save();
                }

                theme.SpacerY(0.5f);
                DrawAddPatternRow(config, innerWidth);
            },
            label: "Patterns",
            drawTrailing: anchor => DrawCountChip(anchor, config.Patterns.Count, "pattern"));
    }

    private void DrawPatternSearchBar(float innerWidth)
    {
        var pos = ImGui.GetCursorScreenPos();
        float width = MathF.Min(innerWidth, theme.Scaled(320f));

        string search = patternSearch;
        if (theme.TextInput("##ad-pattern-search", pos, width, ref search, 128, "Search patterns..."))
            patternSearch = search;

        ImGui.SetCursorScreenPos(pos);
        ImGui.Dummy(new Vector2(innerWidth, theme.Scaled(UiTheme.ControlHeight)));
    }

    private bool DrawPatternRow(FilterPattern pattern, int index, float rowWidth)
    {
        var pos = ImGui.GetCursorScreenPos();
        float height = theme.Scaled(UiTheme.ControlHeight);
        float gap = theme.Gap(0.7f);
        float deleteWidth = theme.Scaled(UiTheme.ActionButtonSize);
        float kindWidth = theme.Scaled(110f);
        float weightWidth = theme.Scaled(120f);
        float valueWidth = MathF.Max(theme.Scaled(120f), rowWidth - kindWidth - weightWidth - deleteWidth - gap * 3f);

        bool validRegex = pattern.Kind != FilterPatternKind.Regex || AdvertisementFilter.IsValidRegex(pattern.Value);

        ImGui.SetCursorScreenPos(pos);
        theme.OptionPicker(
            $"ad-pattern-kind-{index}",
            pattern.Kind.ToString(),
            PatternKindItems,
            key =>
            {
                pattern.Kind = Enum.Parse<FilterPatternKind>(key);
                plugin.Config.Save();
            },
            width: kindWidth);

        float weightX = pos.X + kindWidth + gap;
        ImGui.SetCursorScreenPos(new Vector2(weightX, pos.Y));
        theme.OptionPicker(
            $"ad-pattern-weight-{index}",
            pattern.Weight.ToString(),
            PatternWeightItems,
            key =>
            {
                pattern.Weight = Enum.Parse<FilterPatternWeight>(key);
                plugin.Config.Save();
            },
            width: weightWidth);

        float valueX = weightX + weightWidth + gap;
        string value = pattern.Value;
        theme.TextInput($"##ad-pattern-value-{index}", new Vector2(valueX, pos.Y), valueWidth, ref value, 256);
        pattern.Value = value;

        if (ImGui.IsItemDeactivatedAfterEdit())
            plugin.Config.Save();

        if (!validRegex)
        {
            var draw = ImGui.GetWindowDrawList();
            draw.AddRect(
                new Vector2(valueX, pos.Y),
                new Vector2(valueX + valueWidth, pos.Y + height),
                ImGui.GetColorU32(UiTheme.TileRed),
                theme.Radius());

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Invalid regular expression, this pattern never matches");
        }

        bool remove = theme.DeleteAction(
            $"ad-pattern-del-{index}",
            new Vector2(valueX + valueWidth + gap, pos.Y),
            "Remove pattern",
            deleteWidth,
            height);

        ImGui.SetCursorScreenPos(pos);
        ImGui.Dummy(new Vector2(rowWidth, height + theme.Gap(0.4f)));

        return remove;
    }

    private void DrawAddPatternRow(AdvertisementFilterConfig config, float rowWidth)
    {
        var pos = ImGui.GetCursorScreenPos();
        float height = theme.Scaled(UiTheme.ControlHeight);
        float gap = theme.Gap(0.7f);
        float addWidth = theme.Scaled(90f);
        float kindWidth = theme.Scaled(110f);
        float weightWidth = theme.Scaled(120f);
        float valueWidth = MathF.Max(theme.Scaled(120f), rowWidth - kindWidth - weightWidth - addWidth - gap * 3f);

        ImGui.SetCursorScreenPos(pos);
        theme.OptionPicker(
            "ad-new-pattern-kind",
            newPatternKind.ToString(),
            PatternKindItems,
            key => newPatternKind = Enum.Parse<FilterPatternKind>(key),
            width: kindWidth);

        float weightX = pos.X + kindWidth + gap;
        ImGui.SetCursorScreenPos(new Vector2(weightX, pos.Y));
        theme.OptionPicker(
            "ad-new-pattern-weight",
            newPatternWeight.ToString(),
            PatternWeightItems,
            key => newPatternWeight = Enum.Parse<FilterPatternWeight>(key),
            width: weightWidth);

        float valueX = weightX + weightWidth + gap;
        string value = newPatternValue;
        bool submitted = theme.TextInput(
            "##ad-new-pattern-value",
            new Vector2(valueX, pos.Y),
            valueWidth,
            ref value,
            256,
            newPatternKind == FilterPatternKind.Regex ? "New regex..." : "New keyword...");
        newPatternValue = value;

        submitted = submitted && ImGui.IsKeyPressed(ImGuiKey.Enter);

        ImGui.SetCursorScreenPos(new Vector2(valueX + valueWidth + gap, pos.Y + (height - theme.Scaled(32f)) * 0.5f));
        if (theme.PrimaryButton("Add##ad-pattern", new Vector2(addWidth, theme.Scaled(32f))) || submitted)
            AddPattern(config);

        ImGui.SetCursorScreenPos(pos);
        ImGui.Dummy(new Vector2(rowWidth, height));
    }

    private void AddPattern(AdvertisementFilterConfig config)
    {
        if (string.IsNullOrWhiteSpace(newPatternValue))
            return;

        config.Patterns.Add(new FilterPattern
        {
            Value = newPatternValue.Trim(),
            Kind = newPatternKind,
            Weight = newPatternWeight,
        });

        newPatternValue = string.Empty;
        plugin.Config.Save();
    }

    private void DrawWhitelistPanel(AdvertisementFilterConfig config)
    {
        Card.Draw(
            "ad-filter-whitelist",
            innerWidth =>
            {
                ImGui.TextDisabled("Messages containing one of these phrases are never filtered.");
                theme.SpacerY(0.5f);

                int removeAt = -1;
                for (int i = 0; i < config.Whitelist.Count; i++)
                {
                    if (DrawWhitelistRow(config, i, innerWidth))
                        removeAt = i;
                }

                if (removeAt >= 0)
                {
                    config.Whitelist.RemoveAt(removeAt);
                    plugin.Config.Save();
                }

                theme.SpacerY(0.5f);
                DrawAddWhitelistRow(config, innerWidth);
            },
            label: "Whitelist",
            drawTrailing: anchor => DrawCountChip(anchor, config.Whitelist.Count, "phrase"));
    }

    private bool DrawWhitelistRow(AdvertisementFilterConfig config, int index, float rowWidth)
    {
        var pos = ImGui.GetCursorScreenPos();
        float height = theme.Scaled(UiTheme.ControlHeight);
        float gap = theme.Gap(0.7f);
        float deleteWidth = theme.Scaled(UiTheme.ActionButtonSize);
        float valueWidth = MathF.Max(theme.Scaled(120f), rowWidth - deleteWidth - gap);

        string value = config.Whitelist[index];
        theme.TextInput($"##ad-whitelist-{index}", pos, valueWidth, ref value, 256);
        config.Whitelist[index] = value;

        if (ImGui.IsItemDeactivatedAfterEdit())
            plugin.Config.Save();

        bool remove = theme.DeleteAction(
            $"ad-whitelist-del-{index}",
            new Vector2(pos.X + valueWidth + gap, pos.Y),
            "Remove phrase",
            deleteWidth,
            height);

        ImGui.SetCursorScreenPos(pos);
        ImGui.Dummy(new Vector2(rowWidth, height + theme.Gap(0.4f)));

        return remove;
    }

    private void DrawAddWhitelistRow(AdvertisementFilterConfig config, float rowWidth)
    {
        var pos = ImGui.GetCursorScreenPos();
        float height = theme.Scaled(UiTheme.ControlHeight);
        float gap = theme.Gap(0.7f);
        float addWidth = theme.Scaled(90f);
        float valueWidth = MathF.Max(theme.Scaled(120f), rowWidth - addWidth - gap);

        string value = newWhitelistValue;
        bool submitted = theme.TextInput("##ad-new-whitelist", pos, valueWidth, ref value, 256, "New phrase...");
        newWhitelistValue = value;

        submitted = submitted && ImGui.IsKeyPressed(ImGuiKey.Enter);

        ImGui.SetCursorScreenPos(new Vector2(pos.X + valueWidth + gap, pos.Y + (height - theme.Scaled(32f)) * 0.5f));
        if ((theme.PrimaryButton("Add##ad-whitelist", new Vector2(addWidth, theme.Scaled(32f))) || submitted)
            && !string.IsNullOrWhiteSpace(newWhitelistValue))
        {
            config.Whitelist.Add(newWhitelistValue.Trim());
            newWhitelistValue = string.Empty;
            plugin.Config.Save();
        }

        ImGui.SetCursorScreenPos(pos);
        ImGui.Dummy(new Vector2(rowWidth, height));
    }

    private void DrawFilterTestPanel(AdvertisementFilterConfig config)
    {
        Card.Draw(
            "ad-filter-test",
            innerWidth =>
            {
                var pos = ImGui.GetCursorScreenPos();
                float height = theme.Scaled(UiTheme.ControlHeight);

                string message = filterTestMessage;
                if (theme.TextInput("##ad-filter-test", pos, innerWidth, ref message, 512, "Paste a message to score it..."))
                    filterTestMessage = message;

                ImGui.SetCursorScreenPos(pos);
                ImGui.Dummy(new Vector2(innerWidth, height + theme.Gap(0.6f)));

                if (string.IsNullOrWhiteSpace(filterTestMessage))
                {
                    ImGui.TextDisabled("Nothing to score yet.");
                    return;
                }

                var evaluation = AdvertisementFilter.Evaluate(filterTestMessage, config);
                DrawEvaluationResult(evaluation, innerWidth);
            },
            label: "Test a message");
    }

    private void DrawEvaluationResult(FilterEvaluation evaluation, float innerWidth)
    {
        string verdict = evaluation.Whitelisted
            ? "Whitelisted"
            : evaluation.Blocked ? "Blocked" : "Allowed";

        var verdictColor = evaluation.Whitelisted
            ? UiTheme.TileBlue
            : evaluation.Blocked ? UiTheme.TileRed : UiTheme.TileGreen;

        string scoreText = $"Score {evaluation.Score} / {evaluation.Threshold}";

        var pos = ImGui.GetCursorScreenPos();
        var verdictSize = theme.ChipSize(verdict);
        theme.ChipAt(pos, verdict, verdictColor);
        theme.ChipAt(new Vector2(pos.X + verdictSize.X + theme.Gap(0.5f), pos.Y), scoreText, theme.Accent);

        ImGui.SetCursorScreenPos(pos);
        ImGui.Dummy(new Vector2(innerWidth, verdictSize.Y + theme.Gap(0.6f)));

        foreach (var match in evaluation.Matches)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.MutedText))
                ImGui.TextWrapped(match);
        }

        if (evaluation.Matches.Count == 0)
            ImGui.TextDisabled("No pattern matched.");
    }

    private void DrawCountChip(Vector2 rightAnchor, int count, string noun)
    {
        string text = count == 1 ? $"1 {noun}" : $"{count} {noun}s";
        var size = theme.ChipSize(text);

        theme.ChipAt(new Vector2(rightAnchor.X - size.X, rightAnchor.Y - theme.Scaled(2f)), text);
    }

    private void DrawFilterHelpPill(Vector2 rightAnchor) => theme.HelpPill(
        "ad-filter-help",
        rightAnchor,
        "Each matching pattern adds points to a message.\n" +
        "A message is blocked once its score reaches the threshold.\n" +
        "Enable the filter per chat type on the Channel Mappings page.");
}
