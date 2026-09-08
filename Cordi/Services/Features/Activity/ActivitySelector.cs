using System.Collections.Generic;
using System.Linq;
using Cordi.Configuration;
using Crovus.Models;

namespace Cordi.Services.Activity;

public readonly record struct ActivityCandidate(DiscordActivity? Activity, ActivityTypeConfig Config, int Priority)
{
    public ActivityType Type => Activity?.Type ?? ActivityType.Custom;

    public string Label => Activity?.Name ?? "(custom)";
}

public static class ActivitySelector
{
    private const string LogSource = "Activity";

    public static ActivityCandidate? SelectBest(DiscordPresence? presence, DiscordActivityConfig config,
        CordiLogService? log)
    {
        var candidates = new List<ActivityCandidate>();
        var enabled = config.EnabledTypes();

        if (presence is null)
            log?.Debug(LogSource, "Cached presence is null — no presence data received yet.");
        else if (presence.Activities is not { Count: > 0 })
            log?.Debug(LogSource, "Presence has no activities.");
        else
        {
            if (log is not null)
                foreach (var skipped in presence.Activities.Where(activity => !enabled.Includes(activity)))
                    log.Debug(LogSource,
                        $"Activity '{skipped.Name}' ({skipped.Type}) has no enabled type config — skipped.");

            candidates.AddRange(presence.Activities
                .WithTypes(enabled)
                .Select(activity => Match(activity, config, log))
                .Where(candidate => candidate is not null)
                .Select(candidate => candidate!.Value));
        }

        if (enabled.Includes(ActivityType.Custom)
            && config.TypeConfigs.TryGetValue(ActivityType.Custom, out var customConfig)
            && candidates.All(candidate => candidate.Type != ActivityType.Custom))
        {
            log?.Debug(LogSource, "Adding fallback Custom activity candidate.");
            candidates.Add(new ActivityCandidate(null, customConfig, customConfig.Priority));
        }

        log?.Debug(LogSource, $"Total candidates: {candidates.Count}");

        if (candidates.Count == 0) return null;

        var best = candidates.OrderByDescending(candidate => candidate.Priority).First();

        log?.Debug(LogSource, $"Best candidate: Type={best.Type}, Name='{best.Label}', Priority={best.Priority}");

        return best;
    }

    private static ActivityCandidate? Match(DiscordActivity activity, DiscordActivityConfig config,
        CordiLogService? log)
    {
        if (config.TypeConfigs.GetValueOrDefault(activity.Type) is not { } typeConfig)
        {
            log?.Debug(LogSource, $"Activity '{activity.Name}' ({activity.Type}) has no matching type config — skipped.");
            return null;
        }

        var selected = typeConfig;

        if (activity.Type == ActivityType.Playing
            && !string.IsNullOrEmpty(activity.Name)
            && config.GameConfigs.TryGetValue(activity.Name, out var gameConfig))
        {
            log?.Debug(LogSource, $"Activity '{activity.Name}' ({activity.Type}) matched game override config.");

            selected = gameConfig;
        }

        if (!selected.Enabled)
        {
            log?.Debug(LogSource, $"Activity '{activity.Name}' ({activity.Type}) has config but it is disabled — skipped.");
            return null;
        }

        var values = ActivityPlaceholders.From(activity);

        if (ActivityBlacklist.IsFilteredOut(values, selected, log))
        {
            log?.Debug(LogSource, $"Activity '{activity.Name}' ({activity.Type}) matched a blacklist filter — skipped.");
            return null;
        }

        if (activity.Type == ActivityType.Playing
            && !ReferenceEquals(typeConfig, selected)
            && ActivityBlacklist.IsFilteredOut(values, typeConfig, log))
        {
            log?.Debug(LogSource, $"Activity '{activity.Name}' ({activity.Type}) matched a main Playing blacklist filter — skipped.");
            return null;
        }

        log?.Debug(LogSource, $"Activity '{activity.Name}' ({activity.Type}) added as candidate (Priority={typeConfig.Priority}).");

        return new ActivityCandidate(activity, selected, typeConfig.Priority);
    }
}
