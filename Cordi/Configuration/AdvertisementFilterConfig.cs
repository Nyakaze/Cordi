using System;
using System.Collections.Generic;

namespace Cordi.Configuration;

public enum FilterPatternKind
{
    Keyword,
    Regex,
}

public enum FilterPatternWeight
{
    Medium = 1,
    High = 2,
}

[Serializable]
public class FilterPattern
{
    public string Value { get; set; } = string.Empty;
    public FilterPatternKind Kind { get; set; } = FilterPatternKind.Keyword;
    public FilterPatternWeight Weight { get; set; } = FilterPatternWeight.Medium;
}

[Serializable]
public class AdvertisementFilterConfig
{
    public int ScoreThreshold { get; set; } = 3;

    public List<FilterPattern> Patterns { get; set; } = new();
    public List<string> Whitelist { get; set; } = new();

    public List<string> HighScoreRegexPatterns { get; set; } = new();
    public List<string> HighScoreKeywords { get; set; } = new();
    public List<string> MediumScoreRegexPatterns { get; set; } = new();
    public List<string> MediumScoreKeywords { get; set; } = new();

    public bool DefaultsInitialized { get; set; } = false;
    public bool PatternsMigrated { get; set; } = false;

    public void InitializeDefaults()
    {
        MigrateLegacyPatterns();

        if (DefaultsInitialized) return;

        Add(FilterPatternKind.Regex, FilterPatternWeight.High, new[]
        {
            @"discord\.gg/\w+",
            @"https?://discord\.gg/\w+",
            @"\b(ward|plot)\s*\d+",
            @"\bw\d+\s*p\d+\b"
        });

        Add(FilterPatternKind.Keyword, FilterPatternWeight.High, new[]
        {
            "dj", "venue", "giveaway", "gamba", "bingo", "raffle",
            "contest", "photography", "photographer", "bar", "vip",
            "dancers", "glam contest"
        });

        Add(FilterPatternKind.Regex, FilterPatternWeight.Medium, new[]
        {
            @"\d+\s*(pm|am)\s*st\b",
            @"\b(light|alpha|raiden|odin|phoenix|shiva|goblet|mist|lavender\s*beds?|empyreum|shirogane)\b"
        });

        Add(FilterPatternKind.Keyword, FilterPatternWeight.Medium, new[]
        {
            "tonight", "today", "event", "party", "club", "open now",
            "join us", "tune in", "celebrate"
        });

        DefaultsInitialized = true;
    }

    public void RestoreDefaults()
    {
        DefaultsInitialized = false;
        InitializeDefaults();
    }

    private void MigrateLegacyPatterns()
    {
        if (PatternsMigrated) return;

        Add(FilterPatternKind.Regex, FilterPatternWeight.High, HighScoreRegexPatterns);
        Add(FilterPatternKind.Keyword, FilterPatternWeight.High, HighScoreKeywords);
        Add(FilterPatternKind.Regex, FilterPatternWeight.Medium, MediumScoreRegexPatterns);
        Add(FilterPatternKind.Keyword, FilterPatternWeight.Medium, MediumScoreKeywords);

        HighScoreRegexPatterns.Clear();
        HighScoreKeywords.Clear();
        MediumScoreRegexPatterns.Clear();
        MediumScoreKeywords.Clear();

        PatternsMigrated = true;
    }

    private void Add(FilterPatternKind kind, FilterPatternWeight weight, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (Contains(value, kind))
                continue;

            Patterns.Add(new FilterPattern { Value = value, Kind = kind, Weight = weight });
        }
    }

    private bool Contains(string value, FilterPatternKind kind)
    {
        foreach (var pattern in Patterns)
        {
            if (pattern.Kind == kind && string.Equals(pattern.Value, value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
