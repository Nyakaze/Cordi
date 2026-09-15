using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cordi.Services.Translation;

public enum PhraseOutcome
{
    None,
    Swallow,
    Identified,
}

public readonly record struct PhraseMatch(PhraseOutcome Outcome, string? Iso, string? Translation)
{
    public static PhraseMatch Miss => new(PhraseOutcome.None, null, null);
}

public static partial class TranslationPhrases
{
    private const string ResourceName = "Cordi.Resources.phrases.json";
    private const int CollapseSpaceLimit = 8;

    private sealed class Entry
    {
        public string Language { get; set; } = string.Empty;
        public Dictionary<string, string>? Translations { get; set; }
    }

    private static readonly Dictionary<string, string> NameToIso = new(StringComparer.OrdinalIgnoreCase)
    {
        ["English"] = "en",
        ["Japanese"] = "ja",
        ["German"] = "de",
        ["French"] = "fr",
        ["Spanish"] = "es",
        ["Korean"] = "ko",
        ["Portuguese"] = "pt",
        ["Russian"] = "ru",
        ["Italian"] = "it",
        ["Dutch"] = "nl",
        ["Chinese"] = "zh",
        ["Chinese (Simplified)"] = "zh",
        ["Chinese (Traditional)"] = "zh",
    };

    private static readonly Dictionary<string, string> IsoToName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "English",
        ["ja"] = "Japanese",
        ["de"] = "German",
        ["fr"] = "French",
        ["es"] = "Spanish",
        ["ko"] = "Korean",
        ["pt"] = "Portuguese",
        ["ru"] = "Russian",
        ["it"] = "Italian",
        ["nl"] = "Dutch",
        ["zh"] = "Chinese (Simplified)",
    };

    private static readonly Dictionary<string, Entry> Table = Load();

    private static readonly HashSet<string> EnglishTokens = BuildEnglishTokens();

    [GeneratedRegex(@"^[\s!！?？.。～~ーｰ\-_,、…\^]+|[\s!！?？.。～~ーｰ\-_,、…\^]+$")]
    private static partial Regex EdgePunctuationRegex();

    [GeneratedRegex(@"(.)\1{2,}")]
    private static partial Regex LongRunRegex();

    [GeneratedRegex(@"(.)\1+")]
    private static partial Regex AnyRunRegex();

    [GeneratedRegex(@"(ha){3,}")]
    private static partial Regex LaughRegex();

    [GeneratedRegex(@"(lo){2,}l")]
    private static partial Regex LolRegex();

    [GeneratedRegex(@"\bxd+\b")]
    private static partial Regex XdRegex();

    [GeneratedRegex(@"[\s,!?.;:""'()\[\]{}]+")]
    private static partial Regex TokenSplitRegex();

    public static int Count => Table.Count;

    public static string Normalize(string text) => Normalize(text, false);

    public static PhraseMatch Resolve(string text, string? targetIso)
    {
        if (text.Length == 0) return PhraseMatch.Miss;

        if (!TryLookup(text, out var entry))
            return PhraseMatch.Miss;

        var iso = entry.Language.Length > 0 && NameToIso.TryGetValue(entry.Language, out var mapped)
            ? mapped
            : null;

        if (entry.Translations is null)
            return iso == null ? new PhraseMatch(PhraseOutcome.Swallow, null, null) : new PhraseMatch(PhraseOutcome.Identified, iso, null);

        string? translation = null;

        if (targetIso != null
            && IsoToName.TryGetValue(targetIso, out var targetName)
            && entry.Translations.TryGetValue(targetName, out var candidate)
            && candidate.Length > 0)
        {
            translation = candidate;
        }

        return new PhraseMatch(PhraseOutcome.Identified, iso, translation);
    }

    public static bool HasEnglishToken(string text)
    {
        foreach (var c in text)
        {
            if (IsCjk(c)) return false;
        }

        foreach (var word in TokenSplitRegex().Split(text.ToLowerInvariant()))
        {
            if (word.Length == 0) continue;
            if (EnglishTokens.Contains(word)) return true;

            var shortened = LongRunRegex().Replace(word, "$1$1");
            if (shortened != word && EnglishTokens.Contains(shortened)) return true;

            var collapsed = AnyRunRegex().Replace(word, "$1");
            if (collapsed != word && EnglishTokens.Contains(collapsed)) return true;
        }

        return false;
    }

    private static bool TryLookup(string text, out Entry entry)
    {
        var normalized = Normalize(text, false);

        if (Table.TryGetValue(normalized, out var hit))
        {
            entry = hit;
            return true;
        }

        var collapsed = Normalize(text, true);

        if (!string.Equals(collapsed, normalized, StringComparison.Ordinal) && Table.TryGetValue(collapsed, out hit))
        {
            entry = hit;
            return true;
        }

        entry = null!;
        return false;
    }

    private static string Normalize(string text, bool aggressive)
    {
        var value = text.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        if (Table.ContainsKey(value)) return value;

        value = EdgePunctuationRegex().Replace(value, string.Empty).Trim();
        value = aggressive
            ? AnyRunRegex().Replace(value, "$1")
            : LongRunRegex().Replace(value, "$1$1");
        value = LaughRegex().Replace(value, "haha");
        value = LolRegex().Replace(value, "lol");
        value = XdRegex().Replace(value, "xd");

        var spaceless = value.Replace(" ", string.Empty);
        if (spaceless.Length <= CollapseSpaceLimit && value.Contains(' ') && !Table.ContainsKey(value))
            value = spaceless;

        return value;
    }

    private static bool IsCjk(char c) =>
        c is >= '⺀' and <= '鿿' or >= '가' and <= '힯' or >= '･' and <= 'ﾟ';

    private static Dictionary<string, Entry> Load()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream == null) return new Dictionary<string, Entry>(StringComparer.Ordinal);

            var parsed = JsonSerializer.Deserialize<Dictionary<string, Entry>>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed == null) return new Dictionary<string, Entry>(StringComparer.Ordinal);

            var table = new Dictionary<string, Entry>(parsed.Count, StringComparer.Ordinal);

            foreach (var (key, value) in parsed)
                table[key] = value;

            return table;
        }
        catch
        {
            return new Dictionary<string, Entry>(StringComparer.Ordinal);
        }
    }

    private static HashSet<string> BuildEnglishTokens()
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, entry) in Table)
        {
            if (string.Equals(entry.Language, "English", StringComparison.OrdinalIgnoreCase)) tokens.Add(key);
        }

        return tokens;
    }
}
