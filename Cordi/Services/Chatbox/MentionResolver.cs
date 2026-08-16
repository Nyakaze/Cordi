using System;
using System.Collections.Generic;
using System.Linq;

namespace Cordi.Services.Chatbox;

public sealed class MentionResolver
{
    public string? LocalFullName { get; set; }
    public IReadOnlyCollection<string> LocalNameParts { get; set; } = Array.Empty<string>();
    public ulong SelfDiscordId { get; set; }
    public IReadOnlyCollection<string> Keywords { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<string> KnownNames { get; set; } = Array.Empty<string>();

    public Func<ulong, string?>? ResolveUser { get; set; }
    public Func<ulong, string?>? ResolveRole { get; set; }
    public Func<ulong, string?>? ResolveChannel { get; set; }
    public Func<string, string?>? ResolveEmoteByName { get; set; }

    public bool MatchOwnName { get; set; } = true;
    public bool MatchOwnNameParts { get; set; } = true;

    public bool IsSelfUser(ulong id) => SelfDiscordId != 0 && id == SelfDiscordId;

    public bool TargetsMe(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        if (MatchOwnName && !string.IsNullOrEmpty(LocalFullName)
            && string.Equals(name, LocalFullName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (MatchOwnNameParts && LocalNameParts.Any(part =>
                !string.IsNullOrWhiteSpace(part)
                && string.Equals(name, part, StringComparison.OrdinalIgnoreCase)))
            return true;

        return Keywords.Any(keyword =>
            !string.IsNullOrWhiteSpace(keyword)
            && string.Equals(name, keyword.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public bool TryMatchPlainName(string content, int atIndex, out int length, out string name, out bool targetsMe)
    {
        length = 0;
        name = string.Empty;
        targetsMe = false;

        foreach (var candidate in Candidates())
        {
            if (candidate.Length == 0) continue;
            if (atIndex + candidate.Length > content.Length) continue;
            if (string.Compare(content, atIndex, candidate, 0, candidate.Length, StringComparison.OrdinalIgnoreCase) != 0)
                continue;

            var end = atIndex + candidate.Length;
            if (end < content.Length && (char.IsLetterOrDigit(content[end]) || content[end] == '\'')) continue;

            if (candidate.Length <= length) continue;

            length = candidate.Length;
            name = candidate;
            targetsMe = TargetsMe(candidate);
        }

        return length > 0;
    }

    private IEnumerable<string> Candidates()
    {
        if (!string.IsNullOrEmpty(LocalFullName)) yield return LocalFullName!;

        foreach (var part in LocalNameParts)
            if (!string.IsNullOrWhiteSpace(part))
                yield return part;

        foreach (var keyword in Keywords)
            if (!string.IsNullOrWhiteSpace(keyword))
                yield return keyword.Trim();

        foreach (var known in KnownNames)
            if (!string.IsNullOrWhiteSpace(known))
                yield return known;
    }
}
