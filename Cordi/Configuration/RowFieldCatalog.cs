using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Cordi.Configuration;

public sealed record RowToken(string Token, string Description, string Sample);

public sealed record RowFieldInfo(
    string Key,
    string Label,
    string Description,
    string DefaultFormat,
    IReadOnlyList<RowToken> Tokens);

public static class RowFieldCatalog
{
    public const string Direction = "direction";
    public const string Distance = "distance";
    public const string Name = "name";
    public const string World = "world";
    public const string Time = "time";
    public const string Target = "target";
    public const string Duration = "duration";
    public const string Verb = "verb";
    public const string Emote = "emote";
    public const string Command = "command";
    public const string Count = "count";
    public const string Reply = "reply";

    private static readonly RowToken[] NoTokens = Array.Empty<RowToken>();

    private static readonly RowToken[] TimeTokens =
    {
        new("HH", "Hour on a 24 hour clock", "14"),
        new("h", "Hour on a 12 hour clock", "2"),
        new("mm", "Minute", "32"),
        new("ss", "Second", "07"),
        new("tt", "AM or PM", "PM"),
        new("date", "Short date", "20/09/2026"),
    };

    private static readonly RowToken[] DurationTokens =
    {
        new("total", "The whole length, written short", "1m 05s"),
        new("h", "Hours", "0"),
        new("m", "Minutes", "1"),
        new("mm", "Minutes with a leading zero", "01"),
        new("s", "Seconds", "5"),
        new("ss", "Seconds with a leading zero", "05"),
    };

    private static readonly RowToken[] NameTokens =
    {
        new("name", "Full name", "Alice Example"),
        new("first", "First name", "Alice"),
        new("last", "Last name", "Example"),
        new("initials", "Initials", "A.E."),
    };

    private static readonly RowToken[] WorldTokens =
    {
        new("world", "Home world", "Omega"),
    };

    private static readonly RowToken[] DistanceTokens =
    {
        new("y", "Yalms with one decimal", "12.3"),
        new("yint", "Yalms rounded", "12"),
    };

    private static readonly RowToken[] TargetTokens =
    {
        new("name", "Full name", "Bob Example"),
        new("first", "First name", "Bob"),
        new("last", "Last name", "Example"),
        new("initials", "Initials", "B.E."),
        new("world", "Home world", "Omega"),
    };

    private static readonly RowToken[] EmoteTokens =
    {
        new("emote", "Name of the emote", "Pat"),
        new("command", "Slash command of the emote", "/pat"),
    };

    private static readonly RowToken[] CommandTokens =
    {
        new("command", "Slash command of the emote", "/pat"),
    };

    private static readonly RowToken[] CountTokens =
    {
        new("count", "How often it repeated", "3"),
    };

    public static readonly IReadOnlyList<RowFieldInfo> PeeperFields = new[]
    {
        new RowFieldInfo(Direction, "Direction", "Arrow pointing at where they are standing", string.Empty, NoTokens),
        new RowFieldInfo(Distance, "Distance", "How far away they are, in yalms", "{y}y", DistanceTokens),
        new RowFieldInfo(Name, "Name", "Character name", "{name}", NameTokens),
        new RowFieldInfo(World, "World", "Home world of the character", "@{world}", WorldTokens),
        new RowFieldInfo(Target, "Their target", "Who that player is looking at", "→ {name}", TargetTokens),
        new RowFieldInfo(Time, "Time", "When they last looked at you", "{HH}:{mm}", TimeTokens),
        new RowFieldInfo(Duration, "Duration", "How long they have been looking", "{m}:{ss}", DurationTokens),
    };

    public static readonly IReadOnlyList<RowFieldInfo> EmoteLogFields = new[]
    {
        new RowFieldInfo(Time, "Time", "When the emote came in", "[{HH}:{mm}]", TimeTokens),
        new RowFieldInfo(Name, "Name", "Character name", "{name}", NameTokens),
        new RowFieldInfo(World, "World", "Home world of the character", "@{world}", WorldTokens),
        new RowFieldInfo(Verb, "Word between", "The little word between the name and the emote", "used", NoTokens),
        new RowFieldInfo(Emote, "Emote", "Name of the emote", "{emote}", EmoteTokens),
        new RowFieldInfo(Command, "Command", "The slash command of the emote", "{command}", CommandTokens),
        new RowFieldInfo(Count, "Repeat count", "How often it was repeated", "[{count}]", CountTokens),
        new RowFieldInfo(Reply, "Emote back button", "Button that sends the emote straight back", string.Empty, NoTokens),
    };

    public static List<RowFieldConfig> PeeperDefaults() => new()
    {
        Field(PeeperFields, Direction, RowSide.Left, RowFieldStyle.Muted),
        Field(PeeperFields, Distance, RowSide.Left, RowFieldStyle.Muted),
        Field(PeeperFields, Name, RowSide.Left, RowFieldStyle.Normal),
        Field(PeeperFields, World, RowSide.Left, RowFieldStyle.Muted, enabled: false),
        Field(PeeperFields, Time, RowSide.Right, RowFieldStyle.Muted),
        Field(PeeperFields, Duration, RowSide.Right, RowFieldStyle.Muted, enabled: false),
        Field(PeeperFields, Target, RowSide.Second, RowFieldStyle.Muted),
    };

    public static List<RowFieldConfig> EmoteLogDefaults() => new()
    {
        Field(EmoteLogFields, Time, RowSide.Left, RowFieldStyle.Normal),
        Field(EmoteLogFields, Name, RowSide.Left, RowFieldStyle.Normal),
        Field(EmoteLogFields, World, RowSide.Left, RowFieldStyle.Muted, enabled: false),
        Field(EmoteLogFields, Verb, RowSide.Left, RowFieldStyle.Normal),
        Field(EmoteLogFields, Emote, RowSide.Left, RowFieldStyle.Normal),
        Field(EmoteLogFields, Command, RowSide.Left, RowFieldStyle.Muted, enabled: false),
        Field(EmoteLogFields, Count, RowSide.Left, RowFieldStyle.Normal),
        Field(EmoteLogFields, Reply, RowSide.Right, RowFieldStyle.Normal),
    };

    public static RowFieldInfo? Info(IReadOnlyList<RowFieldInfo> catalog, string key) =>
        catalog.FirstOrDefault(f => f.Key == key);

    public static string? Format(string template, Func<string, string?> resolve)
    {
        if (string.IsNullOrEmpty(template)) return null;
        if (template.IndexOf('{') < 0) return template;

        var builder = new StringBuilder(template.Length + 16);

        for (int i = 0; i < template.Length; i++)
        {
            if (template[i] != '{')
            {
                builder.Append(template[i]);
                continue;
            }

            int close = template.IndexOf('}', i + 1);
            if (close < 0)
            {
                builder.Append(template[i]);
                continue;
            }

            var value = resolve(template[(i + 1)..close]);
            if (string.IsNullOrEmpty(value)) return null;

            builder.Append(value);
            i = close;
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    public static string? NamePart(string? name, string token)
    {
        if (string.IsNullOrEmpty(name)) return null;

        int space = name.IndexOf(' ');
        bool split = space > 0 && space + 1 < name.Length;

        return token switch
        {
            "name" => name,
            "first" => split ? name[..space] : name,
            "last" => split ? name[(space + 1)..] : null,
            "initials" => split ? $"{name[0]}.{name[space + 1]}." : $"{name[0]}.",
            _ => null,
        };
    }

    public static string? TimePart(DateTime when, string token) => token switch
    {
        "HH" => when.ToString("HH"),
        "h" => when.ToString("%h"),
        "mm" => when.ToString("mm"),
        "ss" => when.ToString("ss"),
        "tt" => when.ToString("tt"),
        "date" => when.ToString("d"),
        _ => null,
    };

    public static string? DurationPart(TimeSpan span, string token)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;

        return token switch
        {
            "total" => span.TotalHours >= 1
                ? $"{(int)span.TotalHours}h {span.Minutes:D2}m"
                : span.TotalMinutes >= 1
                    ? $"{span.Minutes}m {span.Seconds:D2}s"
                    : $"{span.Seconds}s",
            "h" => ((int)span.TotalHours).ToString(),
            "m" => ((int)span.TotalMinutes).ToString(),
            "mm" => ((int)span.TotalMinutes).ToString("D2"),
            "s" => span.Seconds.ToString(),
            "ss" => span.Seconds.ToString("D2"),
            _ => null,
        };
    }

    private static RowFieldConfig Field(
        IReadOnlyList<RowFieldInfo> catalog,
        string key,
        RowSide side,
        RowFieldStyle style,
        bool enabled = true) => new()
    {
        Key = key,
        Enabled = enabled,
        Side = side,
        Style = style,
        Color = new Vector4(1f, 1f, 1f, 1f),
        Format = catalog.First(f => f.Key == key).DefaultFormat,
    };
}
