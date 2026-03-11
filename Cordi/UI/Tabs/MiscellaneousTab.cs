using System;
using System.Collections.Generic;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;

namespace Cordi.UI.Tabs;

public class MiscellaneousTab : ConfigTabBase
{
    private bool _highScoreRegexExpanded;
    private bool _highScoreKeywordsExpanded;
    private bool _mediumScoreRegexExpanded;
    private bool _mediumScoreKeywordsExpanded;
    private bool _whitelistExpanded;

    public override string Label => "Miscellaneous";

    public MiscellaneousTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {

    }

    protected override IReadOnlyList<(string Label, Action Draw)> GetSubTabs()
    {
        return new (string, Action)[]
        {
            ("Advertisement Filter", () => DrawAdvertisementFilter(plugin.Config.AdvertisementFilter)),
        };
    }

    private void DrawAdvertisementFilter(AdvertisementFilterConfig config)
    {
        bool filterEnabled = config.Enabled;

        theme.DrawPluginCardAuto(
            id: "ad-filter",
            enabled: ref filterEnabled,
            showCheckbox: true,
            title: "Advertisement Filter",
            drawContent: (avail) =>
            {
                if (filterEnabled)
                {
                    ImGui.TextDisabled("Filters messages containing Discord links, venue locations, and spam keywords.");
                    theme.SpacerY(0.5f);

                    theme.SpacerY(1f);

                    ImGui.TextColored(theme.Text, "Detection Threshold");
                    int threshold = config.ScoreThreshold;
                    ImGui.PushItemWidth(200);
                    if (ImGui.SliderInt("##threshold", ref threshold, 1, 10))
                    {
                        config.ScoreThreshold = threshold;
                        plugin.Config.Save();
                    }
                    ImGui.PopItemWidth();
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Lower = more strict filtering. Default: 3");
                    }

                    ImGui.TextColored(theme.MutedText, "Customize detection patterns below. Patterns are scored: High (2 pts) and Medium (1 pt).");
                }
            }
        );

        theme.SpacerY(1f);

        Action save = () => plugin.Config.Save();

        theme.DrawStringTable("hsregex", "High-Score Regex Patterns ", ref _highScoreRegexExpanded,
            config.HighScoreRegexPatterns, save, itemName: "Pattern");

        theme.DrawStringTable("hskw", "High-Score Keywords", ref _highScoreKeywordsExpanded,
            config.HighScoreKeywords, save, itemName: "Pattern");

        theme.DrawStringTable("msregex", "Medium-Score Regex Patterns", ref _mediumScoreRegexExpanded,
            config.MediumScoreRegexPatterns, save, itemName: "Pattern");

        theme.DrawStringTable("mskw", "Medium-Score Keywords", ref _mediumScoreKeywordsExpanded,
            config.MediumScoreKeywords, save, itemName: "Pattern");

        theme.DrawStringTable("wl", "Whitelist", ref _whitelistExpanded,
            config.Whitelist, save, itemName: "Pattern");
    }
}
