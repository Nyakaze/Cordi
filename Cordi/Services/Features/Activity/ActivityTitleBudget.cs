using System;
using System.Collections.Generic;
using System.Text;
using Cordi.Configuration;

namespace Cordi.Services.Activity;

public static class ActivityTitleBudget
{
    private const string LogSource = "Activity";
    private const int RecoveryPasses = 4;

    public static string Build(
        ActivityFormat format,
        ActivityPlaceholders values,
        ActivityTypeConfig config,
        IReadOnlyDictionary<string, string>? replacements,
        int maxLength,
        CordiLogService? log)
    {
        var segments = format.Segments;

        if (segments.Count == 0) return "";

        var parts = new string[segments.Count];
        var slots = new List<int>(format.PlaceholderCount);
        int staticCost = 0;

        for (int index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];

            if (!segment.IsPlaceholder)
            {
                parts[index] = ActivityText.ApplyReplacements(segment.Text, replacements);
                staticCost += parts[index].Length;
                continue;
            }

            parts[index] = ActivityText.ApplyReplacements(values.Resolve(segment.Placeholder), replacements);
            slots.Add(index);
        }

        if (slots.Count == 0)
            return ActivityText.CollapseWhitespace(Concat(parts));

        var caps = CapsFor(config);
        var full = new string[slots.Count];
        var natural = new int[slots.Count];
        var cap = new int[slots.Count];

        for (int slot = 0; slot < slots.Count; slot++)
        {
            full[slot] = parts[slots[slot]];
            natural[slot] = full[slot].Length;
            cap[slot] = caps.TryGetValue(segments[slots[slot]].Placeholder, out var limit) ? limit : 0;
        }

        if (!config.DynamicTrim)
        {
            for (int slot = 0; slot < slots.Count; slot++)
            {
                if (cap[slot] > 0)
                    parts[slots[slot]] = ActivityText.Truncate(full[slot], cap[slot]);
            }

            return ActivityText.CollapseWhitespace(Concat(parts));
        }

        var granted = new int[slots.Count];
        int effectiveMax = maxLength;
        string result = "";

        for (int pass = 0; pass < RecoveryPasses; pass++)
        {
            Allocate(natural, cap, effectiveMax - staticCost, granted);

            for (int slot = 0; slot < slots.Count; slot++)
                parts[slots[slot]] = ActivityText.Truncate(full[slot], granted[slot]);

            result = ActivityText.CollapseWhitespace(Concat(parts));

            int recovered = maxLength - result.Length;

            if (recovered <= 0 || !IsTrimmed(granted, natural)) break;

            effectiveMax += recovered;
        }

        log?.Debug(LogSource,
            $"Budget: static={staticCost}, slots={slots.Count}, length={result.Length}/{maxLength}");

        return result;
    }

    private static void Allocate(int[] natural, int[] cap, int budget, int[] granted)
    {
        if (budget <= 0)
        {
            Array.Clear(granted);
            return;
        }

        int total = 0;

        for (int slot = 0; slot < natural.Length; slot++)
        {
            granted[slot] = cap[slot] > 0 ? Math.Min(natural[slot], cap[slot]) : natural[slot];
            total += granted[slot];
        }

        while (total < budget)
        {
            int handed = 0;

            for (int slot = 0; slot < natural.Length && total < budget; slot++)
            {
                if (granted[slot] >= natural[slot]) continue;

                granted[slot]++;
                total++;
                handed++;
            }

            if (handed == 0) break;
        }

        while (total > budget)
        {
            int longest = -1;

            for (int slot = 0; slot < natural.Length; slot++)
            {
                if (granted[slot] > 0 && (longest < 0 || granted[slot] > granted[longest]))
                    longest = slot;
            }

            if (longest < 0) break;

            granted[longest]--;
            total--;
        }
    }

    private static bool IsTrimmed(int[] granted, int[] natural)
    {
        for (int slot = 0; slot < granted.Length; slot++)
        {
            if (granted[slot] < natural[slot]) return true;
        }

        return false;
    }

    private static Dictionary<string, int> CapsFor(ActivityTypeConfig config)
    {
        var caps = new Dictionary<string, int>(StringComparer.Ordinal);

        if (config.TrackLimit > 0)
            caps[ActivityPlaceholders.Normalize("{track}")] = config.TrackLimit;

        if (config.ArtistLimit > 0)
            caps[ActivityPlaceholders.Normalize("{artist}")] = config.ArtistLimit;

        if (config.CharLimits is not { Count: > 0 }) return caps;

        foreach (var rule in config.CharLimits)
        {
            if (rule is null || rule.Limit <= 0) continue;

            var key = ActivityPlaceholders.Normalize(rule.TargetPlaceholder);

            if (key.Length > 0)
                caps[key] = rule.Limit;
        }

        return caps;
    }

    private static string Concat(string[] parts)
    {
        var builder = new StringBuilder();

        foreach (var part in parts)
            builder.Append(part);

        return builder.ToString();
    }
}
