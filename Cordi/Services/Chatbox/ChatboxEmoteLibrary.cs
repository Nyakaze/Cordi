using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Services.Emojis;

namespace Cordi.Services.Chatbox;

public sealed class ChatboxSeenEmote
{
    public ulong Id { get; init; }
    public string Name { get; set; } = string.Empty;
    public bool Animated { get; init; }
    public long LastSeen { get; set; }
    public string? Url { get; set; }

    public string Token => $"<{(Animated ? "a" : string.Empty)}:{Name}:{Id}>";
    public string ImageUrl => string.IsNullOrEmpty(Url) ? EmojiTranslator.EmoteUrl(Id, Animated) : Url!;
}

public sealed class ChatboxEmoteLibrary
{
    private const long TouchIntervalSeconds = 3600;

    private static readonly Regex EmoteUrl = new(
        @"cdn\.discordapp\.com/emojis/(?<id>\d{5,25})\.(?<ext>png|gif|webp)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RawToken = new(
        @"<(?<a>a?):(?<name>[A-Za-z0-9_~]{2,32}):(?<id>\d{5,25})>",
        RegexOptions.Compiled);

    private static readonly Regex EmoteName = new(
        @"^:?(?<name>[A-Za-z0-9_~]{2,32}):?$",
        RegexOptions.Compiled);

    private readonly ChatboxDatabase _database;
    private readonly Func<int> _limit;
    private readonly ConcurrentDictionary<ulong, ChatboxSeenEmote> _entries = new();
    private readonly ConcurrentDictionary<string, ulong> _byName = new(StringComparer.OrdinalIgnoreCase);
    private int _version;

    public ChatboxEmoteLibrary(ChatboxDatabase database, Func<int> limit)
    {
        _database = database;
        _limit = limit;

        Load();

        _ = Task.Run(Backfill);
    }

    public int Count => _entries.Count;

    public int Version => Volatile.Read(ref _version);

    public bool Contains(ulong id) => _entries.ContainsKey(id);

    public ChatboxSeenEmote? FindById(ulong id) =>
        _entries.TryGetValue(id, out var entry) ? entry : null;

    public ChatboxSeenEmote? FindByName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        return _byName.TryGetValue(name, out var id) && _entries.TryGetValue(id, out var entry) ? entry : null;
    }

    public List<ChatboxSeenEmote> Snapshot()
    {
        var list = new List<ChatboxSeenEmote>(_entries.Values);
        list.Sort((a, b) => b.LastSeen.CompareTo(a.LastSeen));
        return list;
    }

    public void Record(ChatboxMessage message)
    {
        if (message.Segments == null) return;

        foreach (var segment in message.Segments)
        {
            if (segment.Kind != SegmentKind.Emote) continue;

            Record(segment.Text, segment.ImageUrl);
        }
    }

    public void Record(string? label, string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return;

        var match = EmoteUrl.Match(imageUrl);
        if (!match.Success) return;
        if (!ulong.TryParse(match.Groups["id"].Value, out var id)) return;

        var nameMatch = EmoteName.Match(label ?? string.Empty);
        var name = nameMatch.Success ? nameMatch.Groups["name"].Value : "emote";
        var animated = string.Equals(match.Groups["ext"].Value, "gif", StringComparison.OrdinalIgnoreCase);

        Record(id, name, animated, imageUrl);
    }

    public void Record(ulong id, string name, bool animated, string? url = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (_entries.TryGetValue(id, out var existing))
        {
            var renamed = !string.Equals(existing.Name, name, StringComparison.Ordinal) && name != "emote";
            var relinked = !string.IsNullOrEmpty(url) && !string.Equals(existing.Url, url, StringComparison.Ordinal);

            if (!renamed && !relinked && now - existing.LastSeen < TouchIntervalSeconds) return;

            if (renamed)
            {
                _byName.TryRemove(existing.Name, out _);
                existing.Name = name;
                _byName[name] = id;
            }

            if (relinked) existing.Url = url;

            existing.LastSeen = now;

            Persist(existing);
            if (renamed) Interlocked.Increment(ref _version);
            return;
        }

        var entry = new ChatboxSeenEmote
        {
            Id = id,
            Name = name,
            Animated = animated,
            LastSeen = now,
            Url = string.IsNullOrEmpty(url) ? null : url,
        };

        if (!_entries.TryAdd(id, entry)) return;

        if (name != "emote") _byName[name] = id;

        Persist(entry);
        Interlocked.Increment(ref _version);

        Trim();
    }

    public void Clear()
    {
        _entries.Clear();
        _byName.Clear();
        Interlocked.Increment(ref _version);

        _database.Write(
            connection => ChatboxDatabase.Execute(connection, "DELETE FROM emotes;"),
            "clear emotes");
    }

    private void Backfill()
    {
        try
        {
            var done = _database.Read(connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT value FROM meta WHERE key = 'emotes_backfilled';";
                return command.ExecuteScalar() != null;
            }, true, "check emote backfill");

            if (done) return;

            var raw = _database.Read(connection =>
            {
                var list = new List<string>();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT raw_content FROM messages WHERE raw_content LIKE '%<%:%:%>%';";

                using var reader = command.ExecuteReader();
                while (reader.Read()) list.Add(reader.GetString(0));

                return list;
            }, new List<string>(), "backfill emotes");

            foreach (var content in raw)
            {
                foreach (Match match in RawToken.Matches(content))
                {
                    if (!ulong.TryParse(match.Groups["id"].Value, out var id)) continue;

                    Record(id, match.Groups["name"].Value, match.Groups["a"].Value.Length > 0);
                }
            }

            _database.Write(
                connection => ChatboxDatabase.Execute(
                    connection,
                    "INSERT OR REPLACE INTO meta(key, value) VALUES ('emotes_backfilled', '1');"),
                "mark emote backfill");
        }
        catch (Exception ex)
        {
            Service.Log.Warning($"[Chatbox] Emote backfill failed: {ex.Message}");
        }
    }

    private void Load()
    {
        _database.Read(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT id, name, animated, last_seen, url FROM emotes;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var entry = new ChatboxSeenEmote
                {
                    Id = (ulong)reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Animated = reader.GetInt32(2) != 0,
                    LastSeen = reader.GetInt64(3),
                    Url = reader.IsDBNull(4) ? null : reader.GetString(4),
                };

                _entries[entry.Id] = entry;
                if (entry.Name != "emote") _byName[entry.Name] = entry.Id;
            }

            return true;
        }, false, "load emotes");

        Interlocked.Increment(ref _version);
    }

    private void Persist(ChatboxSeenEmote entry) => _database.Write(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO emotes(id, name, animated, last_seen, url)
            VALUES ($id, $name, $animated, $lastSeen, $url)
            ON CONFLICT(id) DO UPDATE SET
                name      = excluded.name,
                animated  = excluded.animated,
                last_seen = excluded.last_seen,
                url       = COALESCE(excluded.url, emotes.url);
            """;

        command.Parameters.AddWithValue("$id", (long)entry.Id);
        command.Parameters.AddWithValue("$name", entry.Name);
        command.Parameters.AddWithValue("$animated", entry.Animated ? 1 : 0);
        command.Parameters.AddWithValue("$lastSeen", entry.LastSeen);
        command.Parameters.AddWithValue("$url", (object?)entry.Url ?? DBNull.Value);
        command.ExecuteNonQuery();
    }, "store emote");

    private void Trim()
    {
        var limit = Math.Clamp(_limit(), 0, 5000);
        if (limit <= 0 || _entries.Count <= limit) return;

        var ordered = Snapshot();

        for (var i = limit; i < ordered.Count; i++)
        {
            if (!_entries.TryRemove(ordered[i].Id, out var removed)) continue;

            if (_byName.TryGetValue(removed.Name, out var mapped) && mapped == removed.Id)
                _byName.TryRemove(removed.Name, out _);

            var id = (long)ordered[i].Id;
            _database.Write(connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM emotes WHERE id = $id;";
                command.Parameters.AddWithValue("$id", id);
                command.ExecuteNonQuery();
            }, "trim emotes");
        }

        Interlocked.Increment(ref _version);
    }
}
