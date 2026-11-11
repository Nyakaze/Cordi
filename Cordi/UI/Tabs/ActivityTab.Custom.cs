using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Extensions;
using Cordi.Services.Activity;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Crovus.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ActivityTab
{
    private const string GlobalScopeKey = "global";
    private const string CharacterScopeKey = "character";
    private const string OtherScopeKey = "other";

    private bool honorificImportPending;
    private string honorificImportMessage = string.Empty;

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
            drawLead: () =>
            {
                DrawPresetsCard(active, conf);
                DrawHonorificImportCard();
            });

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

                Row.Draw(
                    id: "activity-custom-scope",
                    icon: FontAwesomeIcon.User,
                    iconColor: UiTheme.TileAmber,
                    title: "Available on",
                    subtitle: "Global presets show on every character, character presets only on one",
                    controlWidth: 220f,
                    drawControl: (pos, width) =>
                    {
                        var preset = Config.CustomPresets[active];

                        ImGui.SetCursorScreenPos(pos);
                        theme.OptionPicker(
                            "activity-custom-scope-picker",
                            ScopeKey(preset),
                            ScopeItems(preset),
                            key => SetScope(preset, key),
                            width: width);
                    },
                    rowWidth: innerWidth);

                theme.SpacerY(0.5f);
                DrawPresetButtons(active, conf, innerWidth);

                theme.PopInputScope();
            },
            label: "Presets",
            drawTrailing: anchor => DrawCountChip(anchor, VisiblePresetIndices().Count, "preset"));
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
                Name = Config.UniqueCustomPresetName("Preset"),
                Config = new ActivityTypeConfig
                {
                    Enabled = conf.Enabled,
                    Priority = conf.Priority,
                    Format = "{state}",
                },
            };

            if (!Config.CustomPresets[active].IsGlobal() && CurrentCharacter() is { } character)
            {
                preset.CharacterName = character.Name;
                preset.CharacterWorldId = character.WorldId;
            }

            Config.CustomPresets.Add(preset);
            Config.ActiveCustomPreset = Config.CustomPresets.Count - 1;
            Config.TypeConfigs[ActivityType.Custom] = preset.Config;
            Save();
        }
        theme.HoverHandIfItem();

        ImGui.SetCursorScreenPos(new Vector2(origin.X + buttonWidth + gap, origin.Y));

        if (VisiblePresetIndices().Count <= 1)
        {
            theme.MutedLabel("Delete Preset");
        }
        else if (theme.SecondaryButton("Delete Preset##activity-custom-del", new Vector2(buttonWidth, height)))
        {
            Config.CustomPresets.RemoveAt(active);
            Config.ActiveCustomPreset = Math.Clamp(active, 0, Config.CustomPresets.Count - 1);
            Config.TypeConfigs[ActivityType.Custom] = Config.CustomPresets[Config.ActiveCustomPreset].Config;
            SyncPresetToCharacter();
            Save();
        }
        theme.HoverHandIfItem();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(innerWidth, height));
    }

    private void DrawHonorificImportCard()
    {
        Card.Draw(
            "activity-custom-honorific",
            innerWidth =>
            {
                theme.PushInputScope();

                bool available = HonorificTitleImporter.Available;

                DrawButtonRow(
                    "activity-custom-honorific-import",
                    FontAwesomeIcon.FileImport,
                    available ? UiTheme.TileGreen : theme.MutedText,
                    "Import titles from Honorific",
                    available
                        ? "Turns each Honorific title into a preset. Titles you already have are skipped."
                        : "No Honorific config found next to Cordi's own.",
                    "Import",
                    innerWidth,
                    () =>
                    {
                        honorificImportMessage = available ? string.Empty : "No Honorific config found.";
                        honorificImportPending = available;
                    },
                    controlWidth: 120f);

                if (honorificImportPending)
                {
                    theme.SpacerY(0.4f);
                    theme.WrappedText(
                        "Honorific keeps titles per character. Make them global presets for every character, or keep each one on the character it came from?",
                        innerWidth,
                        theme.MutedText);
                    theme.SpacerY(0.4f);
                    theme.ButtonRow(
                        innerWidth,
                        new UiBarButton
                        {
                            Label = "Keep per character##activity-custom-honorific-character",
                            Width = 170f,
                            Primary = true,
                            OnClick = () => RunHonorificImport(asGlobal: false),
                        },
                        new UiBarButton
                        {
                            Label = "Make global##activity-custom-honorific-global",
                            Width = 130f,
                            OnClick = () => RunHonorificImport(asGlobal: true),
                        },
                        new UiBarButton
                        {
                            Label = "Cancel##activity-custom-honorific-cancel",
                            Width = 100f,
                            OnClick = () => honorificImportPending = false,
                        });
                }

                if (!string.IsNullOrEmpty(honorificImportMessage))
                {
                    theme.SpacerY(0.4f);
                    theme.WrappedText(honorificImportMessage, innerWidth, theme.FaintText);
                }

                theme.PopInputScope();
            },
            label: "Honorific");
    }

    private void RunHonorificImport(bool asGlobal)
    {
        honorificImportPending = false;

        var result = new HonorificTitleImporter(plugin.Config, plugin.LogService).Import(asGlobal);

        honorificImportMessage = result.Message;

        if (result.Titles > 0 && SyncPresetToCharacter())
            Save();
    }

    private IReadOnlyList<DropdownItem> PresetItems()
    {
        var visible = VisiblePresetIndices();
        var items = new List<DropdownItem>(visible.Count);

        foreach (int index in visible)
        {
            var preset = Config.CustomPresets[index];
            string label = preset.IsGlobal() ? preset.Name : $"{preset.Name} ({preset.CharacterName})";

            items.Add(new DropdownItem { Key = index.ToString(), Label = label });
        }

        return items;
    }

    private List<int> VisiblePresetIndices()
    {
        var character = CurrentCharacter();

        return Enumerable.Range(0, Config.CustomPresets.Count)
            .Where(index => Config.CustomPresets[index].VisibleTo(character?.Name, character?.WorldId ?? 0))
            .ToList();
    }

    private static (string Name, uint WorldId)? CurrentCharacter()
    {
        var player = Service.ObjectTable.LocalPlayer;

        return player is null ? null : (player.Name.TextValue, player.HomeWorld.RowId);
    }

    private bool SyncPresetToCharacter()
    {
        var character = CurrentCharacter();

        return Config.SyncCustomPreset(character?.Name, character?.WorldId ?? 0);
    }

    private static string CharacterLabel(string name, uint worldId)
    {
        string world = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>()
            ?.GetRowOrDefault(worldId)?.Name.ExtractText() ?? worldId.ToString();

        return $"{name} @ {world}";
    }

    private static string ScopeKey(ActivityPreset preset)
    {
        if (preset.IsGlobal()) return GlobalScopeKey;

        return CurrentCharacter() is { } current && preset.BelongsTo(current.Name, current.WorldId)
            ? CharacterScopeKey
            : OtherScopeKey;
    }

    private static IReadOnlyList<DropdownItem> ScopeItems(ActivityPreset preset)
    {
        var items = new List<DropdownItem> { new() { Key = GlobalScopeKey, Label = "All characters" } };

        if (CurrentCharacter() is { } character)
            items.Add(new DropdownItem { Key = CharacterScopeKey, Label = CharacterLabel(character.Name, character.WorldId) });

        if (ScopeKey(preset) == OtherScopeKey)
            items.Add(new DropdownItem { Key = OtherScopeKey, Label = CharacterLabel(preset.CharacterName, preset.CharacterWorldId) });

        return items;
    }

    private void SetScope(ActivityPreset preset, string key)
    {
        if (key == GlobalScopeKey)
        {
            preset.CharacterName = string.Empty;
            preset.CharacterWorldId = 0;
        }
        else if (key == CharacterScopeKey && CurrentCharacter() is { } character)
        {
            preset.CharacterName = character.Name;
            preset.CharacterWorldId = character.WorldId;
        }
        else
        {
            return;
        }

        Save();
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

        if (VisiblePresetIndices().Count == 0)
        {
            Config.CustomPresets.Add(new ActivityPreset
            {
                Name = Config.UniqueCustomPresetName("Default"),
                Config = new ActivityTypeConfig { Enabled = true, Priority = 0, Format = "{state}" },
            });
            changed = true;
        }

        Config.ActiveCustomPreset = Math.Clamp(Config.ActiveCustomPreset, 0, Config.CustomPresets.Count - 1);

        if (Config.CustomPresets[Config.ActiveCustomPreset].Config is null)
        {
            Config.CustomPresets[Config.ActiveCustomPreset].Config =
                new ActivityTypeConfig { Enabled = true, Priority = 0, Format = "{state}" };
            changed = true;
        }

        if (SyncPresetToCharacter())
            changed = true;

        var activeConfig = Config.CustomPresets[Config.ActiveCustomPreset].Config;

        Config.TypeConfigs.TryGetValue(ActivityType.Custom, out var current);

        if (!ReferenceEquals(current, activeConfig))
        {
            Config.TypeConfigs[ActivityType.Custom] = activeConfig;
            changed = true;
        }

        if (changed)
            Save();
    }
}
