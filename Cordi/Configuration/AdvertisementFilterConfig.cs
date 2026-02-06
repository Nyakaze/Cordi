using System;
using System.Collections.Generic;

namespace Cordi.Configuration;

[Serializable]
public class AdvertisementFilterConfig
{
    public bool Enabled { get; set; } = true;
    public int ScoreThreshold { get; set; } = 3;

    // High-score patterns (2 points each)
    public List<string> HighScoreRegexPatterns { get; set; } = new();
    public List<string> HighScoreKeywords { get; set; } = new();

    // Medium-score patterns (1 point each)
    public List<string> MediumScoreRegexPatterns { get; set; } = new();
    public List<string> MediumScoreKeywords { get; set; } = new();

    // Whitelist - messages containing these will never be blocked
    public List<string> Whitelist { get; set; } = new();

    // Track if defaults have been initialized
    public bool DefaultsInitialized { get; set; } = false;

    public void InitializeDefaults()
    {
        if (DefaultsInitialized) return;

        HighScoreRegexPatterns.AddRange(new[]
        {
            @"discord\.gg/\w+",
            @"https?://discord\.gg/\w+",
            @"\b(ward|plot)\s*\d+",
            @"\bw\d+\s*p\d+\b"
        });

        HighScoreKeywords.AddRange(new[]
        {
            "dj", "venue", "giveaway", "gamba", "bingo", "raffle",
            "contest", "photography", "photographer", "bar", "vip",
            "dancers", "glam contest"
        });

        MediumScoreRegexPatterns.AddRange(new[]
        {
            @"\d+\s*(pm|am)\s*st\b",
            @"\b(light|alpha|raiden|odin|phoenix|shiva|goblet|mist|lavender\s*beds?|empyreum|shirogane)\b"
        });

        MediumScoreKeywords.AddRange(new[]
        {
            "tonight", "today", "event", "party", "club", "open now",
            "join us", "tune in", "celebrate"
        });

        DefaultsInitialized = true;
    }
}
