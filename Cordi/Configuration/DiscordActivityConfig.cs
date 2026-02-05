using System;
using DSharpPlus.Entities;
using System.Collections.Generic;

namespace Cordi.Configuration;

[Serializable]
public class DiscordActivityConfig
{
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

    public bool Enabled { get; set; } = true;
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


    public System.Numerics.Vector3? Color { get; set; } = null;
    public System.Numerics.Vector3? Glow { get; set; } = null;


    public List<FilterRule> Filters { get; set; } = new();
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
