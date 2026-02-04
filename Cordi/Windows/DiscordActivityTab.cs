using System;
using System.Numerics;
using Cordi.Services;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Linq;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using DSharpPlus.Entities;
using Cordi.Core;
using Cordi.UI.Themes;
using Cordi.Configuration;

namespace Cordi.Windows;

public class DiscordActivityTab
{
    private readonly CordiPlugin _plugin;
    private readonly UiTheme _theme;

    private ActivityType _selectedConfigType = ActivityType.Playing;

    public DiscordActivityTab(CordiPlugin plugin, UiTheme theme)
    {
        _plugin = plugin;
        _theme = theme;
    }

    public void Draw()
    {
        var config = _plugin.Config.ActivityConfig;
        bool changed = false;

        _theme.SpacerY(1f);

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

        DrawTypeCard(ActivityType.Playing, "Activity: Playing", config, ref changed);
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

                var keys = config.Replacements.Keys.ToList();
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

    private void DrawTypeCard(ActivityType type, string label, DiscordActivityConfig config, ref bool changed)
    {
        if (!config.TypeConfigs.TryGetValue(type, out var conf))
        {
            conf = new ActivityTypeConfig { Enabled = true, Priority = 0, Format = $"{label} {{name}}" };
            config.TypeConfigs[type] = conf;
            changed = true;
        }

        bool enabled = conf.Enabled;
        bool localChanged = false;

        _theme.DrawPluginCardAuto(
            id: $"act-card-{type}",
            enabled: ref enabled,
            showCheckbox: true,
            title: label,
            drawContent: (avail) =>
            {
                float scale = ImGuiHelpers.GlobalScale;
                var startPos = ImGui.GetCursorPos();
                ImGui.SetCursorPosX(avail - 20 * scale);
                ImGui.TextDisabled("(?)");
                if (ImGui.IsItemHovered())
                {
                    string tip = "Common Placeholders:\n";
                    tip += "- {name}: Activity Name\n";
                    tip += "- {details}: Track / Details\n";
                    tip += "- {state}: Artist / Status\n";
                    tip += "- {elapsed}, {duration}, {time_start}, {time_end}\n";

                    if (type == ActivityType.ListeningTo)
                        tip += "\nMusic Specific:\n- {album}, {track}, {artist}";
                    else if (type == ActivityType.Watching)
                        tip += "\nVideo Specific:\n- {details} (Title)\n- {state} (Status)\n- {album} (Show/Series)";

                    ImGui.SetTooltip(tip);
                }
                _theme.HoverHandIfItem();
                ImGui.SetCursorPos(startPos);

                if (enabled != conf.Enabled) { conf.Enabled = enabled; localChanged = true; }

                ImGui.BeginGroup();
                ImGui.TextColored(_theme.MutedText, "Priority: ");
                ImGui.SameLine();
                int prio = conf.Priority;
                ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
                if (ImGui.InputInt($"##Prio_{type}", ref prio)) { conf.Priority = prio; localChanged = true; }
                ImGui.EndGroup();

                _theme.SpacerY(0.5f);

                ImGui.Text("Format");
                string fmt = conf.Format;
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.InputText($"##Fmt_{type}", ref fmt, 128)) { conf.Format = fmt; localChanged = true; }

                _theme.SpacerY(0.5f);
                ImGui.Separator();
                _theme.SpacerY(0.5f);

                bool cycle = conf.EnableCycling;
                if (ImGui.Checkbox($"Cycling Mode##{type}", ref cycle)) { conf.EnableCycling = cycle; localChanged = true; }
                _theme.HoverHandIfItem();

                if (cycle)
                {
                    ImGui.Indent();

                    if (conf.CycleFormats == null) conf.CycleFormats = new();

                    ImGui.Text("Add formats to cycle through (including the base format if desired):");

                    if (ImGui.BeginTable($"##CycleFmts_{type}", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInnerV))
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
                            if (ImGui.InputText($"##CycleFmt_{type}_{i}", ref fmtStr, 128))
                            {
                                conf.CycleFormats[i] = fmtStr;
                                localChanged = true;
                            }

                            ImGui.TableNextColumn();
                            if (ImGui.Button($"X##DelCycleFmt_{type}_{i}"))
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

                    if (_theme.SecondaryButton($"+ Add Cycle Format##{type}"))
                    {
                        conf.CycleFormats.Add("");
                        localChanged = true;
                    }

                    _theme.SpacerY(0.5f);
                    ImGui.Text("Switch Interval (s)");
                    int interval = conf.CycleIntervalSeconds;
                    ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
                    if (ImGui.DragInt($"##Int_{type}", ref interval, 1, 3, 300)) { conf.CycleIntervalSeconds = interval; localChanged = true; }
                    ImGui.Unindent();
                    _theme.HoverHandIfItem();
                }

                if (type == ActivityType.ListeningTo)
                {
                    _theme.SpacerY(0.5f);
                    ImGui.Separator();
                    _theme.SpacerY(0.5f);
                    ImGui.Text("Truncation Limits (0 = Unlimited)");

                    ImGui.BeginGroup();
                    ImGui.Text("Track");
                    ImGui.SameLine();
                    int tLim = conf.TrackLimit;
                    ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
                    if (ImGui.InputInt($"##TLim_{type}", ref tLim)) { conf.TrackLimit = Math.Max(0, tLim); localChanged = true; }


                    ImGui.SameLine();
                    _theme.SpacerX(1f);
                    ImGui.SameLine();

                    ImGui.Text("Artist");
                    ImGui.SameLine();
                    int aLim = conf.ArtistLimit;
                    ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
                    if (ImGui.InputInt($"##ALim_{type}", ref aLim)) { conf.ArtistLimit = Math.Max(0, aLim); localChanged = true; }
                    ImGui.EndGroup();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Limits the character count of {details} (Track) and {state} (Artist) before insertion.");
                }

                _theme.SpacerY(0.5f);
                ImGui.Separator();
                _theme.SpacerY(0.5f);

                ImGui.Text("Title Colors");

                ImGui.BeginGroup();
                Vector3? cVal = conf.Color;
                bool hasColor = cVal.HasValue;
                if (ImGui.Checkbox($"Override Color##{type}", ref hasColor))
                {
                    conf.Color = hasColor ? new Vector3(1, 1, 1) : null;
                    localChanged = true;
                }
                _theme.HoverHandIfItem();
                if (hasColor)
                {
                    ImGui.SameLine();
                    Vector3 col = conf.Color ?? new Vector3(1, 1, 1);
                    if (ImGui.ColorEdit3($"##ColPick_{type}", ref col, ImGuiColorEditFlags.NoInputs))
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
                if (ImGui.Checkbox($"Override Glow##{type}", ref hasGlow))
                {
                    conf.Glow = hasGlow ? new Vector3(1, 1, 1) : null;
                    localChanged = true;
                }
                _theme.HoverHandIfItem();
                if (hasGlow)
                {
                    ImGui.SameLine();
                    Vector3 glo = conf.Glow ?? new Vector3(1, 1, 1);
                    if (ImGui.ColorEdit3($"##GlowPick_{type}", ref glo, ImGuiColorEditFlags.NoInputs))
                    {
                        conf.Glow = glo;
                        localChanged = true;
                    }
                    _theme.HoverHandIfItem();
                }
                ImGui.EndGroup();

                _theme.SpacerY(0.5f);
                ImGui.Separator();
                _theme.SpacerY(0.5f);

                ImGui.Text("Exclusion Filters");
                _theme.MutedLabel("If a filter matches, the activity is hidden.");

                if (ImGui.BeginTable($"##FiltersTbl_{type}", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit))
                {
                    ImGui.TableSetupColumn("Placeholder", ImGuiTableColumnFlags.WidthFixed, 100f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("Mode", ImGuiTableColumnFlags.WidthFixed, 120f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("##Action", ImGuiTableColumnFlags.WidthFixed, 30f * ImGuiHelpers.GlobalScale);
                    ImGui.TableHeadersRow();

                    bool removeRule = false;
                    FilterRule ruleToRemove = null;

                    foreach (var rule in conf.Filters)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        ImGui.SetNextItemWidth(-1);
                        string ph = rule.TargetPlaceholder;
                        if (ImGui.InputText($"##LoopPH_{rule.GetHashCode()}", ref ph, 32)) { rule.TargetPlaceholder = ph; localChanged = true; }

                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1);
                        var mode = rule.Mode;
                        string[] modeNames = Enum.GetNames(typeof(FilterMode));
                        int modeIdx = (int)mode;
                        if (ImGui.Combo($"##LoopMode_{rule.GetHashCode()}", ref modeIdx, modeNames, modeNames.Length))
                        {
                            rule.Mode = (FilterMode)modeIdx;
                            localChanged = true;
                        }

                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1);
                        string val = rule.Value;
                        if (ImGui.InputText($"##LoopVal_{rule.GetHashCode()}", ref val, 64)) { rule.Value = val; localChanged = true; }

                        ImGui.TableNextColumn();
                        if (ImGui.Button($"X##Rm_{rule.GetHashCode()}"))
                        {
                            ruleToRemove = rule;
                            removeRule = true;
                        }
                    }

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled("{placeholder}");
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled("Mode");
                    ImGui.TableNextColumn();
                    if (ImGui.Button("+ Add New Filter", new Vector2(-1, 0)))
                    {
                        conf.Filters.Add(new FilterRule { TargetPlaceholder = "{artist}", Mode = FilterMode.Contains, Value = "" });
                        localChanged = true;
                    }
                    _theme.HoverHandIfItem();
                    ImGui.TableNextColumn();

                    ImGui.EndTable();

                    if (removeRule && ruleToRemove != null)
                    {
                        conf.Filters.Remove(ruleToRemove);
                        localChanged = true;
                    }
                }
            }
        );

        if (localChanged) changed = true;
    }
}
