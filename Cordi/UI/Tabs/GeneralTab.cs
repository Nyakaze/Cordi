using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

using Cordi.Core;
using Cordi.UI.Themes;
using Cordi.Configuration;

namespace Cordi.UI.Tabs;

public class GeneralTab : ConfigTabBase
{
    private string botToken = string.Empty;
    private bool _botTokenInputActive = false;



    private bool _highScoreRegexExpanded = false;
    private bool _highScoreKeywordsExpanded = false;
    private bool _mediumScoreRegexExpanded = false;
    private bool _mediumScoreKeywordsExpanded = false;
    private bool _whitelistExpanded = false;

    public override string Label => "General";

    public GeneralTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
        this.botToken = plugin.Config.Discord.BotToken ?? string.Empty;
    }

    public override void Draw()
    {
        theme.SpacerY(2f);
        bool enabled = true;

        theme.DrawPluginCardAuto(
            id: "dsc-bot-token",
            enabled: ref enabled,
            showCheckbox: false,
            title: "Discord Bot Token",
            drawContent: (avail) =>
            {
                string btnLabel = "Save Token";
                var btnSize = ImGui.CalcTextSize(btnLabel);
                var style = ImGui.GetStyle();
                float btnWidth = btnSize.X + style.FramePadding.X * 2;
                float spacing = style.ItemSpacing.X;

                ImGui.PushItemWidth(avail - btnWidth - spacing);
                var flags = _botTokenInputActive ? ImGuiInputTextFlags.None : ImGuiInputTextFlags.Password;
                ImGui.InputText("##bot-token-input", ref botToken, 256, flags);
                _botTokenInputActive = ImGui.IsItemActive();
                ImGui.PopItemWidth();

                ImGui.SameLine();

                if (ImGui.Button(btnLabel))
                {
                    plugin.Config.Discord.BotToken = botToken;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();
            }
        );

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);

        theme.DrawPluginCardAuto(
            id: "dsc-commands",
            enabled: ref enabled,
            showCheckbox: false,
            title: "Discord Commands",
            drawContent: (avail) =>
            {
                ImGui.TextColored(UiTheme.ColorDangerText, "WARNING: This feature is experimental!");
                bool allowCommands = plugin.Config.Discord.AllowDiscordCommands;
                if (ImGui.Checkbox("Enable Discord Commands", ref allowCommands))
                {
                    plugin.Config.Discord.AllowDiscordCommands = allowCommands;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();
                if (plugin.Config.Discord.AllowDiscordCommands)
                {
                    ImGui.Indent();

                    string prefix = plugin.Config.Discord.CommandPrefix;
                    if (ImGui.InputText("Command Prefix", ref prefix, 10))
                    {
                        plugin.Config.Discord.CommandPrefix = prefix;
                        plugin.Config.Save();
                    }

                    ImGui.TextWrapped("Available commands:");
                    ImGui.BulletText("!target PlayerName World - Target a player");
                    ImGui.BulletText("!emote emotename PlayerName World - Emote at a player");

                    ImGui.Unindent();
                }
            }
        );

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);

        bool keepTargetActive = plugin.Config.KeepTarget.Enabled;
        string keepTargetName = plugin.Config.KeepTarget.TargetName;

        theme.DrawPluginCardAuto(
            id: "keep-target",
            title: "Keep Target",
            enabled: ref keepTargetActive,
            showCheckbox: true,
            drawContent: (avail) =>
            {
                ImGui.TextWrapped("Automatically retargets a specific player or NPC if they are nearby.\nYou can also use /cordikpt <name> to toggle this.");
                theme.SpacerY(1f);

                ImGui.TextColored(theme.Text, "Target Name");
                ImGui.PushItemWidth(200);
                if (ImGui.InputText("##target-name", ref keepTargetName, 64))
                {
                    plugin.Config.KeepTarget.TargetName = keepTargetName;
                    plugin.Config.Save();
                }
                ImGui.PopItemWidth();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("The exact name of the character to keep targeted.");
                }

                ImGui.SameLine();
                if (ImGui.Button("Current Target"))
                {
                    var target = Service.TargetManager.Target;
                    if (target != null)
                    {
                        keepTargetName = target.Name.TextValue;
                        plugin.Config.KeepTarget.TargetName = keepTargetName;
                        plugin.Config.Save();
                    }
                }
                theme.HoverHandIfItem();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Set target name to your current target.");
                }
            }
        );

        if (keepTargetActive != plugin.Config.KeepTarget.Enabled)
        {
            plugin.Config.KeepTarget.Enabled = keepTargetActive;
            plugin.Config.Save();
        }

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);

        theme.DrawPluginCardAuto(
            id: "ad-filter",
            enabled: ref enabled,
            showCheckbox: false,
            title: "Advertisement Filter",
            drawContent: (avail) =>
            {
                bool filterEnabled = plugin.Config.AdvertisementFilter.Enabled;
                if (ImGui.Checkbox("Enable Advertisement Filter", ref filterEnabled))
                {
                    plugin.Config.AdvertisementFilter.Enabled = filterEnabled;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();

                if (filterEnabled)
                {
                    theme.SpacerY(0.5f);
                    ImGui.TextWrapped("Filters messages containing Discord links, venue locations, and spam keywords.");

                    theme.SpacerY(1f);

                    ImGui.TextColored(theme.Text, "Detection Threshold");
                    int threshold = plugin.Config.AdvertisementFilter.ScoreThreshold;
                    ImGui.PushItemWidth(200);
                    if (ImGui.SliderInt("##threshold", ref threshold, 1, 10))
                    {
                        plugin.Config.AdvertisementFilter.ScoreThreshold = threshold;
                        plugin.Config.Save();
                    }
                    ImGui.PopItemWidth();
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Lower = more strict filtering. Default: 3");
                    }

                    ImGui.TextColored(theme.MutedText, "Customize detection patterns below. Patterns are scored: High (2 pts) and Medium (1 pt).");

                    theme.SpacerY(2f);
                }
            }
        );

        theme.SpacerY(2f);

        theme.SpacerY(2f);

        Action save = () => plugin.Config.Save();

        theme.DrawStringTable("hsregex", "High-Score Regex Patterns ", ref _highScoreRegexExpanded,
            plugin.Config.AdvertisementFilter.HighScoreRegexPatterns, save, itemName: "Pattern");

        theme.DrawStringTable("hskw", "High-Score Keywords", ref _highScoreKeywordsExpanded,
            plugin.Config.AdvertisementFilter.HighScoreKeywords, save, itemName: "Pattern");

        theme.DrawStringTable("msregex", "Medium-Score Regex Patterns", ref _mediumScoreRegexExpanded,
            plugin.Config.AdvertisementFilter.MediumScoreRegexPatterns, save, itemName: "Pattern");

        theme.DrawStringTable("mskw", "Medium-Score Keywords", ref _mediumScoreKeywordsExpanded,
            plugin.Config.AdvertisementFilter.MediumScoreKeywords, save, itemName: "Pattern");

        theme.DrawStringTable("wl", "Whitelist", ref _whitelistExpanded,
            plugin.Config.AdvertisementFilter.Whitelist, save, itemName: "Pattern");

        theme.SpacerY(2f);

        theme.SpacerY(2f);

        bool cwActive = plugin.Config.CleanWindow.Enabled;

        theme.DrawPluginCardAuto(
            id: "clean-window",
            title: "Clean Window",
            enabled: ref cwActive,
            showCheckbox: true,
            drawContent: (avail) =>
            {
                ImGui.TextWrapped("Opens a secondary, borderless game window WITHOUT Dalamud UI.\nUseful for OBS capture. Must be played in borderless windowed mode.");
                theme.SpacerY(1f);

                bool showUI = plugin.Config.CleanWindow.ShowGameUI;
                if (ImGui.Checkbox("Show Game UI", ref showUI))
                {
                    plugin.Config.CleanWindow.ShowGameUI = showUI;
                    plugin.Config.Save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("When enabled, shows the native game UI (chat, hotbars, etc.) in the clean window.\nWhen disabled, shows only the game world without any UI.");

                theme.SpacerY(0.5f);

                string[] sizes = { "Native", "1440p", "1080p", "720p" };
                int[] heights = { 0, 1440, 1080, 720 };

                int currentHeight = plugin.Config.CleanWindow.OutputHeight;
                int selectedIndex = Array.IndexOf(heights, currentHeight);
                if (selectedIndex == -1) selectedIndex = 0;

                ImGui.PushItemWidth(150);
                if (ImGui.BeginCombo("Output Size", sizes[selectedIndex]))
                {
                    for (int i = 0; i < sizes.Length; i++)
                    {
                        bool isSelected = (selectedIndex == i);
                        if (ImGui.Selectable(sizes[i], isSelected))
                        {
                            plugin.Config.CleanWindow.OutputHeight = heights[i];
                            plugin.Config.Save();
                        }
                        if (isSelected) ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }
                ImGui.PopItemWidth();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Scales the clean window down to save screen space while capturing.");

                theme.SpacerY(0.5f);

                bool movable = plugin.Config.CleanWindow.MovableWindow;
                if (ImGui.Checkbox("Movable Window", ref movable))
                {
                    plugin.Config.CleanWindow.MovableWindow = movable;
                    plugin.Config.Save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("When disabled, the window will lose its title bar and perfectly wrap the game texture.");
            }
        );

        if (cwActive != plugin.Config.CleanWindow.Enabled)
        {
            plugin.Config.CleanWindow.Enabled = cwActive;
            plugin.Config.Save();
        }
    }
}
