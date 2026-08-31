using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cordi.Configuration;

namespace Cordi.Services.Discord;

public readonly struct FilterEvaluation
{
    public int Score { get; init; }
    public int Threshold { get; init; }
    public bool Whitelisted { get; init; }
    public IReadOnlyList<string> Matches { get; init; }

    public bool Blocked => !Whitelisted && Score >= Threshold;
}

public static class AdvertisementFilter
{
    private const int HighKeywordsNeeded = 3;
    private const int MediumKeywordsNeeded = 2;
    private const int UppercaseWordsNeeded = 5;

    public static bool IsAdvertisement(string message, AdvertisementFilterConfig config) =>
        Evaluate(message, config).Blocked;

    public static FilterEvaluation Evaluate(string message, AdvertisementFilterConfig config)
    {
        var matches = new List<string>();

        if (string.IsNullOrWhiteSpace(message))
            return new FilterEvaluation { Threshold = config.ScoreThreshold, Matches = matches };

        var lowerMessage = message.ToLowerInvariant();

        foreach (var phrase in config.Whitelist)
        {
            if (string.IsNullOrWhiteSpace(phrase) || !lowerMessage.Contains(phrase.ToLowerInvariant()))
                continue;

            return new FilterEvaluation
            {
                Threshold = config.ScoreThreshold,
                Whitelisted = true,
                Matches = new List<string> { $"Whitelisted by \"{phrase}\"" },
            };
        }

        int score = 0;

        foreach (var pattern in config.Patterns)
        {
            if (pattern.Kind != FilterPatternKind.Regex || string.IsNullOrWhiteSpace(pattern.Value))
                continue;

            if (!MatchesRegex(message, pattern.Value))
                continue;

            int points = (int)pattern.Weight;
            score += points;
            matches.Add($"Regex \"{pattern.Value}\" +{points}");
        }

        score += ScoreKeywords(config, lowerMessage, FilterPatternWeight.High, HighKeywordsNeeded, matches);
        score += ScoreKeywords(config, lowerMessage, FilterPatternWeight.Medium, MediumKeywordsNeeded, matches);

        if (CountConsecutiveUppercase(message) >= UppercaseWordsNeeded)
        {
            score += 1;
            matches.Add("Shouting +1");
        }

        if (Regex.IsMatch(message, @"[♪♥♦◆→←]{3,}"))
        {
            score += 1;
            matches.Add("Symbol spam +1");
        }

        return new FilterEvaluation
        {
            Score = score,
            Threshold = config.ScoreThreshold,
            Matches = matches,
        };
    }

    public static bool IsValidRegex(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        try
        {
            _ = Regex.Match(string.Empty, pattern);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static int ScoreKeywords(
        AdvertisementFilterConfig config,
        string lowerMessage,
        FilterPatternWeight weight,
        int needed,
        List<string> matches)
    {
        var hits = config.Patterns
            .Where(pattern => pattern.Kind == FilterPatternKind.Keyword
                              && pattern.Weight == weight
                              && !string.IsNullOrWhiteSpace(pattern.Value)
                              && lowerMessage.Contains(pattern.Value.ToLowerInvariant()))
            .Select(pattern => pattern.Value)
            .ToList();

        if (hits.Count == 0)
            return 0;

        string label = weight.ToString().ToLowerInvariant();
        string list = string.Join(", ", hits);

        if (hits.Count < needed)
        {
            matches.Add($"{hits.Count}/{needed} {label} keywords, no points ({list})");
            return 0;
        }

        int points = (int)weight;
        matches.Add($"{hits.Count} {label} keywords +{points} ({list})");
        return points;
    }

    private static bool MatchesRegex(string message, string pattern)
    {
        try
        {
            return Regex.IsMatch(message, pattern, RegexOptions.IgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static int CountConsecutiveUppercase(string message)
    {
        var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int consecutive = 0;
        int longest = 0;

        foreach (var word in words)
        {
            if (word.Length > 1 && word.All(c => !char.IsLetter(c) || char.IsUpper(c)))
            {
                consecutive++;
                longest = Math.Max(longest, consecutive);
                continue;
            }

            consecutive = 0;
        }

        return longest;
    }
}
