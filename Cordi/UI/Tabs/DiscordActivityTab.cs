using System;
using System.Numerics;
using Cordi.Services;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Linq;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using DSharpPlus.Entities;

using Cordi.Configuration;
using Cordi.Core;
using Cordi.UI.Themes;

namespace Cordi.UI.Tabs;

public class DiscordActivityTab : ConfigTabBase
{
    private string newGameInputState = "";

    private static readonly string[] FilterModeLabels = { "Contains", "Equals", "Starts With", "Ends With", "Regex" };

    private static readonly Dictionary<ActivityType, string[]> PlaceholdersByType = new()
    {
        { ActivityType.Playing, new[] { "{name}", "{details}", "{state}", "{large_image}", "{small_image}", "{elapsed}", "{duration}", "{time_start}", "{time_end}" } },
        { ActivityType.ListeningTo, new[] { "{name}", "{details}", "{track}", "{state}", "{artist}", "{album}", "{large_image}", "{small_image}", "{elapsed}", "{duration}", "{time_start}", "{time_end}" } },
        { ActivityType.Watching, new[] { "{name}", "{details}", "{state}", "{large_image}", "{small_image}", "{elapsed}", "{duration}", "{time_start}", "{time_end}" } },
        { ActivityType.Custom, new[] { "{name}", "{state}", "{large_image}", "{small_image}" } },
    };

    private static readonly string[] DefaultPlaceholders = { "{name}", "{details}", "{state}" };

    public override string Label => "Activity";

    public DiscordActivityTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
    }

    protected override IReadOnlyList<(string Label, Action Draw)> GetSubTabs()
    {
        return new List<(string Label, Action Draw)>
        {
            ("Playing", DrawPlayingSubTab),
            ("Listening", DrawListeningSubTab),
            ("Watching", DrawWatchingSubTab),
            ("Custom", DrawCustomSubTab),
            ("General", DrawGeneralSubTab),
        };
    }

    private void DrawPlayingSubTab()
    {
        var config = plugin.Config.ActivityConfig;
        bool changed = false;

        DrawTypeCard(ActivityType.Playing, "Activity: Playing", config, ref changed, (avail) =>
        {
            theme.SpacerY(1f);
            ImGui.Separator();
            theme.SpacerY(1f);

            ImGui.TextColored(theme.MutedText, "Game Specific Overrides");
            theme.MutedLabel("Define special formats for specific games by name.");
            theme.SpacerY(0.5f);

            using (var table = ImRaii.Table("GamesTable", 2, ImGuiTableFlags.SizingStretchProp))
            {
                if (table)
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
                        using var node = ImRaii.TreeNode($"{game}###GameNode_{i}", ImGuiTreeNodeFlags.SpanAvailWidth);

                        ImGui.TableNextColumn();
                        if (ImGui.Button($"Delete##DelGame_{i}")) gameToRemove = game;

                        if (node)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.TableSetColumnIndex(0);

                            var gameConf = config.GameConfigs[game];
                            DrawTypeCardInner(gameConf, $"Settings: {game}", ActivityType.Playing, ref changed, showLimits: false);
                        }
                    }

                    if (gameToRemove != null)
                    {
                        config.GameConfigs.Remove(gameToRemove);
                        changed = true;
                    }
                }
            }

            theme.SpacerY(0.5f);

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
                        Priority = 0,
                        Format = "Playing {name}"
                    };
                    newGameInputState = "";
                    changed = true;
                }
            }
        });

        if (changed)
            plugin.Config.Save();
    }

    private void DrawListeningSubTab()
    {
        var config = plugin.Config.ActivityConfig;
        bool changed = false;

        DrawTypeCard(ActivityType.ListeningTo, "Activity: Listening", config, ref changed);

        if (changed)
            plugin.Config.Save();
    }

    private void DrawWatchingSubTab()
    {
        var config = plugin.Config.ActivityConfig;
        bool changed = false;

        DrawTypeCard(ActivityType.Watching, "Activity: Watching", config, ref changed);

        if (changed)
            plugin.Config.Save();
    }

    private void DrawCustomSubTab()
    {
        var config = plugin.Config.ActivityConfig;
        bool changed = false;

        EnsureCustomPresets(config, ref changed);

        bool unused = true;
        theme.DrawPluginCardAuto(
            id: "act-custom-presets-card",
            enabled: ref unused,
            showCheckbox: false,
            title: "Presets",
            drawContent: (avail) =>
            {
                ImGui.TextColored(theme.MutedText, "Save multiple custom statuses and switch between them.");
                theme.SpacerY(0.5f);

                string[] names = config.CustomPresets.Select(p => p.Name).ToArray();
                int active = Math.Clamp(config.ActiveCustomPreset, 0, config.CustomPresets.Count - 1);

                ImGui.Text("Active Preset");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
                if (ImGui.Combo("##CustomPresetSelect", ref active, names, names.Length))
                {
                    config.ActiveCustomPreset = active;
                    config.TypeConfigs[ActivityType.Custom] = config.CustomPresets[active].Config;
                    changed = true;
                }
                theme.HoverHandIfItem();

                ImGui.SameLine();
                if (theme.SecondaryButton("+ New##CustomPreset"))
                {
                    var preset = new ActivityPreset
                    {
                        Name = UniquePresetName(config, "Preset"),
                        Config = new ActivityTypeConfig { Enabled = true, Priority = 0, Format = "{state}" }
                    };
                    config.CustomPresets.Add(preset);
                    config.ActiveCustomPreset = config.CustomPresets.Count - 1;
                    config.TypeConfigs[ActivityType.Custom] = preset.Config;
                    changed = true;
                }

                ImGui.SameLine();
                using (ImRaii.Disabled(config.CustomPresets.Count <= 1))
                {
                    if (theme.DangerIconButton("##DelCustomPreset", FontAwesomeIcon.Trash, "Delete Preset"))
                    {
                        config.CustomPresets.RemoveAt(active);
                        config.ActiveCustomPreset = Math.Clamp(active, 0, config.CustomPresets.Count - 1);
                        config.TypeConfigs[ActivityType.Custom] = config.CustomPresets[config.ActiveCustomPreset].Config;
                        changed = true;
                    }
                }

                theme.SpacerY(0.5f);

                int safeActive = Math.Clamp(config.ActiveCustomPreset, 0, config.CustomPresets.Count - 1);
                ImGui.Text("Name");
                ImGui.SameLine();
                string nm = config.CustomPresets[safeActive].Name;
                ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
                if (ImGui.InputText("##CustomPresetName", ref nm, 64))
                {
                    config.CustomPresets[safeActive].Name = nm;
                    changed = true;
                }
            }
        );

        theme.SpacerY();

        DrawTypeCard(ActivityType.Custom, "Activity: Custom Status", config, ref changed);

        if (changed)
            plugin.Config.Save();
    }

    private void EnsureCustomPresets(DiscordActivityConfig config, ref bool changed)
    {
        if (config.CustomPresets == null) config.CustomPresets = new();

        if (config.CustomPresets.Count == 0)
        {
            if (!config.TypeConfigs.TryGetValue(ActivityType.Custom, out var existing) || existing == null)
            {
                existing = new ActivityTypeConfig { Enabled = true, Priority = 0, Format = "{state}" };
                config.TypeConfigs[ActivityType.Custom] = existing;
            }
            config.CustomPresets.Add(new ActivityPreset { Name = "Default", Config = existing });
            config.ActiveCustomPreset = 0;
            changed = true;
        }

        config.ActiveCustomPreset = Math.Clamp(config.ActiveCustomPreset, 0, config.CustomPresets.Count - 1);

        var activeConfig = config.CustomPresets[config.ActiveCustomPreset].Config;
        if (activeConfig == null)
        {
            activeConfig = new ActivityTypeConfig { Enabled = true, Priority = 0, Format = "{state}" };
            config.CustomPresets[config.ActiveCustomPreset].Config = activeConfig;
            changed = true;
        }

        config.TypeConfigs.TryGetValue(ActivityType.Custom, out var current);
        if (!ReferenceEquals(current, activeConfig))
        {
            config.TypeConfigs[ActivityType.Custom] = activeConfig;
            changed = true;
        }
    }

    private static string UniquePresetName(DiscordActivityConfig config, string baseName)
    {
        string name = baseName;
        int i = 1;
        while (config.CustomPresets.Any(p => p.Name == name)) name = $"{baseName} {i++}";
        return name;
    }

    private void DrawGeneralSubTab()
    {
        var config = plugin.Config.ActivityConfig;
        bool changed = false;
        bool unused = true;

        bool enabled = config.Enabled;
        theme.DrawPluginCardAuto(
            id: "act-general-card",
            enabled: ref unused,
            showCheckbox: false,
            title: "General",
            drawContent: (avail) =>
            {
                ImGui.TextColored(theme.MutedText, "Configure the target Discord user and main settings.");
                theme.SpacerY(0.5f);

                theme.ConfigCheckbox("Enable Discord Activity Integration", ref enabled, () =>
                {
                    config.Enabled = enabled;
                    changed = true;
                });

                theme.SpacerY(0.5f);

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
                theme.SpacerX();
                ImGui.SameLine();

                bool prefix = config.PrefixTitle;
                if (ImGui.Checkbox("Prefix Mode", ref prefix)) { config.PrefixTitle = prefix; changed = true; }
                theme.HoverHandIfItem();
            }
        );

        theme.SpacerY();

        theme.DrawPluginCardAuto(
            id: "act-replacements-card",
            enabled: ref unused,
            showCheckbox: false,
            title: "Text Replacements",
            drawContent: (avail) =>
            {
                ImGui.TextColored(theme.MutedText, "Sanitize or shorten text before it appears in the title.");
                theme.SpacerY(0.5f);

                List<string> keys = config.Replacements.Keys.ToList();
                string keyToDelete = null;
                string? keyToRename = null;
                string? newKeyVal = null;

                theme.DrawTable(
                    id: "act-replacements",
                    collection: keys,
                    drawRow: (key, i) =>
                    {
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
                        if (theme.DangerIconButton($"##Del_{i}", FontAwesomeIcon.Trash, "Remove")) keyToDelete = key;
                    },
                    headers: new[] { "Original Text", "Replacement", "##Del" },
                    setupColumns: () =>
                    {
                        float delColW = ImGui.GetFrameHeight() + ImGui.GetStyle().FramePadding.X * 2f;
                        float remaining = ImGui.GetContentRegionAvail().X - delColW - ImGui.GetStyle().CellPadding.X * 4f;
                        float colW = remaining * 0.5f;
                        ImGui.TableSetupColumn("Original Text", ImGuiTableColumnFlags.WidthFixed, colW);
                        ImGui.TableSetupColumn("Replacement", ImGuiTableColumnFlags.WidthFixed, colW);
                        ImGui.TableSetupColumn("##Del", ImGuiTableColumnFlags.WidthFixed, delColW);
                    },
                    showHeaders: true
                );

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

                theme.SpacerY(0.5f);
                float repBtnWidth = ImGui.GetContentRegionAvail().X * 0.95f;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X - repBtnWidth) * 0.5f);
                if (theme.SecondaryButton("+ Add New Replacement", new Vector2(repBtnWidth, 0)))
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
            plugin.Config.Save();
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

        theme.DrawPluginCardAuto(
            id: $"act-card-{type}",
            enabled: ref enabled,
            showCheckbox: true,
            title: label,
            drawContent: (avail) =>
            {
                if (enabled != conf.Enabled) { conf.Enabled = enabled; cardChanged = true; }
                DrawTypeCardInner(conf, label, type, ref cardChanged, showLimits: true);

                if (extraContent != null)
                {
                    extraContent(avail);
                }
            },
            drawHeaderRight: () =>
            {
                ImGui.TextDisabled("(?)");
                if (ImGui.IsItemHovered())
                {
                    string tip = "Common Placeholders:\n";
                    tip += "- {name}: Activity Name\n";
                    tip += "- {details}: Track / Details\n";
                    tip += "- {state}: Artist / Status\n";
                    tip += "- {large_image}: Large Image Text\n";
                    tip += "- {small_image}: Small Image Text\n";
                    tip += "- {elapsed}, {duration}, {time_start}, {time_end}\n";

                    ImGui.SetTooltip(tip);
                }
            }
        );

        if (cardChanged) changed = true;
    }

    private void DrawTypeCardInner(ActivityTypeConfig conf, string label, ActivityType activityType, ref bool changed, bool showLimits)
    {
        bool localChanged = false;

        float scale = ImGuiHelpers.GlobalScale;

        ImGui.SameLine();
        ImGui.TextColored(theme.MutedText, "Priority: ");
        ImGui.SameLine();
        int prio = conf.Priority;
        ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt($"##Prio_{label.GetHashCode()}", ref prio)) { conf.Priority = prio; localChanged = true; }

        theme.SpacerY(0.5f);

        ImGui.Text("Format");
        string fmt = conf.Format;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.InputText($"##Fmt_{label.GetHashCode()}", ref fmt, 128)) { conf.Format = fmt; localChanged = true; }

        theme.SpacerY(0.5f);
        ImGui.Separator();
        theme.SpacerY(0.5f);

        bool cycle = conf.EnableCycling;
        if (ImGui.Checkbox($"Cycling Mode##{label.GetHashCode()}", ref cycle)) { conf.EnableCycling = cycle; localChanged = true; }
        theme.HoverHandIfItem();

        if (cycle)
        {
            using var indent = ImRaii.PushIndent();
            if (conf.CycleFormats == null) conf.CycleFormats = new();

            using (var table = ImRaii.Table($"##CycleFmts_{label.GetHashCode()}", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInnerV))
            {
                if (table)
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
                }
            }

            if (theme.SecondaryButton($"+ Add Cycle Format##{label.GetHashCode()}"))
            {
                conf.CycleFormats.Add("");
                localChanged = true;
            }

            theme.SpacerY(0.5f);
            ImGui.Text("Switch Interval (s)");
            int interval = conf.CycleIntervalSeconds;
            ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
            if (ImGui.DragInt($"##Int_{label.GetHashCode()}", ref interval, 1, 3, 300)) { conf.CycleIntervalSeconds = interval; localChanged = true; }
        }

        if (showLimits)
        {
            if (conf.CharLimits == null) conf.CharLimits = new();

            if (conf.CharLimits.Count == 0 && (conf.TrackLimit > 0 || conf.ArtistLimit > 0))
            {
                if (conf.TrackLimit > 0) conf.CharLimits.Add(new CharLimitRule { TargetPlaceholder = "{track}", Limit = conf.TrackLimit });
                if (conf.ArtistLimit > 0) conf.CharLimits.Add(new CharLimitRule { TargetPlaceholder = "{artist}", Limit = conf.ArtistLimit });
                conf.TrackLimit = 0;
                conf.ArtistLimit = 0;
                localChanged = true;
            }

            theme.SpacerY(0.5f);
            ImGui.Separator();
            theme.SpacerY(0.5f);

            string[] limitPlaceholders = PlaceholdersByType.TryGetValue(activityType, out var lph) ? lph : DefaultPlaceholders;

            ImGui.Text("Character Limits");
            theme.MutedLabel($"Truncate a placeholder's value to fewer characters (0 = untouched). The full title is always capped at {DiscordActivityConfig.MaxTitleLength}.");
            theme.SpacerY(0.5f);

            int? limitRemoveIdx = null;

            theme.DrawTable(
                id: $"act-charlimits-{label.GetHashCode()}",
                collection: conf.CharLimits,
                drawRow: (rule, i) =>
                {
                    int phIdx = Array.IndexOf(limitPlaceholders, rule.TargetPlaceholder);
                    if (phIdx < 0) phIdx = 0;
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.Combo($"##LimPh_{label.GetHashCode()}_{i}", ref phIdx, limitPlaceholders, limitPlaceholders.Length))
                    {
                        rule.TargetPlaceholder = limitPlaceholders[phIdx];
                        localChanged = true;
                    }
                    theme.HoverHandIfItem();

                    ImGui.TableNextColumn();
                    int lim = rule.Limit;
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.InputInt($"##LimVal_{label.GetHashCode()}_{i}", ref lim))
                    {
                        rule.Limit = Math.Clamp(lim, 0, DiscordActivityConfig.MaxTitleLength);
                        localChanged = true;
                    }

                    ImGui.TableNextColumn();
                    if (theme.DangerIconButton($"##LimDel_{label.GetHashCode()}_{i}", FontAwesomeIcon.Trash, "Remove")) limitRemoveIdx = i;
                },
                headers: new[] { "Field", "Limit", "##Del" },
                setupColumns: () =>
                {
                    float limDelW = ImGui.GetFrameHeight() + ImGui.GetStyle().FramePadding.X * 2f;
                    ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Limit", ImGuiTableColumnFlags.WidthFixed, 100f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("##Del", ImGuiTableColumnFlags.WidthFixed, limDelW);
                },
                showHeaders: true
            );

            if (limitRemoveIdx.HasValue)
            {
                conf.CharLimits.RemoveAt(limitRemoveIdx.Value);
                localChanged = true;
            }

            theme.SpacerY(0.5f);
            float limBtnWidth = ImGui.GetContentRegionAvail().X * 0.95f;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X - limBtnWidth) * 0.5f);
            if (theme.SecondaryButton($"+ Add Char Limit##{label.GetHashCode()}", new Vector2(limBtnWidth, 0)))
            {
                conf.CharLimits.Add(new CharLimitRule { TargetPlaceholder = limitPlaceholders[0] });
                localChanged = true;
            }

            theme.SpacerY(0.5f);
            ImGui.Separator();
            theme.SpacerY(0.5f);
        }

        ImGui.Text("Title Colors");
        using (var group2 = ImRaii.Group())
        {
            Vector3? cVal = conf.Color;
            bool hasColor = cVal.HasValue;
            if (ImGui.Checkbox($"Override Color##{label.GetHashCode()}", ref hasColor))
            {
                conf.Color = hasColor ? new Vector3(1, 1, 1) : null;
                localChanged = true;
            }
            theme.HoverHandIfItem();
            if (hasColor)
            {
                ImGui.SameLine();
                Vector3 col = conf.Color ?? new Vector3(1, 1, 1);
                if (ImGui.ColorEdit3($"##ColPick_{label.GetHashCode()}", ref col, ImGuiColorEditFlags.NoInputs))
                {
                    conf.Color = col;
                    localChanged = true;
                }
                theme.HoverHandIfItem();
            }

            ImGui.SameLine();
            theme.SpacerX(2f);
            ImGui.SameLine();

            Vector3? gVal = conf.Glow;
            bool hasGlow = gVal.HasValue;
            if (ImGui.Checkbox($"Override Glow##{label.GetHashCode()}", ref hasGlow))
            {
                conf.Glow = hasGlow ? new Vector3(1, 1, 1) : null;
                localChanged = true;
            }
            theme.HoverHandIfItem();
            if (hasGlow)
            {
                ImGui.SameLine();
                Vector3 glo = conf.Glow ?? new Vector3(1, 1, 1);
                if (ImGui.ColorEdit3($"##GlowPick_{label.GetHashCode()}", ref glo, ImGuiColorEditFlags.NoInputs))
                {
                    conf.Glow = glo;
                    localChanged = true;
                }
                theme.HoverHandIfItem();
            }
        }

        theme.SpacerY(0.5f);
        ImGui.Separator();
        theme.SpacerY(0.5f);

        ImGui.Text("Gradient Title");
        using (var group3 = ImRaii.Group())
        {
            bool hasGradient = conf.GradientColourSet.HasValue;
            if (ImGui.Checkbox($"Enable Gradient##{label.GetHashCode()}_grad", ref hasGradient))
            {
                conf.GradientColourSet = hasGradient ? 0 : null;
                conf.GradientAnimationStyle = hasGradient ? 0 : null;
                localChanged = true;
            }
            theme.HoverHandIfItem();

            if (hasGradient)
            {
                ImGui.SameLine();
                theme.SpacerX(1f);
                ImGui.SameLine();

                int preset = conf.GradientColourSet ?? 0;
                ImGui.Text("Preset");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
                if (ImGui.InputInt($"##GradPreset_{label.GetHashCode()}", ref preset))
                {
                    if (preset < -1) preset = -1;
                    conf.GradientColourSet = preset;
                    localChanged = true;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Gradient colour set index from Honorific.\n-1 = Two Colour Gradient (uses the Override Color & Glow above as gradient colours).");

                ImGui.SameLine();
                theme.SpacerX(1f);
                ImGui.SameLine();

                string[] animStyles = { "Pulse", "Wave", "Static" };
                int animIdx = conf.GradientAnimationStyle ?? 0;
                if (animIdx < 0 || animIdx >= animStyles.Length) animIdx = 0;
                ImGui.Text("Animation");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
                if (ImGui.Combo($"##GradAnim_{label.GetHashCode()}", ref animIdx, animStyles, animStyles.Length))
                {
                    conf.GradientAnimationStyle = animIdx;
                    localChanged = true;
                }
                theme.HoverHandIfItem();
            }
        }

        theme.SpacerY(0.5f);
        ImGui.Separator();
        theme.SpacerY(0.5f);

        string[] placeholders = PlaceholdersByType.TryGetValue(activityType, out var ph) ? ph : DefaultPlaceholders;
        int? filterRemoveIdx = null;

        ImGui.Text("Blacklist Filters");
        theme.MutedLabel("Activities matching any filter rule below will be ignored.");
        theme.SpacerY(0.5f);

        theme.DrawTable(
            id: $"act-filters-{label.GetHashCode()}",
            collection: conf.Filters,
            drawRow: (filter, i) =>
            {
                int phIdx = Array.IndexOf(placeholders, filter.TargetPlaceholder);
                if (phIdx < 0) phIdx = 0;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.Combo($"##FiltPh_{label.GetHashCode()}_{i}", ref phIdx, placeholders, placeholders.Length))
                {
                    filter.TargetPlaceholder = placeholders[phIdx];
                    localChanged = true;
                }

                ImGui.TableNextColumn();
                int modeIdx = (int)filter.Mode;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.Combo($"##FiltMode_{label.GetHashCode()}_{i}", ref modeIdx, FilterModeLabels, FilterModeLabels.Length))
                {
                    filter.Mode = (FilterMode)modeIdx;
                    localChanged = true;
                }

                ImGui.TableNextColumn();
                string val = filter.Value;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText($"##FiltVal_{label.GetHashCode()}_{i}", ref val, 128))
                {
                    filter.Value = val;
                    localChanged = true;
                }

                ImGui.TableNextColumn();
                if (theme.DangerIconButton($"##FiltDel_{label.GetHashCode()}_{i}", FontAwesomeIcon.Trash, "Remove")) filterRemoveIdx = i;
            },
            headers: new[] { "Field", "Mode", "Value", "##Del" },
            setupColumns: () =>
            {
                float filtDelW = ImGui.GetFrameHeight() + ImGui.GetStyle().FramePadding.X * 2f;
                ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthFixed, 120f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Mode", ImGuiTableColumnFlags.WidthFixed, 100f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("##Del", ImGuiTableColumnFlags.WidthFixed, filtDelW);
            },
            showHeaders: true
        );

        if (filterRemoveIdx.HasValue)
        {
            conf.Filters.RemoveAt(filterRemoveIdx.Value);
            localChanged = true;
        }

        theme.SpacerY(0.5f);
        float filterBtnWidth = ImGui.GetContentRegionAvail().X * 0.95f;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X - filterBtnWidth) * 0.5f);
        if (theme.SecondaryButton($"+ Add Filter##{label.GetHashCode()}", new Vector2(filterBtnWidth, 0)))
        {
            conf.Filters.Add(new FilterRule { TargetPlaceholder = placeholders[0] });
            localChanged = true;
        }

        if (localChanged) changed = true;
    }
}
