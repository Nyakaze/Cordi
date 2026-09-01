using System;
using System.Collections.Generic;
using System.Text;

namespace Cordi.Services.Activity;

public readonly record struct FormatSegment(string Text, string Placeholder)
{
    public bool IsPlaceholder => Placeholder.Length > 0;
}

public sealed class ActivityFormat
{
    public static readonly ActivityFormat Empty = new(Array.Empty<FormatSegment>(), 0);

    private ActivityFormat(IReadOnlyList<FormatSegment> segments, int placeholderCount)
    {
        Segments = segments;
        PlaceholderCount = placeholderCount;
    }

    public IReadOnlyList<FormatSegment> Segments { get; }

    public int PlaceholderCount { get; }

    public static ActivityFormat Parse(string? format)
    {
        if (string.IsNullOrEmpty(format)) return Empty;

        var segments = new List<FormatSegment>();
        var literal = new StringBuilder();
        int placeholders = 0;

        for (int index = 0; index < format.Length; index++)
        {
            if (format[index] != '{')
            {
                literal.Append(format[index]);
                continue;
            }

            int close = format.IndexOf('}', index + 1);

            if (close < 0)
            {
                literal.Append(format[index]);
                continue;
            }

            var token = format[index..(close + 1)];

            if (!ActivityPlaceholders.IsKnown(token))
            {
                literal.Append(token);
                index = close;
                continue;
            }

            if (literal.Length > 0)
            {
                segments.Add(new FormatSegment(literal.ToString(), string.Empty));
                literal.Clear();
            }

            segments.Add(new FormatSegment(string.Empty, ActivityPlaceholders.Normalize(token)));
            placeholders++;
            index = close;
        }

        if (literal.Length > 0)
            segments.Add(new FormatSegment(literal.ToString(), string.Empty));

        return new ActivityFormat(segments, placeholders);
    }
}
