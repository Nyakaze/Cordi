using System;
using Crovus.Models;
using System.Collections.Generic;
using System.Linq;

namespace Cordi.Configuration;

[Serializable]
public class DiscordActivityConfig
{
    public const int MaxTitleLength = 32;

    public ulong TargetUserId { get; set; } = 0;
    public bool PrefixTitle { get; set; } = false;
    public Dictionary<string, string> Replacements { get; set; } = new();

    public Dictionary<string, ActivityTypeConfig> GameConfigs { get; set; } = new();

    public Dictionary<ActivityType, ActivityTypeConfig> TypeConfigs { get; set; } = new()
    {
        { ActivityType.Playing, new ActivityTypeConfig { Priority = 1, Enabled = true, Format = "Playing {name}" } },
        { ActivityType.ListeningTo, new ActivityTypeConfig { Priority = 2, Enabled = true, Format = "Listening to {details}" } },
        { ActivityType.Watching, new ActivityTypeConfig { Priority = 0, Enabled = true, Format = "Watching {name}" } },
        { ActivityType.Custom, new ActivityTypeConfig { Priority = 3, Enabled = true, Format = "{state}" } },

    };

    public List<ActivityPreset> CustomPresets { get; set; } = new();
    public int ActiveCustomPreset { get; set; } = 0;

    public EmoteTitleSettings EmoteTitles { get; set; } = new();

    public bool SyncCustomPreset(string? characterName, uint worldId)
    {
        if (CustomPresets.Count == 0) return false;

        int active = Math.Clamp(ActiveCustomPreset, 0, CustomPresets.Count - 1);

        if (CustomPresets[active].VisibleTo(characterName, worldId)) return false;

        var visible = Enumerable.Range(0, CustomPresets.Count)
            .Where(index => CustomPresets[index].VisibleTo(characterName, worldId))
            .ToList();

        if (visible.Count == 0) return false;

        int next = visible.FirstOrDefault(index => !CustomPresets[index].IsGlobal(), visible[0]);
        var previous = CustomPresets[active].Config;
        var config = CustomPresets[next].Config;

        config.Enabled = previous.Enabled;
        config.Priority = previous.Priority;

        ActiveCustomPreset = next;
        TypeConfigs[ActivityType.Custom] = config;

        return true;
    }

    public string UniqueCustomPresetName(string baseName)
    {
        string name = baseName;
        int index = 1;

        while (CustomPresets.Any(preset => preset.Name == name))
            name = $"{baseName} {index++}";

        return name;
    }

    public ActivityTypes EnabledTypes() => TypeConfigs
        .Where(entry => entry.Value is { Enabled: true })
        .Aggregate(ActivityTypes.None, (types, entry) => types | entry.Key.ToFlag());
}

[Serializable]
public class ActivityPreset
{
    public string Name { get; set; } = "Default";
    public ActivityTypeConfig Config { get; set; } = new();

    public string CharacterName { get; set; } = string.Empty;
    public uint CharacterWorldId { get; set; } = 0;

    public bool IsGlobal() => string.IsNullOrEmpty(CharacterName);

    public bool BelongsTo(string? characterName, uint worldId) =>
        !IsGlobal() && CharacterWorldId == worldId && string.Equals(CharacterName, characterName, StringComparison.Ordinal);

    public bool VisibleTo(string? characterName, uint worldId) =>
        IsGlobal() || string.IsNullOrEmpty(characterName) || BelongsTo(characterName, worldId);
}

[Serializable]
public class ActivityTypeConfig
{
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 0;
    public string Format { get; set; } = "";


    public bool EnableCycling { get; set; } = false;
    public List<string> CycleFormats { get; set; } = new();
    public int CycleIntervalSeconds { get; set; } = 10;


    public int TrackLimit { get; set; } = 0;
    public int ArtistLimit { get; set; } = 0;

    public List<CharLimitRule> CharLimits { get; set; } = new();

    public bool DynamicTrim { get; set; } = true;


    public System.Numerics.Vector3? Color { get; set; } = null;
    public System.Numerics.Vector3? Glow { get; set; } = null;

    public int? GradientColourSet { get; set; } = null;
    public int? GradientAnimationStyle { get; set; } = null;

    public List<FilterRule> Filters { get; set; } = new();
}

[Serializable]
public class CharLimitRule
{
    public string TargetPlaceholder { get; set; } = "{name}";

    public int Limit { get; set; } = 0;
}

[Serializable]
public class FilterRule
{

    public string TargetPlaceholder { get; set; } = "{name}";


    public string Value { get; set; } = "";


    public FilterMode Mode { get; set; } = FilterMode.Contains;
}

public enum FilterMode
{
    Contains,
    Equals,
    StartsWith,
    EndsWith,
    Regex
}
