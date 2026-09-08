using System;
using System.Text.RegularExpressions;
using Cordi.Configuration;

namespace Cordi.Services.Activity;

public static class ActivityBlacklist
{
    private const string LogSource = "Activity";

    public static bool IsFilteredOut(ActivityPlaceholders values, ActivityTypeConfig config, CordiLogService? log)
    {
        if (config.Filters is not { Count: > 0 }) return false;

        foreach (var filter in config.Filters)
        {
            if (filter is null || string.IsNullOrEmpty(filter.Value)) continue;

            var fieldValue = values.Resolve(filter.TargetPlaceholder);

            if (!Matches(filter, fieldValue, log)) continue;

            log?.Debug(LogSource, $"Filter matched: {filter.TargetPlaceholder} {filter.Mode} '{filter.Value}' against '{fieldValue}'");

            return true;
        }

        return false;
    }

    private static bool Matches(FilterRule filter, string fieldValue, CordiLogService? log)
    {
        try
        {
            return filter.Mode switch
            {
                FilterMode.Contains => fieldValue.Contains(filter.Value, StringComparison.OrdinalIgnoreCase),
                FilterMode.Equals => fieldValue.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
                FilterMode.StartsWith => fieldValue.StartsWith(filter.Value, StringComparison.OrdinalIgnoreCase),
                FilterMode.EndsWith => fieldValue.EndsWith(filter.Value, StringComparison.OrdinalIgnoreCase),
                FilterMode.Regex => Regex.IsMatch(fieldValue, filter.Value, RegexOptions.IgnoreCase),
                _ => false,
            };
        }
        catch (ArgumentException ex)
        {
            log?.Warning(LogSource, $"Filter '{filter.Value}' ({filter.Mode}) is invalid and was ignored: {ex.Message}");

            return false;
        }
    }
}
