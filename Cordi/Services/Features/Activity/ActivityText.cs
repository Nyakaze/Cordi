using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Cordi.Services.Activity;

public static class ActivityText
{
    private static readonly Regex UrlLikeDotRegex = new(@"(?<=\w)\.(?=\w)", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public static string SanitizeUrlLikeDots(string value) =>
        string.IsNullOrEmpty(value) ? value : UrlLikeDotRegex.Replace(value, "·");

    public static string CollapseWhitespace(string value) =>
        string.IsNullOrEmpty(value) ? "" : WhitespaceRegex.Replace(value, " ").Trim();

    public static string ApplyReplacements(string value, IReadOnlyDictionary<string, string>? replacements)
    {
        if (string.IsNullOrEmpty(value) || replacements is not { Count: > 0 }) return value;

        foreach (var (key, replacement) in replacements)
        {
            if (string.IsNullOrEmpty(key)) continue;

            value = value.Replace(key, replacement);
        }

        return value;
    }

    public static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max) return value;
        if (max <= 0) return "";

        var length = max;

        if (char.IsHighSurrogate(value[length - 1])) length--;

        return value[..length];
    }
}
