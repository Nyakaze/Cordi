using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Cordi.Services.Chatbox;

public sealed class EmojiCatalogEntry
{
    public string Glyph { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Shortcode { get; init; } = string.Empty;
}

public sealed class EmojiCatalogGroup
{
    public string Name { get; init; } = string.Empty;
    public List<EmojiCatalogEntry> Entries { get; } = new();
}

public static class EmojiCatalog
{
    private const string ResourceName = "Cordi.Resources.chatbox-emoji.txt";

    private static IReadOnlyList<EmojiCatalogGroup>? _groups;
    private static Dictionary<string, EmojiCatalogEntry>? _byGlyph;
    private static Dictionary<string, EmojiCatalogEntry>? _byShortcode;

    public static IReadOnlyList<EmojiCatalogGroup> Groups
    {
        get
        {
            Ensure();
            return _groups!;
        }
    }

    public static EmojiCatalogEntry? Find(string? glyph)
    {
        if (string.IsNullOrEmpty(glyph)) return null;

        Ensure();
        return _byGlyph!.TryGetValue(glyph, out var entry) ? entry : null;
    }

    public static EmojiCatalogEntry? FindByShortcode(string? shortcode)
    {
        if (string.IsNullOrEmpty(shortcode)) return null;

        Ensure();
        return _byShortcode!.TryGetValue(shortcode, out var entry) ? entry : null;
    }

    public static void Search(string query, List<EmojiCatalogEntry> results, int limit)
    {
        results.Clear();
        if (limit <= 0) return;

        Ensure();
        var needle = query.Trim();
        if (needle.Length == 0) return;

        foreach (var group in _groups!)
        {
            foreach (var entry in group.Entries)
            {
                if (entry.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0 &&
                    entry.Shortcode.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                results.Add(entry);
                if (results.Count >= limit) return;
            }
        }
    }

    public static string ImageUrl(string baseUrl, string glyph) =>
        $"{baseUrl.TrimEnd('/')}/{EmojiIndex.ToCodePointName(glyph)}.png";

    private static void Ensure()
    {
        if (_groups != null) return;

        var groups = new List<EmojiCatalogGroup>();
        var byGlyph = new Dictionary<string, EmojiCatalogEntry>(StringComparer.Ordinal);
        var byShortcode = new Dictionary<string, EmojiCatalogEntry>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                EmojiCatalogGroup? current = null;

                while (reader.ReadLine() is { } line)
                {
                    if (line.Length == 0) continue;

                    if (line.StartsWith("::", StringComparison.Ordinal))
                    {
                        current = new EmojiCatalogGroup { Name = line[2..] };
                        groups.Add(current);
                        continue;
                    }

                    var split = line.IndexOf('\t');
                    if (split <= 0 || current == null) continue;

                    var glyph = line[..split];
                    var name = line[(split + 1)..];

                    var entry = new EmojiCatalogEntry
                    {
                        Glyph = glyph,
                        Name = name,
                        Shortcode = ToShortcode(name),
                    };

                    current.Entries.Add(entry);
                    byGlyph.TryAdd(glyph, entry);
                    if (entry.Shortcode.Length > 0) byShortcode.TryAdd(entry.Shortcode, entry);
                }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Warning($"[Chatbox] Emoji catalog could not be loaded: {ex.Message}");
        }

        _groups = groups;
        _byGlyph = byGlyph;
        _byShortcode = byShortcode;
    }

    private static string ToShortcode(string name)
    {
        Span<char> buffer = stackalloc char[name.Length];
        var length = 0;

        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c)) buffer[length++] = char.ToLowerInvariant(c);
            else if (length > 0 && buffer[length - 1] != '_') buffer[length++] = '_';
        }

        while (length > 0 && buffer[length - 1] == '_') length--;

        return new string(buffer[..length]);
    }
}
