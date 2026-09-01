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
        log?.Debug(LogSource, activity is null
            ? "Rendering fallback custom activity."
            : $"Rendering activity '{activity.Name}'.");

        return Render(ActivityPlaceholders.From(activity), config, cycleIndex, replacements, log);
    }

    public static string Render(ActivityPlaceholders placeholders, ActivityTypeConfig config, int cycleIndex,
        IReadOnlyDictionary<string, string>? replacements, CordiLogService? log)
    {
        var format = SelectFormat(config, cycleIndex, log);

        if (string.IsNullOrEmpty(format))
        {
            log?.Debug(LogSource, "Format string is empty — returning empty title.");
            return "";
        }

        var result = ActivityTitleBudget.Build(
            ActivityFormat.Parse(format),
            placeholders.Sanitized(),
            config,
            replacements,
            DiscordActivityConfig.MaxTitleLength,
            log);

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

}
