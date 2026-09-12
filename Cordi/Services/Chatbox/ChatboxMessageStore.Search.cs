using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Dalamud.Game.Text;
using Microsoft.Data.Sqlite;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxMessageStore
{
    private const string SearchColumns =
        "seq, channel_id, origin, timestamp, author_key, author_name, author_world, " +
        "avatar_url, author_color, game_chat_type, discord_message_id, discord_channel_id, " +
        "raw_content, attachments, reply, mentions_me, is_self, filtered_ad, source, tell_target";

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    public ChatboxSearchResults Search(ChatboxSearchQuery query, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();

        Regex? regex = null;

        if (query.Match == ChatboxSearchMatch.Regex && !string.IsNullOrEmpty(query.Text))
        {
            var options = RegexOptions.CultureInvariant;
            if (!query.MatchCase) options |= RegexOptions.IgnoreCase;

            try
            {
                regex = new Regex(query.Text, options, RegexTimeout);
            }
            catch (ArgumentException ex)
            {
                return new ChatboxSearchResults { Error = $"Invalid pattern: {ex.Message}", Elapsed = watch.Elapsed };
            }
        }

        var fallback = new ChatboxSearchResults { Error = "Search failed.", Elapsed = watch.Elapsed };

        return _database.Read(connection => Run(connection, query, regex, token, watch), fallback, "search messages");
    }

    private static ChatboxSearchResults Run(
        SqliteConnection connection,
        ChatboxSearchQuery query,
        Regex? regex,
        CancellationToken token,
        Stopwatch watch)
    {
        using var command = connection.CreateCommand();
        command.CommandText = BuildSql(query, command);

        var comparison = query.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var needle = query.Text.Trim();
        var author = query.Author.Trim();
        var limit = Math.Max(1, query.Limit);
        var cap = Math.Max(limit, query.ScanCap);

        var items = new List<ChatboxMessage>();
        var scanned = 0;
        var matched = 0;
        var capped = false;
        var limited = false;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            if (token.IsCancellationRequested) break;

            scanned++;

            if (scanned > cap)
            {
                capped = true;
                break;
            }

            if (!PassesTime(reader.GetInt64(3), query)) continue;

            var raw = reader.GetString(12);

            if (query.Links != ChatboxSearchFlag.Any)
            {
                var hasLink = raw.Contains("http://", StringComparison.OrdinalIgnoreCase)
                              || raw.Contains("https://", StringComparison.OrdinalIgnoreCase)
                              || raw.Contains("www.", StringComparison.OrdinalIgnoreCase);

                if (hasLink != (query.Links == ChatboxSearchFlag.Only)) continue;
            }

            var name = reader.GetString(5);
            var world = reader.GetString(6);

            if (author.Length > 0 && !MatchesAuthor(name, world, author)) continue;

            if (needle.Length > 0 && !MatchesQueryText(raw, name, world, needle, query, comparison, regex)) continue;

            matched++;
            items.Add(ReadMessage(reader));

            if (items.Count < limit) continue;

            limited = true;
            break;
        }

        return new ChatboxSearchResults
        {
            Items = items,
            Scanned = scanned,
            Matched = matched,
            LimitReached = limited,
            ScanCapReached = capped,
            Elapsed = watch.Elapsed,
        };
    }

    public IReadOnlyList<ChatboxChannelSummary> ChannelSummaries() => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT channel_id, game_chat_type, COUNT(*), MIN(timestamp), MAX(timestamp) " +
            "FROM messages GROUP BY channel_id, game_chat_type;";

        var groups = new Dictionary<string, ChannelTally>(StringComparer.Ordinal);

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var id = reader.GetString(0);
            var type = reader.GetInt32(1);
            var count = reader.GetInt32(2);
            var first = reader.GetInt64(3);
            var last = reader.GetInt64(4);

            if (!groups.TryGetValue(id, out var tally))
                tally = new ChannelTally { First = first, Last = last };

            tally.Total += count;
            tally.First = Math.Min(tally.First, first);
            tally.Last = Math.Max(tally.Last, last);

            if (count > tally.TopCount)
            {
                tally.TopCount = count;
                tally.TopType = type;
            }

            groups[id] = tally;
        }

        var summaries = new List<ChatboxChannelSummary>(groups.Count);

        foreach (var pair in groups)
        {
            summaries.Add(new ChatboxChannelSummary
            {
                Id = pair.Key,
                Count = pair.Value.Total,
                FirstSeen = FromUnixMs(pair.Value.First),
                LastSeen = FromUnixMs(pair.Value.Last),
                DominantType = (XivChatType)pair.Value.TopType,
            });
        }

        summaries.Sort((a, b) => b.LastSeen.CompareTo(a.LastSeen));

        return (IReadOnlyList<ChatboxChannelSummary>)summaries;
    }, Array.Empty<ChatboxChannelSummary>(), "channel summaries");

    private struct ChannelTally
    {
        public int Total;
        public long First;
        public long Last;
        public int TopType;
        public int TopCount;
    }

    private static string BuildSql(ChatboxSearchQuery query, SqliteCommand command)
    {
        var sql = new StringBuilder("SELECT ").Append(SearchColumns).Append(" FROM messages WHERE 1 = 1");

        if (query.ChannelIds.Count > 0)
        {
            sql.Append(" AND channel_id IN (");

            for (var i = 0; i < query.ChannelIds.Count; i++)
            {
                if (i > 0) sql.Append(", ");
                sql.Append("$c").Append(i);
                command.Parameters.AddWithValue($"$c{i}", query.ChannelIds[i]);
            }

            sql.Append(')');
        }

        AppendCategoryFilter(sql, command, query);

        if (query.From is { } from)
        {
            sql.Append(" AND timestamp >= $from");
            command.Parameters.AddWithValue("$from", ToUnixMs(from));
        }

        if (query.To is { } to)
        {
            sql.Append(" AND timestamp < $to");
            command.Parameters.AddWithValue("$to", ToUnixMs(to));
        }

        AppendFlag(sql, "mentions_me", query.Mentions);
        AppendFlag(sql, "is_self", query.FromMe);
        AppendFlag(sql, "filtered_ad", query.FilteredAds);

        switch (query.Attachments)
        {
            case ChatboxSearchFlag.Only:
                sql.Append(" AND attachments IS NOT NULL AND attachments <> ''");
                break;
            case ChatboxSearchFlag.Exclude:
                sql.Append(" AND (attachments IS NULL OR attachments = '')");
                break;
        }

        AppendTextPrefilter(sql, command, query);
        AppendAuthorPrefilter(sql, command, query);

        sql.Append(Descending(query.Sort) ? " ORDER BY seq DESC;" : " ORDER BY seq ASC;");

        return sql.ToString();
    }

    private static void AppendCategoryFilter(StringBuilder sql, SqliteCommand command, ChatboxSearchQuery query)
    {
        if (query.ExcludedCategories.Count == 0) return;

        var excluded = new List<int>();

        foreach (var category in query.ExcludedCategories)
        {
            if (category == ChatboxSearchCategory.System)
                sql.Append(" AND origin <> ").Append((int)ChatboxOrigin.System);

            excluded.AddRange(ChatboxSearchCategories.TypesOf(category));
        }

        if (excluded.Count == 0) return;

        sql.Append(" AND game_chat_type NOT IN (");

        for (var i = 0; i < excluded.Count; i++)
        {
            if (i > 0) sql.Append(", ");

            sql.Append("$g").Append(i);
            command.Parameters.AddWithValue($"$g{i}", excluded[i]);
        }

        sql.Append(')');
    }

    private static void AppendTextPrefilter(StringBuilder sql, SqliteCommand command, ChatboxSearchQuery query)
    {
        if (query.Match == ChatboxSearchMatch.Regex) return;

        var needle = query.Text.Trim();
        if (needle.Length == 0 || !IsAscii(needle)) return;

        var pattern = query.Match switch
        {
            ChatboxSearchMatch.StartsWith => EscapeLike(needle) + "%",
            ChatboxSearchMatch.Exact => EscapeLike(needle),
            _ => "%" + EscapeLike(needle) + "%",
        };

        command.Parameters.AddWithValue("$like", pattern);

        sql.Append(query.Field switch
        {
            ChatboxSearchField.Message => " AND raw_content LIKE $like ESCAPE '\\'",
            ChatboxSearchField.Author => " AND author_name LIKE $like ESCAPE '\\'",
            _ => " AND (raw_content LIKE $like ESCAPE '\\' OR author_name LIKE $like ESCAPE '\\')",
        });
    }

    private static void AppendAuthorPrefilter(StringBuilder sql, SqliteCommand command, ChatboxSearchQuery query)
    {
        var author = query.Author.Trim();
        if (author.Length == 0 || !IsAscii(author)) return;

        command.Parameters.AddWithValue("$author", "%" + EscapeLike(author) + "%");
        sql.Append(" AND (author_name LIKE $author ESCAPE '\\' OR author_world LIKE $author ESCAPE '\\')");
    }

    private static void AppendFlag(StringBuilder sql, string column, ChatboxSearchFlag flag)
    {
        switch (flag)
        {
            case ChatboxSearchFlag.Only:
                sql.Append(" AND ").Append(column).Append(" = 1");
                break;
            case ChatboxSearchFlag.Exclude:
                sql.Append(" AND ").Append(column).Append(" = 0");
                break;
        }
    }

    private static bool Descending(ChatboxSearchSort sort) =>
        sort is not (ChatboxSearchSort.Oldest or ChatboxSearchSort.ChannelThenOldest);

    private static bool PassesTime(long timestamp, ChatboxSearchQuery query)
    {
        if (query.TimeFromMinutes is null && query.TimeToMinutes is null) return true;

        var local = FromUnixMs(timestamp);
        var minutes = local.Hour * 60 + local.Minute;

        var from = query.TimeFromMinutes ?? 0;
        var to = query.TimeToMinutes ?? 1439;

        return from <= to
            ? minutes >= from && minutes <= to
            : minutes >= from || minutes <= to;
    }

    private static bool MatchesAuthor(string name, string world, string author)
    {
        if (name.Contains(author, StringComparison.OrdinalIgnoreCase)) return true;
        if (world.Contains(author, StringComparison.OrdinalIgnoreCase)) return true;

        return world.Length > 0
               && $"{name}@{world}".Contains(author, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesQueryText(
        string raw,
        string name,
        string world,
        string needle,
        ChatboxSearchQuery query,
        StringComparison comparison,
        Regex? regex)
    {
        switch (query.Field)
        {
            case ChatboxSearchField.Message:
                return Matches(raw, needle, query.Match, comparison, regex);

            case ChatboxSearchField.Author:
                return Matches(name, needle, query.Match, comparison, regex)
                       || (world.Length > 0 && Matches($"{name}@{world}", needle, query.Match, comparison, regex));

            default:
                return Matches(raw, needle, query.Match, comparison, regex)
                       || Matches(name, needle, query.Match, comparison, regex);
        }
    }

    private static bool Matches(
        string haystack,
        string needle,
        ChatboxSearchMatch mode,
        StringComparison comparison,
        Regex? regex)
    {
        switch (mode)
        {
            case ChatboxSearchMatch.Regex:
                if (regex == null) return false;

                try
                {
                    return regex.IsMatch(haystack);
                }
                catch (RegexMatchTimeoutException)
                {
                    return false;
                }

            case ChatboxSearchMatch.Exact:
                return haystack.Equals(needle, comparison);

            case ChatboxSearchMatch.StartsWith:
                return haystack.StartsWith(needle, comparison);

            case ChatboxSearchMatch.WholeWord:
                return ContainsWord(haystack, needle, comparison);

            default:
                return haystack.Contains(needle, comparison);
        }
    }

    private static bool ContainsWord(string haystack, string needle, StringComparison comparison)
    {
        var index = 0;

        while (index <= haystack.Length - needle.Length)
        {
            var hit = haystack.IndexOf(needle, index, comparison);
            if (hit < 0) return false;

            var before = hit == 0 || !IsWordChar(haystack[hit - 1]);
            var after = hit + needle.Length >= haystack.Length || !IsWordChar(haystack[hit + needle.Length]);

            if (before && after) return true;

            index = hit + 1;
        }

        return false;
    }

    private static bool IsWordChar(char value) => char.IsLetterOrDigit(value) || value == '_';

    private static bool IsAscii(string value)
    {
        foreach (var character in value)
        {
            if (character > 0x7F) return false;
        }

        return true;
    }

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
