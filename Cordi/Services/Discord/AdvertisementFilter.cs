using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Cordi.Services.Discord;

public static class AdvertisementFilter
{
    public static bool IsAdvertisement(
        string message,
        int scoreThreshold,
        List<string> highScoreRegexPatterns,
        List<string> highScoreKeywords,
        List<string> mediumScoreRegexPatterns,
        List<string> mediumScoreKeywords,
        List<string> whitelist)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var lowerMessage = message.ToLowerInvariant();

        // Check whitelist first
        foreach (var phrase in whitelist)
        {
            if (!string.IsNullOrWhiteSpace(phrase) &&
                lowerMessage.Contains(phrase.ToLowerInvariant()))
            {
                return false;
            }
        }

        int score = 0;

        // High-score regex patterns (2 points each)
        foreach (var pattern in highScoreRegexPatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;

            try
            {
                if (Regex.IsMatch(message, pattern, RegexOptions.IgnoreCase))
                {
                    score += 2;
                }
            }
            catch (Exception)
            {
                // Invalid regex, skip
            }
        }

        // High-score keywords (2 points for 3+ matches)
        int highKeywordMatches = 0;
        foreach (var keyword in highScoreKeywords)
        {
            if (!string.IsNullOrWhiteSpace(keyword) &&
                lowerMessage.Contains(keyword.ToLowerInvariant()))
            {
                highKeywordMatches++;
            }
        }
        if (highKeywordMatches >= 3)
        {
            score += 2;
        }

        // Medium-score regex patterns (1 point each)
        foreach (var pattern in mediumScoreRegexPatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;

            try
            {
                if (Regex.IsMatch(message, pattern, RegexOptions.IgnoreCase))
                {
                    score += 1;
                }
            }
            catch (Exception)
            {
                // Invalid regex, skip
            }
        }

        // Medium-score keywords (1 point for 2+ matches)
        int mediumKeywordMatches = 0;
        foreach (var keyword in mediumScoreKeywords)
        {
            if (!string.IsNullOrWhiteSpace(keyword) &&
                lowerMessage.Contains(keyword.ToLowerInvariant()))
            {
                mediumKeywordMatches++;
            }
        }
        if (mediumKeywordMatches >= 2)
        {
            score += 1;
        }

        // Check for excessive uppercase (5+ consecutive uppercase words)
        var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int consecutiveUppercase = 0;
        int maxConsecutiveUppercase = 0;

        foreach (var word in words)
        {
            if (word.Length > 1 && word.All(c => !char.IsLetter(c) || char.IsUpper(c)))
            {
                consecutiveUppercase++;
                maxConsecutiveUppercase = Math.Max(maxConsecutiveUppercase, consecutiveUppercase);
            }
            else
            {
                consecutiveUppercase = 0;
            }
        }

        if (maxConsecutiveUppercase >= 5)
        {
            score += 1;
        }

        // Check for excessive special characters (3+ in a row)
        if (Regex.IsMatch(message, @"[♪♥♦◆→←]{3,}"))
        {
            score += 1;
        }

        return score >= scoreThreshold;
    }
}
