using System;
using System.Numerics;
using Cordi.Services;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Linq;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using DSharpPlus.Entities;

using Cordi.Configuration;
using Cordi.Core;
using Cordi.UI.Themes;

namespace Cordi.UI.Tabs;

public class DiscordActivityTab
{
    private readonly CordiPlugin _plugin;
    private readonly UiTheme _theme;

    private string newGameInputState = "";

    public DiscordActivityTab(CordiPlugin plugin, UiTheme theme)
    {
        _plugin = plugin;
        _theme = theme;
    }

    public void Draw()
    {
        var config = _plugin.Config.ActivityConfig;
        bool changed = false;

        _theme.SpacerY(2f);


        bool enabled = config.Enabled;
        bool unused = true;
        _theme.DrawPluginCardAuto(
            id: "act-general-card",
            enabled: ref unused,
            showCheckbox: false,
            title: "General Settings",
            drawContent: (avail) =>
            {
                ImGui.TextColored(_theme.MutedText, "Configure the target Discord user and main settings.");
                _theme.SpacerY(0.5f);


                if (ImGui.Checkbox("Enable Discord Activity Integration", ref enabled))
                {
                    config.Enabled = enabled;
                    changed = true;
                }
                _theme.HoverHandIfItem();

                _theme.SpacerY(0.5f);


                string userIdStr = config.TargetUserId == 0 ? "" : config.TargetUserId.ToString();
                ImGui.Text("Target User ID");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
                if (ImGui.InputText("##TargetUserId", ref userIdStr, 20))
                {
                    if (ulong.TryParse(userIdStr, out var newId))
                    {
                        config.TargetUserId = newId;
                        changed = true;
                    }
                    else if (string.IsNullOrEmpty(userIdStr))
                    {
                        config.TargetUserId = 0;
                        changed = true;
                    }
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("The value from 'Copy User ID' in Discord.");

                ImGui.SameLine();
                _theme.SpacerX(1f);
                ImGui.SameLine();


                bool prefix = config.PrefixTitle;
                if (ImGui.Checkbox("Prefix Mode", ref prefix)) { config.PrefixTitle = prefix; changed = true; }
                _theme.HoverHandIfItem();
            }
        );

        _theme.SpacerY(1f);
        ImGui.Separator();
        _theme.SpacerY(1f);



        DrawTypeCard(ActivityType.Playing, "Activity: Playing", config, ref changed, (avail) =>
        {
            _theme.SpacerY(1f);
            ImGui.Separator();
            _theme.SpacerY(1f);

            ImGui.TextColored(_theme.MutedText, "Game Specific Overrides");
            _theme.MutedLabel("Define special formats for specific games by name.");
            _theme.SpacerY(0.5f);

            if (ImGui.BeginTable("GamesTable", 2, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Game Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("##Action", ImGuiTableColumnFlags.WidthFixed, 100f * ImGuiHelpers.GlobalScale);

                string gameToRemove = null;
                List<string> games = config.GameConfigs.Keys.ToList();

                for (int i = 0; i < games.Count; i++)
                {
                    var game = games[i];
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    bool nodeOpen = ImGui.TreeNodeEx($"##GameNode_{i}", ImGuiTreeNodeFlags.SpanAvailWidth, game);

                    ImGui.TableNextColumn();
                    if (ImGui.Button($"Delete##DelGame_{i}")) gameToRemove = game;

                    if (nodeOpen)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        // Spanning both columns for the editor
                        ImGui.TableSetColumnIndex(0);

                        var gameConf = config.GameConfigs[game];
                        DrawTypeCardInner(gameConf, $"Settings: {game}", ref changed, showLimits: false);

                        ImGui.TreePop();
                    }
                }
                ImGui.EndTable();

                if (gameToRemove != null)
                {
                    config.GameConfigs.Remove(gameToRemove);
                    changed = true;
                }
            }

            _theme.SpacerY(0.5f);

            string newGameName = newGameInputState;
            ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
            if (ImGui.InputText("##NewGameName", ref newGameName, 64)) newGameInputState = newGameName;
            ImGui.SameLine();
            if (ImGui.Button("Add Game Override") && !string.IsNullOrEmpty(newGameInputState))
            {
                if (!config.GameConfigs.ContainsKey(newGameInputState))
                {
                    config.GameConfigs[newGameInputState] = new ActivityTypeConfig
                    {
                        Enabled = true,
                        Priority = 10,
                        Format = "Playing {name}"
                    };
                    newGameInputState = "";
                    changed = true;
                }
            }
        });
        _theme.SpacerY(0.5f);


        DrawTypeCard(ActivityType.ListeningTo, "Activity: Listening", config, ref changed);
        _theme.SpacerY(0.5f);


        DrawTypeCard(ActivityType.Watching, "Activity: Watching", config, ref changed);
        _theme.SpacerY(0.5f);


        DrawTypeCard(ActivityType.Custom, "Activity: Custom Status", config, ref changed);

        _theme.SpacerY(1f);
        ImGui.Separator();
        _theme.SpacerY(1f);


        _theme.DrawPluginCardAuto(
            id: "act-replacements-card",
            enabled: ref unused,
            showCheckbox: false,
            title: "Text Replacements",
            drawContent: (avail) =>
            {
                ImGui.TextColored(_theme.MutedText, "Sanitize or shorten text before it appears in the title.");
                _theme.SpacerY(0.5f);

                List<string> keys = config.Replacements.Keys.ToList();
                string keyToDelete = null;
                string? keyToRename = null;
                string? newKeyVal = null;

                float tableHeight = 150f * ImGuiHelpers.GlobalScale;
                if (ImGui.BeginChild("ReplacementsList", new Vector2(0, tableHeight), true))
                {

                    if (ImGui.BeginTable("RepTable", 3, ImGuiTableFlags.SizingStretchProp))
                    {
                        ImGui.TableSetupColumn("Original Text");
                        ImGui.TableSetupColumn("Replacement");
                        ImGui.TableSetupColumn("##Del", ImGuiTableColumnFlags.WidthFixed, 30f);

                        for (int i = 0; i < keys.Count; i++)
                        {
                            var key = keys[i];
                            ImGui.TableNextRow();

                            ImGui.TableNextColumn();
                            string k = key;
                            ImGui.SetNextItemWidth(-1);
                            if (ImGui.InputText($"##Key_{i}", ref k, 50))
                            {
                                keyToRename = key;
                                newKeyVal = k;
                            }

                            ImGui.TableNextColumn();
                            string val = config.Replacements[key];
                            ImGui.SetNextItemWidth(-1);
                            if (ImGui.InputText($"##Val_{i}", ref val, 50))
                            {
                                config.Replacements[key] = val;
                                changed = true;
                            }

                            ImGui.TableNextColumn();
                            if (ImGui.Button($"X##Del_{i}")) keyToDelete = key;
                            _theme.HoverHandIfItem();
                        }
                        ImGui.EndTable();
                    }
                }
                ImGui.EndChild();

                if (keyToRename != null && newKeyVal != null && keyToRename != newKeyVal)
                {
                    if (!config.Replacements.ContainsKey(newKeyVal))
                    {
                        var val = config.Replacements[keyToRename];
                        config.Replacements.Remove(keyToRename);
                        config.Replacements[newKeyVal] = val;
                        changed = true;
                    }
                }

                if (keyToDelete != null)
                {
                    config.Replacements.Remove(keyToDelete);
                    changed = true;
                }

                _theme.SpacerY(0.5f);
                if (_theme.SecondaryButton("+ Add New Replacement"))
                {
                    string newKey = "New";
                    int i = 1;
                    while (config.Replacements.ContainsKey(newKey)) newKey = $"New{i++}";
                    config.Replacements[newKey] = "";
                    changed = true;
                }
            }
        );

        if (changed)
        {
            _plugin.Config.Save();
        }
    }

    private void DrawTypeCard(ActivityType type, string label, DiscordActivityConfig config, ref bool changed, Action<float>? extraContent = null)
    {
        if (!config.TypeConfigs.TryGetValue(type, out var conf))
        {
            conf = new ActivityTypeConfig { Enabled = true, Priority = 0, Format = $"{label} {{name}}" };
            config.TypeConfigs[type] = conf;
            changed = true;
        }

        bool enabled = conf.Enabled;
        bool cardChanged = false;

        _theme.DrawPluginCardAuto(
            id: $"act-card-{type}",
            enabled: ref enabled,
            showCheckbox: true,
            title: label,
            drawContent: (avail) =>
            {
                if (enabled != conf.Enabled) { conf.Enabled = enabled; cardChanged = true; }
                DrawTypeCardInner(conf, label, ref cardChanged, showLimits: type == ActivityType.ListeningTo);

                if (extraContent != null)
                {
                    extraContent(avail);
                }
            }
        );

        if (cardChanged) changed = true;
    }

    private void DrawTypeCardInner(ActivityTypeConfig conf, string label, ref bool changed, bool showLimits)
    {
        bool localChanged = false;

        float scale = ImGuiHelpers.GlobalScale;

        // Help Tooltip
        ImGui.TextDisabled("(?) Help Placeholders");
        if (ImGui.IsItemHovered())
        {
            string tip = "Common Placeholders:\n";
            tip += "- {name}: Activity Name\n";
            tip += "- {details}: Track / Details\n";
            tip += "- {state}: Artist / Status\n";
            tip += "- {elapsed}, {duration}, {time_start}, {time_end}\n";

            ImGui.SetTooltip(tip);
        }

        ImGui.SameLine();
        ImGui.TextColored(_theme.MutedText, "| Priority: ");
        ImGui.SameLine();
        int prio = conf.Priority;
        ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt($"##Prio_{label.GetHashCode()}", ref prio)) { conf.Priority = prio; localChanged = true; }

        _theme.SpacerY(0.5f);

        ImGui.Text("Format");
        string fmt = conf.Format;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.InputText($"##Fmt_{label.GetHashCode()}", ref fmt, 128)) { conf.Format = fmt; localChanged = true; }

        _theme.SpacerY(0.5f);
        ImGui.Separator();
        _theme.SpacerY(0.5f);

        bool cycle = conf.EnableCycling;
        if (ImGui.Checkbox($"Cycling Mode##{label.GetHashCode()}", ref cycle)) { conf.EnableCycling = cycle; localChanged = true; }
        _theme.HoverHandIfItem();

        if (cycle)
        {
            ImGui.Indent();
            if (conf.CycleFormats == null) conf.CycleFormats = new();

            if (ImGui.BeginTable($"##CycleFmts_{label.GetHashCode()}", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInnerV))
            {
                ImGui.TableSetupColumn("Format String", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("##Action", ImGuiTableColumnFlags.WidthFixed, 30);

                int formatToDelete = -1;
                for (int i = 0; i < conf.CycleFormats.Count; i++)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    string fmtStr = conf.CycleFormats[i];
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.InputText($"##CycleFmt_{label.GetHashCode()}_{i}", ref fmtStr, 128))
                    {
                        conf.CycleFormats[i] = fmtStr;
                        localChanged = true;
                    }

                    ImGui.TableNextColumn();
                    if (ImGui.Button($"X##DelCycleFmt_{label.GetHashCode()}_{i}"))
                    {
                        formatToDelete = i;
                    }
                }

                if (formatToDelete != -1)
                {
                    conf.CycleFormats.RemoveAt(formatToDelete);
                    localChanged = true;
                }

                ImGui.EndTable();
            }

            if (_theme.SecondaryButton($"+ Add Cycle Format##{label.GetHashCode()}"))
            {
                conf.CycleFormats.Add("");
                localChanged = true;
            }

            _theme.SpacerY(0.5f);
            ImGui.Text("Switch Interval (s)");
            int interval = conf.CycleIntervalSeconds;
            ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
            if (ImGui.DragInt($"##Int_{label.GetHashCode()}", ref interval, 1, 3, 300)) { conf.CycleIntervalSeconds = interval; localChanged = true; }
            ImGui.Unindent();
        }

        if (showLimits)
        {
            _theme.SpacerY(0.5f);
            ImGui.Separator();
            _theme.SpacerY(0.5f);

            ImGui.Text("Lists / Limits");
            ImGui.BeginGroup();
            ImGui.Text("Track Limit");
            ImGui.SameLine();
            int tLim = conf.TrackLimit;
            ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
            if (ImGui.InputInt($"##TLim_{label.GetHashCode()}", ref tLim)) { conf.TrackLimit = Math.Max(0, tLim); localChanged = true; }

            ImGui.SameLine();
            _theme.SpacerX(1f);
            ImGui.SameLine();

            ImGui.Text("Artist Limit");
            ImGui.SameLine();
            int aLim = conf.ArtistLimit;
            ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
            if (ImGui.InputInt($"##ALim_{label.GetHashCode()}", ref aLim)) { conf.ArtistLimit = Math.Max(0, aLim); localChanged = true; }
            ImGui.EndGroup();
        }

        _theme.SpacerY(0.5f);
        ImGui.Separator();
        _theme.SpacerY(0.5f);

        ImGui.Text("Title Colors");
        ImGui.BeginGroup();

        Vector3? cVal = conf.Color;
        bool hasColor = cVal.HasValue;
        if (ImGui.Checkbox($"Override Color##{label.GetHashCode()}", ref hasColor))
        {
            conf.Color = hasColor ? new Vector3(1, 1, 1) : null;
            localChanged = true;
        }
        _theme.HoverHandIfItem();
        if (hasColor)
        {
            ImGui.SameLine();
            Vector3 col = conf.Color ?? new Vector3(1, 1, 1);
            if (ImGui.ColorEdit3($"##ColPick_{label.GetHashCode()}", ref col, ImGuiColorEditFlags.NoInputs))
            {
                conf.Color = col;
                localChanged = true;
            }
            _theme.HoverHandIfItem();
        }

        ImGui.SameLine();
        _theme.SpacerX(2f);
        ImGui.SameLine();

        Vector3? gVal = conf.Glow;
        bool hasGlow = gVal.HasValue;
        if (ImGui.Checkbox($"Override Glow##{label.GetHashCode()}", ref hasGlow))
        {
            conf.Glow = hasGlow ? new Vector3(1, 1, 1) : null;
            localChanged = true;
        }
        _theme.HoverHandIfItem();
        if (hasGlow)
        {
            ImGui.SameLine();
            Vector3 glo = conf.Glow ?? new Vector3(1, 1, 1);
            if (ImGui.ColorEdit3($"##GlowPick_{label.GetHashCode()}", ref glo, ImGuiColorEditFlags.NoInputs))
            {
                conf.Glow = glo;
                localChanged = true;
            }
            _theme.HoverHandIfItem();
        }
        ImGui.EndGroup();

        if (localChanged) changed = true;
    }
}
