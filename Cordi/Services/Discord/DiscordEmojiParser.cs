using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Cordi.Services.Discord;

public readonly record struct DiscordCustomEmote(ulong Id, string Name, bool Animated);

public static class DiscordEmojiParser
{
    private static readonly Regex CustomEmojiRegex = new(
        @"<(?<a>a?):(?<name>[A-Za-z0-9_~]{2,32}):(?<id>\d{5,25})>",
        RegexOptions.Compiled);

    private static readonly Regex EmoteLinkRegex = new(
        @"\[(?<name>[^\]]+)\]\(https?://(?:cdn|media)\.discord(?:app)?\.(?:com|net)/emojis/(?<id>\d{5,25})\.(?<ext>[A-Za-z0-9]+)(?:\?[^)]*)?\)",
        RegexOptions.Compiled);

    private static readonly Regex BareEmoteUrlRegex = new(
        @"https?://(?:cdn|media)\.discord(?:app)?\.(?:com|net)/emojis/(?<id>\d{5,25})\.(?<ext>png|gif|webp|jpe?g)(?:\?(?<query>\S*))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UrlNameRegex = new(
        @"(?:^|&)name=(?<name>[A-Za-z0-9_~%\-]{1,64})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AnimatedFlagRegex = new(
        @"(?:^|&)animated=true",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EmoteSuffixRegex = new(
        @"[^A-Za-z0-9]\d+$",
        RegexOptions.Compiled);

    private static readonly Regex EmoteWordRegex = new(
        @"[A-Za-z0-9_]+",
        RegexOptions.Compiled);

    public static string Parse(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return content ?? string.Empty;

        var result = CustomEmojiRegex.Replace(content, m => $":{m.Groups["name"].Value}:");

        return EmoteLinkRegex.Replace(result, m => $":{CleanEmoteName(m.Groups["name"].Value)}:");
    }

    public static string ParseToUrls(string? content)
    {
        if (string.IsNullOrEmpty(content)) return content ?? string.Empty;

        var result = CustomEmojiRegex.Replace(content, m =>
            ulong.TryParse(m.Groups["id"].Value, out var id)
                ? $" {EmoteUrl(id, m.Groups["a"].Value.Length > 0)} "
                : m.Value);

        result = EmoteLinkRegex.Replace(result, m =>
            ulong.TryParse(m.Groups["id"].Value, out var id)
                ? $" {EmoteUrl(id, IsAnimated(m.Groups["ext"].Value))} "
                : m.Value);

        return result.Trim();
    }

    public static List<DiscordCustomEmote> Extract(string? content)
    {
        var found = new List<DiscordCustomEmote>();
        if (string.IsNullOrEmpty(content)) return found;

        foreach (Match match in CustomEmojiRegex.Matches(content))
            Add(found, match.Groups["id"].Value, match.Groups["name"].Value, match.Groups["a"].Value.Length > 0);

        foreach (Match match in EmoteLinkRegex.Matches(content))
            Add(found, match.Groups["id"].Value, CleanEmoteName(match.Groups["name"].Value), IsAnimated(match.Groups["ext"].Value));

        foreach (Match match in BareEmoteUrlRegex.Matches(content))
        {
            var query = match.Groups["query"];
            var animated = IsAnimated(match.Groups["ext"].Value)
                           || (query.Success && AnimatedFlagRegex.IsMatch(query.Value));

            Add(found, match.Groups["id"].Value, NameFromQuery(query), animated);
        }

        return found;
    }

    public static string EmoteUrl(ulong id, bool animated) =>
        animated
            ? $"https://cdn.discordapp.com/emojis/{id}.webp?size=96&animated=true"
            : $"https://cdn.discordapp.com/emojis/{id}.webp?size=96";

    private static string NameFromQuery(Group query)
    {
        if (!query.Success) return string.Empty;

        var match = UrlNameRegex.Match(query.Value);
        if (!match.Success) return string.Empty;

        var decoded = System.Net.WebUtility.UrlDecode(match.Groups["name"].Value);

        return string.IsNullOrWhiteSpace(decoded) ? string.Empty : CleanEmoteName(decoded);
    }

    private static bool IsAnimated(string extension) =>
        string.Equals(extension, "gif", StringComparison.OrdinalIgnoreCase);

    private static void Add(List<DiscordCustomEmote> found, string rawId, string name, bool animated)
    {
        if (!ulong.TryParse(rawId, out var id)) return;
        if (string.IsNullOrEmpty(name)) return;

        foreach (var existing in found)
            if (existing.Id == id) return;

        found.Add(new DiscordCustomEmote(id, name, animated));
    }

    private static string CleanEmoteName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        name = System.Net.WebUtility.UrlDecode(name);

        name = EmoteSuffixRegex.Replace(name, "");

        var matches = EmoteWordRegex.Matches(name);
        string longest = "";
        foreach (Match match in matches)
        {
            if (match.Value.Length > longest.Length)
            {
                longest = match.Value;
            }
        }

        return longest;
    }
}
