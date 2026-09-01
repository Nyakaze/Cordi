using System;
using System.Collections.Generic;
using System.Linq;
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
    private void DrawCustomPage()
    {
        EnsureCustomPresets();

        int active = Math.Clamp(Config.ActiveCustomPreset, 0, Config.CustomPresets.Count - 1);
        var conf = Config.CustomPresets[active].Config;

        DrawTypePage(
            "activity-custom",
            ActivityType.Custom,
            "Custom",
            "A standalone title that does not need a Discord account",
            conf,
            requiresUserId: false,
            drawLead: () => DrawPresetsCard(active, conf));

        foreach (var preset in Config.CustomPresets)
        {
            preset.Config.Enabled = conf.Enabled;
            preset.Config.Priority = conf.Priority;
        }
    }

    private void DrawPresetsCard(int active, ActivityTypeConfig conf)
    {
        Card.Draw(
            "activity-custom-presets",
            innerWidth =>
            {
                theme.PushInputScope();

                Row.Draw(
                    id: "activity-custom-active",
                    icon: FontAwesomeIcon.Bookmark,
                    iconColor: UiTheme.TileAmber,
                    title: "Active preset",
                    subtitle: "The preset currently driving the custom title",
                    controlWidth: 220f,
                    drawControl: (pos, width) =>
                    {
                        ImGui.SetCursorScreenPos(pos);
                        theme.OptionPicker(
                            "activity-custom-picker",
                            active.ToString(),
                            PresetItems(),
                            key =>
                            {
                                if (int.TryParse(key, out var index))
                                    SelectPreset(index);
                            },
                            width: width);
                    },
                    rowWidth: innerWidth);

                Row.Draw(
                    id: "activity-custom-name",
                    icon: FontAwesomeIcon.Tag,
                    iconColor: UiTheme.TileAmber,
                    title: "Preset name",
                    subtitle: "Only used to tell your presets apart",
                    controlWidth: 220f,
                    drawControl: (pos, width) =>
                    {
                        string name = Config.CustomPresets[active].Name;

                        theme.TextInput("##activity-custom-name-input", pos, width, ref name, 64, "Preset name");
                        Config.CustomPresets[active].Name = name;

                        if (ImGui.IsItemDeactivatedAfterEdit())
                            Save();
                    },
                    rowWidth: innerWidth);

                theme.SpacerY(0.5f);
                DrawPresetButtons(active, conf, innerWidth);

                theme.PopInputScope();
            },
            label: "Presets",
            drawTrailing: anchor => DrawCountChip(anchor, Config.CustomPresets.Count, "preset"));
    }

    private void DrawPresetButtons(int active, ActivityTypeConfig conf, float innerWidth)
    {
        float gap = theme.Gap();
        float height = theme.Scaled(32f);
        float buttonWidth = (innerWidth - gap) * 0.5f;
        var origin = ImGui.GetCursorScreenPos();

        ImGui.SetCursorScreenPos(origin);
        if (theme.SecondaryButton("+ New Preset##activity-custom-new", new Vector2(buttonWidth, height)))
        {
            var preset = new ActivityPreset
            {
                Name = UniquePresetName("Preset"),
                Config = new ActivityTypeConfig
                {
                    Enabled = conf.Enabled,
                    Priority = conf.Priority,
                    Format = "{state}",
                },
            };

            Config.CustomPresets.Add(preset);
            Config.ActiveCustomPreset = Config.CustomPresets.Count - 1;
            Config.TypeConfigs[ActivityType.Custom] = preset.Config;
            Save();
        }
        theme.HoverHandIfItem();

        ImGui.SetCursorScreenPos(new Vector2(origin.X + buttonWidth + gap, origin.Y));

        if (Config.CustomPresets.Count <= 1)
        {
            theme.MutedLabel("Delete Preset");
        }
        else if (theme.SecondaryButton("Delete Preset##activity-custom-del", new Vector2(buttonWidth, height)))
        {
            Config.CustomPresets.RemoveAt(active);
            Config.ActiveCustomPreset = Math.Clamp(active, 0, Config.CustomPresets.Count - 1);
            Config.TypeConfigs[ActivityType.Custom] = Config.CustomPresets[Config.ActiveCustomPreset].Config;
            Save();
        }
        theme.HoverHandIfItem();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(innerWidth, height));
    }

    private IReadOnlyList<DropdownItem> PresetItems()
    {
        var items = new List<DropdownItem>(Config.CustomPresets.Count);

        for (int index = 0; index < Config.CustomPresets.Count; index++)
            items.Add(new DropdownItem { Key = index.ToString(), Label = Config.CustomPresets[index].Name });

        return items;
    }

    private void SelectPreset(int index)
    {
        if (index < 0 || index >= Config.CustomPresets.Count || index == Config.ActiveCustomPreset)
            return;

        var previous = Config.CustomPresets[Math.Clamp(Config.ActiveCustomPreset, 0, Config.CustomPresets.Count - 1)].Config;
        var next = Config.CustomPresets[index].Config;

        next.Enabled = previous.Enabled;
        next.Priority = previous.Priority;

        Config.ActiveCustomPreset = index;
        Config.TypeConfigs[ActivityType.Custom] = next;
        Save();
    }

    private void EnsureCustomPresets()
    {
        bool changed = false;

        Config.CustomPresets ??= new List<ActivityPreset>();

        if (Config.CustomPresets.Count == 0)
        {
            if (!Config.TypeConfigs.TryGetValue(ActivityType.Custom, out var existing) || existing is null)
            {
                existing = new ActivityTypeConfig { Enabled = true, Priority = 0, Format = "{state}" };
                Config.TypeConfigs[ActivityType.Custom] = existing;
            }

            Config.CustomPresets.Add(new ActivityPreset { Name = "Default", Config = existing });
            Config.ActiveCustomPreset = 0;
            changed = true;
        }

        Config.ActiveCustomPreset = Math.Clamp(Config.ActiveCustomPreset, 0, Config.CustomPresets.Count - 1);

        var activeConfig = Config.CustomPresets[Config.ActiveCustomPreset].Config;

        if (activeConfig is null)
        {
            activeConfig = new ActivityTypeConfig { Enabled = true, Priority = 0, Format = "{state}" };
            Config.CustomPresets[Config.ActiveCustomPreset].Config = activeConfig;
            changed = true;
        }

        Config.TypeConfigs.TryGetValue(ActivityType.Custom, out var current);

        if (!ReferenceEquals(current, activeConfig))
        {
            Config.TypeConfigs[ActivityType.Custom] = activeConfig;
            changed = true;
        }

        if (changed)
            Save();
    }

    private string UniquePresetName(string baseName)
    {
        string name = baseName;
        int index = 1;

        while (Config.CustomPresets.Any(preset => preset.Name == name))
            name = $"{baseName} {index++}";

        return name;
    }
}
