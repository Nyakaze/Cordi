using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Cordi.Services.Chatbox;
using Crovus.Models;

namespace Cordi.Services.Emojis;

public readonly record struct CustomEmote(ulong Id, string Name, bool Animated)
{
    public string Token => Animated ? $"<a:{Name}:{Id}>" : $"<:{Name}:{Id}>";

    public string Url => EmojiTranslator.EmoteUrl(Id, Animated);
}

public sealed class EmojiTranslator
{
    public const int EmoteSize = 128;

    private static readonly Regex MentionRegex = new(
        @"<a?:[A-Za-z0-9_~]{2,32}:\d{5,25}>",
        RegexOptions.Compiled);

    private static readonly Regex MentionOrShortcodeRegex = new(
        @"(?<mention><a?:[A-Za-z0-9_~]{2,32}:\d{5,25}>)|:(?<code>[A-Za-z0-9_+-]{2,32}):",
        RegexOptions.Compiled);

    private static readonly Regex MarkdownEmoteRegex = new(
        @"\[(?<name>[^\]]+)\]\(https?://(?:cdn|media)\.discord(?:app)?\.(?:com|net)/emojis/(?<id>\d{5,25})\.(?<ext>[A-Za-z0-9]+)(?:\?[^)]*)?\)",
        RegexOptions.Compiled);

    private static readonly Regex BareEmoteUrlRegex = new(
        @"https?://(?:cdn|media)\.discord(?:app)?\.(?:com|net)/emojis/(?<id>\d{5,25})\.(?<ext>png|gif|webp|jpe?g|avif)(?:\?(?<query>\S*))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UrlNameRegex = new(
        @"(?:^|&)name=(?<name>[A-Za-z0-9_~%\-]{1,64})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AnimatedFlagRegex = new(
        @"(?:^|&)animated=true",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NumericSuffixRegex = new(
        @"[^A-Za-z0-9]\d+$",
        RegexOptions.Compiled);

    private static readonly Regex WordRegex = new(
        @"[A-Za-z0-9_]+",
        RegexOptions.Compiled);

    private readonly Func<ChatboxEmoteLibrary?> _library;
    private readonly Func<bool> _useEmoticons;

    public EmojiTranslator(GuildEmoteCache guilds, Func<ChatboxEmoteLibrary?> library, Func<bool> useEmoticons)
    {
        Guilds = guilds;
        _library = library;
        _useEmoticons = useEmoticons;
    }

    public GuildEmoteCache Guilds { get; }

    public static string EmoteUrl(ulong id, bool animated) =>
        EmojiParser.ToUrl(new Snowflake(id), animated, EmoteSize);

    public static string LabeledEmoteUrl(ulong id, bool animated, string? name) =>
        WithName(EmoteUrl(id, animated), name);

    private static string WithName(string url, string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name == "emote") return url;

        var separator = url.Contains('?') ? '&' : '?';

        return $"{url}{separator}name={Uri.EscapeDataString(name)}";
    }

    public string ToGame(string? content, bool asUrls)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;

        var text = MarkdownEmoteRegex.Replace(content, match =>
        {
            if (!ulong.TryParse(match.Groups["id"].Value, out var id)) return match.Value;

            var animated = IsAnimated(match.Groups["ext"].Value);
            var name = CleanName(match.Groups["name"].Value);

            return asUrls
                ? $" {LabeledEmoteUrl(id, animated, name)} "
                : $":{name}:";
        });

        text = MentionRegex.Replace(text, match =>
        {
            if (!EmojiParser.TryParseMention(match.Value, out var emoji) || emoji.Id is not { } id)
                return match.Value;

            return asUrls
                ? $" {LabeledEmoteUrl(id.Value, emoji.Animated, emoji.Name)} "
                : $":{emoji.Name}:";
        });

        return EncodeForGame(text).Trim();
    }

    public string ToDiscord(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        return MentionOrShortcodeRegex.Replace(text, match =>
        {
            if (match.Groups["mention"].Success)
            {
                if (!EmojiParser.TryParseMention(match.Value, out var emoji) || emoji.Id is not { } id)
                    return match.Value;

                return Guilds.Contains(id.Value)
                    ? match.Value
                    : $" {LabeledEmoteUrl(id.Value, emoji.Animated, emoji.Name)} ";
            }

            var name = match.Groups["code"].Value;

            if (Guilds.TryByName(name, out var guildEmote)) return guildEmote.Token;

            var seen = _library()?.FindByName(name);
            if (seen != null) return $" {WithName(seen.ImageUrl, seen.Name)} ";

            return EmojiIndex.TryGetShortcode(name, out var unicode) ? unicode : match.Value;
        });
    }

    public string EncodeForGame(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var builder = new StringBuilder(text.Length);

        for (var i = 0; i < text.Length;)
        {
            if (!EmojiIndex.IsEmojiStart(text, i))
            {
                builder.Append(text[i]);
                i++;
                continue;
            }

            var length = EmojiIndex.MeasureCluster(text, i);
            var cluster = text.Substring(i, length);

            builder.Append(ShortcodeFor(cluster) ?? cluster);
            i += length;
        }

        return builder.ToString();
    }

    public IReadOnlyList<CustomEmote> Extract(string? content)
    {
        var found = new List<CustomEmote>();
        if (string.IsNullOrEmpty(content)) return found;

        foreach (Match match in MentionRegex.Matches(content))
        {
            if (EmojiParser.TryParseMention(match.Value, out var emoji) && emoji.Id is { } id)
                Add(found, id.Value, emoji.Name, emoji.Animated);
        }

        foreach (Match match in MarkdownEmoteRegex.Matches(content))
        {
            if (ulong.TryParse(match.Groups["id"].Value, out var id))
                Add(found, id, CleanName(match.Groups["name"].Value), IsAnimated(match.Groups["ext"].Value));
        }

        foreach (Match match in BareEmoteUrlRegex.Matches(content))
        {
            if (!ulong.TryParse(match.Groups["id"].Value, out var id)) continue;

            var query = match.Groups["query"];
            var animated = IsAnimated(match.Groups["ext"].Value)
                           || (query.Success && AnimatedFlagRegex.IsMatch(query.Value));

            Add(found, id, NameFromQuery(query), animated);
        }

        return found;
    }

    public void Register(string? content)
    {
        var library = _library();
        if (library == null) return;

        foreach (var emote in Extract(content))
            library.Record(emote.Id, emote.Name, emote.Animated, emote.Url);
    }

    public string? ResolveUrlByName(string? name)
    {
        if (Guilds.TryByName(name, out var emote)) return emote.Url;

        return _library()?.FindByName(name)?.ImageUrl;
    }

    public string? ResolveName(ulong id)
    {
        if (Guilds.TryById(id, out var emote)) return emote.Name;

        var seen = _library()?.FindById(id);

        return seen != null && seen.Name != "emote" ? seen.Name : null;
    }

    private string? ShortcodeFor(string cluster)
    {
        if (_useEmoticons() && EmojiIndex.TryGetEmoticon(cluster, out var emoticon)) return emoticon;

        var indexed = EmojiIndex.TryGetShortcodeName(cluster, out var name) ? name : null;
        var catalog = EmojiCatalog.Find(cluster)?.Shortcode;

        if (indexed != null && !IsCustomName(indexed)) return $":{indexed}:";
        if (!string.IsNullOrEmpty(catalog) && !IsCustomName(catalog!)) return $":{catalog}:";

        var fallback = catalog ?? indexed;

        return string.IsNullOrEmpty(fallback) ? null : $":{fallback}:";
    }

    private bool IsCustomName(string name) =>
        Guilds.TryByName(name, out _) || _library()?.FindByName(name) != null;

    private static bool IsAnimated(string extension) =>
        string.Equals(extension, "gif", StringComparison.OrdinalIgnoreCase);

    private static void Add(List<CustomEmote> found, ulong id, string name, bool animated)
    {
        if (string.IsNullOrEmpty(name)) return;

        foreach (var existing in found)
            if (existing.Id == id) return;

        found.Add(new CustomEmote(id, name, animated));
    }

    private static string NameFromQuery(Group query)
    {
        if (!query.Success) return string.Empty;

        var match = UrlNameRegex.Match(query.Value);
        if (!match.Success) return string.Empty;

        var decoded = System.Net.WebUtility.UrlDecode(match.Groups["name"].Value);

        return string.IsNullOrWhiteSpace(decoded) ? string.Empty : CleanName(decoded);
    }

    private static string CleanName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        name = NumericSuffixRegex.Replace(System.Net.WebUtility.UrlDecode(name), string.Empty);

        var longest = string.Empty;
        foreach (Match match in WordRegex.Matches(name))
            if (match.Value.Length > longest.Length) longest = match.Value;

        return longest;
    }
}
