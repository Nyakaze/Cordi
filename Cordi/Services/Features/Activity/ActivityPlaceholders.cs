using System;
using System.Collections.Generic;
using Crovus.Models;

namespace Cordi.Services.Activity;

public sealed record ActivityPlaceholders
{
    public static readonly ActivityPlaceholders Empty = new();

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        "{name}",
        "{details}",
        "{state}",
        "{album}",
        "{large_image}",
        "{small_image}",
        "{elapsed}",
        "{duration}",
        "{time_start}",
        "{time_end}",
    };

    public string Name { get; init; } = "";

    public string Details { get; init; } = "";

    public string State { get; init; } = "";

    public string Album { get; init; } = "";

    public string LargeImage { get; init; } = "";

    public string SmallImage { get; init; } = "";

    public string Elapsed { get; init; } = "";

    public string Duration { get; init; } = "";

    public string TimeStart { get; init; } = "";

    public string TimeEnd { get; init; } = "";

    public static ActivityPlaceholders From(DiscordActivity? activity)
    {
        if (activity is null) return Empty;

        var start = activity.Timestamps?.Start;
        var end = activity.Timestamps?.End;

        var elapsed = "";
        var timeStart = "";
        var duration = "";
        var timeEnd = "";

        if (start is { } startedAt)
        {
            elapsed = Clock(DateTimeOffset.UtcNow - startedAt);
            timeStart = startedAt.ToLocalTime().ToString("HH:mm");
        }

        if (end is { } endsAt && endsAt > DateTimeOffset.UtcNow)
        {
            if (start is { } from) duration = Clock(endsAt - from);

            timeEnd = endsAt.ToLocalTime().ToString("HH:mm");
        }

        return new ActivityPlaceholders
        {
            Name = activity.Name ?? "",
            Details = activity.Details ?? "",
            State = activity.State ?? "",
            Album = activity.Assets?.LargeText ?? "",
            LargeImage = activity.Assets?.LargeText ?? "",
            SmallImage = activity.Assets?.SmallText ?? "",
            Elapsed = elapsed,
            Duration = duration,
            TimeStart = timeStart,
            TimeEnd = timeEnd,
        };
    }

    public string Resolve(string placeholder) => Normalize(placeholder) switch
    {
        "{name}" => Name,
        "{details}" => Details,
        "{state}" => State,
        "{album}" => Album,
        "{large_image}" => LargeImage,
        "{small_image}" => SmallImage,
        "{elapsed}" => Elapsed,
        "{duration}" => Duration,
        "{time_start}" => TimeStart,
        "{time_end}" => TimeEnd,
        _ => "",
    };

    public static bool IsKnown(string? placeholder) => Known.Contains(Normalize(placeholder));

    public ActivityPlaceholders Sanitized() => this with
    {
        Name = ActivityText.SanitizeUrlLikeDots(Name),
        Details = ActivityText.SanitizeUrlLikeDots(Details),
        State = ActivityText.SanitizeUrlLikeDots(State),
        Album = ActivityText.SanitizeUrlLikeDots(Album),
        LargeImage = ActivityText.SanitizeUrlLikeDots(LargeImage),
        SmallImage = ActivityText.SanitizeUrlLikeDots(SmallImage),
    };

    public static string Normalize(string? placeholder) => placeholder switch
    {
        "{track}" => "{details}",
        "{artist}" => "{state}",
        null => "",
        _ => placeholder,
    };

    private static string Clock(TimeSpan span) => $"{(int)span.TotalMinutes:D2}:{span.Seconds:D2}";
}
