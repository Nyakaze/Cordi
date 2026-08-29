using System.Collections.Generic;
using Cordi.Configuration;
using Crovus.Models;

namespace Cordi.Services.Activity;

public static class ActivityTitleRenderer
{
    private const string LogSource = "Activity";

    public static string Render(DiscordActivity? activity, ActivityTypeConfig config, int cycleIndex,
        IReadOnlyDictionary<string, string>? replacements, CordiLogService? log)
    {
        var format = SelectFormat(config, cycleIndex, log);

        if (string.IsNullOrEmpty(format))
        {
            log?.Debug(LogSource, "Format string is empty — returning empty title.");
            return "";
        }

        var values = ActivityPlaceholders.From(activity).WithLimits(config).Sanitized();

        log?.Debug(LogSource, activity is null
            ? "Rendering fallback custom activity."
            : $"Rendering {values}");

        var result = ApplyReplacements(Substitute(format, values), replacements);

        result = ActivityText.CollapseWhitespace(result);

        log?.Debug(LogSource, $"Rendered title: \"{result}\"");

        return result;
    }

    private static string SelectFormat(ActivityTypeConfig config, int cycleIndex, CordiLogService? log)
    {
        if (!config.EnableCycling
            || config.CycleFormats is not { } formats
            || cycleIndex < 0
            || cycleIndex >= formats.Count)
            return config.Format;

        log?.Debug(LogSource, $"Using cycle format [{cycleIndex}]: \"{formats[cycleIndex]}\"");

        return formats[cycleIndex];
    }

    private static string Substitute(string format, ActivityPlaceholders values) => format
        .Replace("{name}", values.Name)
        .Replace("{details}", values.Details)
        .Replace("{state}", values.State)
        .Replace("{track}", values.Details)
        .Replace("{artist}", values.State)
        .Replace("{album}", values.Album)
        .Replace("{large_image}", values.LargeImage)
        .Replace("{small_image}", values.SmallImage)
        .Replace("{elapsed}", values.Elapsed)
        .Replace("{duration}", values.Duration)
        .Replace("{time_start}", values.TimeStart)
        .Replace("{time_end}", values.TimeEnd);

    private static string ApplyReplacements(string input, IReadOnlyDictionary<string, string>? replacements)
    {
        if (string.IsNullOrEmpty(input) || replacements is not { Count: > 0 }) return input;

        foreach (var (key, value) in replacements)
        {
            if (string.IsNullOrEmpty(key)) continue;

            input = input.Replace(key, value);
        }

        return input;
    }
}
