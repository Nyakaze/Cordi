using System;
using System.Collections.Generic;
using System.Linq;
using Cordi.UI.Components;
using Dalamud.Interface;

namespace Cordi.UI.Search;

public sealed class SearchEntry
{
    public required string PageId { get; init; }
    public required string PageLabel { get; init; }
    public required string Label { get; init; }
    public string? SubTab { get; init; }
    public FontAwesomeIcon Icon { get; init; } = FontAwesomeIcon.Cog;
    public string Keywords { get; init; } = string.Empty;
}

public sealed class SettingsSearchIndex
{
    private readonly List<SearchEntry> entries = new();
    private readonly List<SearchEntry> staticEntries = new();

    public void RegisterSetting(string pageId, string pageLabel, string label, string? subTab = null, string keywords = "", FontAwesomeIcon icon = FontAwesomeIcon.SlidersH)
    {
        staticEntries.Add(new SearchEntry
        {
            PageId = pageId,
            PageLabel = string.IsNullOrEmpty(subTab) ? pageLabel : $"{pageLabel}  ›  {subTab}",
            Label = label,
            SubTab = string.IsNullOrEmpty(subTab) ? null : subTab,
            Keywords = keywords,
            Icon = icon,
        });
    }

    public void Rebuild(IReadOnlyList<NavSection> sections)
    {
        entries.Clear();

        foreach (var section in sections)
        {
            foreach (var item in section.Items)
            {
                entries.Add(new SearchEntry
                {
                    PageId = item.Id,
                    PageLabel = section.Label,
                    Label = item.Label,
                    Icon = item.Icon,
                    Keywords = item.Subtitle,
                });
            }
        }

        entries.AddRange(staticEntries);
    }

    public IReadOnlyList<SearchEntry> Search(string query, int limit = 8)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<SearchEntry>();

        var needle = query.Trim();

        return entries
            .Select(entry => (Entry: entry, Score: Score(entry, needle)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Entry.Label.Length)
            .Take(limit)
            .Select(x => x.Entry)
            .ToList();
    }

    private static int Score(SearchEntry entry, string needle)
    {
        int labelScore = MatchScore(entry.Label, needle);
        if (labelScore > 0)
            return labelScore + 40;

        int pageScore = MatchScore(entry.PageLabel, needle);
        if (pageScore > 0)
            return pageScore + 10;

        int keywordScore = MatchScore(entry.Keywords, needle);
        return keywordScore > 0 ? keywordScore : 0;
    }

    private static int MatchScore(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(haystack))
            return 0;

        if (haystack.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
            return 100;

        if (haystack.Contains(needle, StringComparison.OrdinalIgnoreCase))
            return 60;

        return IsSubsequence(haystack, needle) ? 25 : 0;
    }

    private static bool IsSubsequence(string haystack, string needle)
    {
        int index = 0;

        foreach (var c in haystack)
        {
            if (index >= needle.Length)
                break;

            if (char.ToLowerInvariant(c) == char.ToLowerInvariant(needle[index]))
                index++;
        }

        return index == needle.Length;
    }
}
