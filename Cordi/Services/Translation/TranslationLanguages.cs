using System;
using System.Collections.Generic;

namespace Cordi.Services.Translation;

public sealed record TranslationLanguage(string Iso, string Name, string Google, string DeepL);

public static class TranslationLanguages
{
    public static readonly IReadOnlyList<TranslationLanguage> All = new TranslationLanguage[]
    {
        new("en", "English", "en", "EN-US"),
        new("ja", "Japanese", "ja", "JA"),
        new("de", "German", "de", "DE"),
        new("fr", "French", "fr", "FR"),
        new("zh", "Chinese", "zh-CN", "ZH"),
        new("ko", "Korean", "ko", "KO"),
        new("es", "Spanish", "es", "ES"),
        new("pt", "Portuguese", "pt", "PT-BR"),
        new("it", "Italian", "it", "IT"),
        new("nl", "Dutch", "nl", "NL"),
        new("ru", "Russian", "ru", "RU"),
        new("uk", "Ukrainian", "uk", "UK"),
        new("pl", "Polish", "pl", "PL"),
        new("tr", "Turkish", "tr", "TR"),
        new("sv", "Swedish", "sv", "SV"),
        new("da", "Danish", "da", "DA"),
        new("fi", "Finnish", "fi", "FI"),
        new("nb", "Norwegian", "no", "NB"),
        new("cs", "Czech", "cs", "CS"),
        new("el", "Greek", "el", "EL"),
        new("hu", "Hungarian", "hu", "HU"),
        new("ro", "Romanian", "ro", "RO"),
        new("bg", "Bulgarian", "bg", "BG"),
        new("sk", "Slovak", "sk", "SK"),
        new("sl", "Slovenian", "sl", "SL"),
        new("lv", "Latvian", "lv", "LV"),
        new("lt", "Lithuanian", "lt", "LT"),
        new("et", "Estonian", "et", "ET"),
        new("id", "Indonesian", "id", "ID"),
        new("th", "Thai", "th", "TH"),
        new("vi", "Vietnamese", "vi", "VI"),
        new("ar", "Arabic", "ar", "AR"),
        new("he", "Hebrew", "iw", string.Empty),
        new("hi", "Hindi", "hi", string.Empty),
    };

    private static readonly Dictionary<string, TranslationLanguage> ByIso = Build();

    private static Dictionary<string, TranslationLanguage> Build()
    {
        var map = new Dictionary<string, TranslationLanguage>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in All)
            map[language.Iso] = language;
        return map;
    }

    public static TranslationLanguage? Find(string? iso) =>
        iso != null && ByIso.TryGetValue(iso, out var language) ? language : null;

    public static bool IsSupported(string? iso) => Find(iso) != null;

    public static string NameOf(string? iso) => Find(iso)?.Name ?? iso ?? "Unknown";

    public static string GoogleCode(string iso) => Find(iso)?.Google ?? iso;

    public static string DeepLCode(string iso)
    {
        var language = Find(iso);
        return language == null || language.DeepL.Length == 0 ? string.Empty : language.DeepL;
    }

    public static string Normalize(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)) return string.Empty;

        var trimmed = iso.Trim();
        var cut = trimmed.IndexOf('-');
        if (cut > 0) trimmed = trimmed[..cut];
        trimmed = trimmed.ToLowerInvariant();

        return trimmed switch
        {
            "iw" => "he",
            "in" => "id",
            "no" => "nb",
            "zh_cn" or "cn" => "zh",
            _ => trimmed,
        };
    }
}
