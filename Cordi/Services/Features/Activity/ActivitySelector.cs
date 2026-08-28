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
    public static ActivityCandidate? SelectBest(DiscordPresence? presence, DiscordActivityConfig config,
        ActivityTrace trace)
    {
        var candidates = new List<ActivityCandidate>();

        if (presence is null)
            trace.Debug("Cached presence is null — no presence data received yet.");
        else if (presence.Activities is not { Count: > 0 })
            trace.Debug("Presence has no activities.");
        else
            candidates.AddRange(presence.Activities
                .Select(activity => Match(activity, config, trace))
                .Where(candidate => candidate is not null)
                .Select(candidate => candidate!.Value));

        if (config.TypeConfigs.TryGetValue(ActivityType.Custom, out var customConfig)
            && customConfig.Enabled
            && candidates.All(candidate => candidate.Type != ActivityType.Custom))
        {
            trace.Debug("Adding fallback Custom activity candidate.");
            candidates.Add(new ActivityCandidate(null, customConfig, customConfig.Priority));
        }

        trace.Debug($"Total candidates: {candidates.Count}");

        if (candidates.Count == 0) return null;

        var best = candidates.OrderByDescending(candidate => candidate.Priority).First();

        trace.Debug($"Best candidate: Type={best.Type}, Name='{best.Label}', Priority={best.Priority}");

        return best;
    }

    private static ActivityCandidate? Match(DiscordActivity activity, DiscordActivityConfig config,
        ActivityTrace trace)
    {
        ActivityTypeConfig? typeConfig;
        int priority;

        if (activity.Type == ActivityType.Playing
            && !string.IsNullOrEmpty(activity.Name)
            && config.GameConfigs.TryGetValue(activity.Name, out var gameConfig))
        {
            var playingConfig = config.TypeConfigs.GetValueOrDefault(ActivityType.Playing);

            if (playingConfig is not { Enabled: true })
            {
                trace.Debug($"Activity '{activity.Name}' ({activity.Type}) matched game override but Playing type is disabled — skipped.");
                return null;
            }

            trace.Debug($"Activity '{activity.Name}' ({activity.Type}) matched game override config.");

            typeConfig = gameConfig;
            priority = playingConfig.Priority;
        }
        else
        {
            typeConfig = config.TypeConfigs.GetValueOrDefault(activity.Type);

            if (typeConfig is null)
            {
                trace.Debug($"Activity '{activity.Name}' ({activity.Type}) has no matching type config — skipped.");
                return null;
            }

            priority = typeConfig.Priority;
        }

        if (!typeConfig.Enabled)
        {
            trace.Debug($"Activity '{activity.Name}' ({activity.Type}) has config but it is disabled — skipped.");
            return null;
        }

        var values = ActivityPlaceholders.From(activity);

        if (ActivityFilters.IsFilteredOut(values, typeConfig, trace))
        {
            trace.Debug($"Activity '{activity.Name}' ({activity.Type}) matched a blacklist filter — skipped.");
            return null;
        }

        if (activity.Type == ActivityType.Playing
            && config.TypeConfigs.TryGetValue(ActivityType.Playing, out var playingTypeConfig)
            && !ReferenceEquals(playingTypeConfig, typeConfig)
            && ActivityFilters.IsFilteredOut(values, playingTypeConfig, trace))
        {
            trace.Debug($"Activity '{activity.Name}' ({activity.Type}) matched a main Playing blacklist filter — skipped.");
            return null;
        }

        trace.Debug($"Activity '{activity.Name}' ({activity.Type}) added as candidate (Priority={priority}).");

        return new ActivityCandidate(activity, typeConfig, priority);
    }
}
