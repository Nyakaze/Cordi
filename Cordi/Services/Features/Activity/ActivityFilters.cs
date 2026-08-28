using System;
using System.Text.RegularExpressions;
using Cordi.Configuration;

namespace Cordi.Services.Activity;

public static class ActivityFilters
{
    public static bool IsFilteredOut(ActivityPlaceholders values, ActivityTypeConfig config, ActivityTrace trace)
    {
        if (config.Filters is not { Count: > 0 }) return false;

        foreach (var filter in config.Filters)
        {
            if (filter is null || string.IsNullOrEmpty(filter.Value)) continue;

            var fieldValue = values.Resolve(filter.TargetPlaceholder);

            if (!Matches(filter, fieldValue, trace)) continue;

            trace.Debug($"Filter matched: {filter.TargetPlaceholder} {filter.Mode} '{filter.Value}' against '{fieldValue}'");

            return true;
        }

        return false;
    }

    private static bool Matches(FilterRule filter, string fieldValue, ActivityTrace trace)
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
            trace.Warning($"Filter '{filter.Value}' ({filter.Mode}) is invalid and was ignored: {ex.Message}");

            return false;
        }
    }
}
