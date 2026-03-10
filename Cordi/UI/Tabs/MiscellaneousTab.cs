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
            ("Keep Target", () => DrawKeepTarget(plugin.Config.KeepTarget)),
            ("Clean Window", () => DrawCleanWindow(plugin.Config.CleanWindow))
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

    private void DrawKeepTarget(KeepTargetConfig config)
    {
        bool keepTargetActive = config.Enabled;
        string keepTargetName = config.TargetName;

        theme.DrawPluginCardAuto(
            id: "keep-target",
            title: "Keep Target",
            enabled: ref keepTargetActive,
            showCheckbox: true,
            drawContent: (avail) =>
            {
                ImGui.TextDisabled("Automatically retargets a specific player or NPC if they are nearby.\nYou can also use /cordikpt <name> to toggle this");
                theme.SpacerY(0.5f);

                ImGui.TextColored(theme.Text, "Target Name");
                ImGui.PushItemWidth(200);
                if (ImGui.InputText("##target-name", ref keepTargetName, 64))
                {
                    config.TargetName = keepTargetName;
                    plugin.Config.Save();
                }
                ImGui.PopItemWidth();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("The exact name of the character to keep targeted.");
                }

                ImGui.SameLine();
                if (theme.Button("Add Current Target", "Set target name to your current target."))
                {
                    var target = Service.TargetManager.Target;
                    if (target != null)
                    {
                        keepTargetName = target.Name.TextValue;
                        config.TargetName = keepTargetName;
                        plugin.Config.Save();
                    }
                }
            }
        );

        if (keepTargetActive != config.Enabled)
        {
            config.Enabled = keepTargetActive;
            plugin.Config.Save();
        }
    }

    private void DrawCleanWindow(CleanWindowConfig config)
    {
        bool cwActive = config.Enabled;

        theme.DrawPluginCardAuto(
            id: "clean-window",
            title: "Clean Window",
            enabled: ref cwActive,
            showCheckbox: true,
            drawContent: (avail) =>
            {
                ImGui.TextDisabled("Opens a secondary, borderless game window WITHOUT Dalamud UI.\nMust be played in borderless windowed mode.");
                theme.SpacerY(0.5f);

                bool showUi = config.ShowGameUI;
                if (theme.Checkbox("Show Game UI", "When enabled, shows the native game UI (chat, hotbars, etc.) in the clean window.\\nWhen disabled, shows only the game world without any UI.\"", ref showUi))
                {
                    config.ShowGameUI = showUi;
                    plugin.Config.Save();
                }
                ImGui.SameLine();
                ImGui.TextDisabled(" |  Probably doesnt work right now, needs testing.");

                theme.SpacerY(0.5f);

                var outputSize = config.OutputSize;
                ImGui.PushItemWidth(150);
                if (theme.EnumCombo("Output Size", "Scales the clean window down to save screen space while capturing.", ref outputSize))
                {
                    config.OutputSize = outputSize;
                    plugin.Config.Save();
                }
                ImGui.PopItemWidth();

                theme.SpacerY(0.5f);

                // bool movable = config.MovableWindow;
                // if (theme.Checkbox("Movable Window", "When disabled, the window will lose its title bar and perfectly wrap the game texture.", ref movable))
                // {
                //     config.MovableWindow = movable;
                //     plugin.Config.Save();
                // }
            }
        );

        if (cwActive != config.Enabled)
        {
            config.Enabled = cwActive;
            plugin.Config.Save();
        }
    }


}
