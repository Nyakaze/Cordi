using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using Dalamud.Game.Text;
using Microsoft.Data.Sqlite;

namespace Cordi.Services.Chatbox;

public sealed class ChatboxMessageStore : IDisposable
{
    private const int TrimSlack = 250;
    private const int FlushIntervalMs = 750;

    private static readonly JsonSerializerOptions Json = new() { IncludeFields = false };

    private readonly ChatboxDatabase _database;
    private readonly Func<string, int> _limitFor;
    private readonly BlockingCollection<ChatboxMessage> _pending = new(new ConcurrentQueue<ChatboxMessage>());
    private readonly ConcurrentDictionary<string, int> _writtenSinceTrim = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (long Divider, long LastRead)> _pendingState = new(StringComparer.Ordinal);
    private readonly Thread _writer;
    private volatile bool _stopping;
    private bool _disposed;

    public ChatboxMessageStore(ChatboxDatabase database, Func<string, int> limitFor)
    {
        _database = database;
        _limitFor = limitFor;

        _writer = new Thread(WriterLoop)
        {
            IsBackground = true,
            Name = "Cordi.ChatboxStore",
        };
        _writer.Start();
    }

    public int PendingWrites => _pending.Count;

    public void Enqueue(ChatboxMessage message)
    {
        if (_stopping || string.IsNullOrEmpty(message.ChannelId)) return;

        try
        {
            _pending.Add(message);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    public long HighestSeq() => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT IFNULL(MAX(seq), 0) FROM messages;";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }, 0L, "highest seq");

    public List<ChatboxMessage> Load(string channelId, int limit)
    {
        if (limit <= 0) return new List<ChatboxMessage>();

        return _database.Read(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = LoadSql;
            command.Parameters.AddWithValue("$channel", channelId);
            command.Parameters.AddWithValue("$limit", limit);

            var result = new List<ChatboxMessage>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
                result.Add(ReadMessage(reader));

            result.Reverse();
            return result;
        }, new List<ChatboxMessage>(), "load " + channelId);
    }

    public int CountFor(string channelId) => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM messages WHERE channel_id = $channel;";
        command.Parameters.AddWithValue("$channel", channelId);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }, 0, "count " + channelId);

    public int TotalCount() => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM messages;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }, 0, "total count");

    public void DeleteChannel(string channelId) => _database.Write(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM messages WHERE channel_id = $channel;";
        command.Parameters.AddWithValue("$channel", channelId);
        command.ExecuteNonQuery();
    }, "delete " + channelId);

    public void DeleteAll() => _database.Write(connection =>
    {
        ChatboxDatabase.Execute(connection, "DELETE FROM messages;");
        ChatboxDatabase.Execute(connection, "DELETE FROM channel_state;");
    }, "delete all messages");

    public void DeleteByDiscordMessageId(ulong discordMessageId) => _database.Write(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM messages WHERE discord_message_id = $id;";
        command.Parameters.AddWithValue("$id", (long)discordMessageId);
        command.ExecuteNonQuery();
    }, "delete discord message " + discordMessageId);

    public void PruneOrphans(IReadOnlyCollection<string> knownChannelIds) => _database.Write(connection =>
    {
        using var command = connection.CreateCommand();
        var names = new List<string>(knownChannelIds.Count);
        var index = 0;

        foreach (var id in knownChannelIds)
        {
            var name = "$c" + index++;
            names.Add(name);
            command.Parameters.AddWithValue(name, id);
        }

        command.CommandText = names.Count == 0
            ? "DELETE FROM messages;"
            : "DELETE FROM messages WHERE channel_id NOT IN (" + string.Join(", ", names) + ");";
        command.ExecuteNonQuery();
    }, "prune orphaned channels");

    public (long Divider, long LastRead) LoadState(string channelId) => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT divider_seq, last_read_seq FROM channel_state WHERE channel_id = $channel;";
        command.Parameters.AddWithValue("$channel", channelId);

        using var reader = command.ExecuteReader();
        return reader.Read()
            ? (reader.GetInt64(0), reader.GetInt64(1))
            : (0L, 0L);
    }, (0L, 0L), "load state " + channelId);

    public void QueueState(string channelId, long dividerSeq, long lastReadSeq)
    {
        if (_stopping) return;

        _pendingState[channelId] = (dividerSeq, lastReadSeq);
    }

    private void FlushState()
    {
        foreach (var pair in _pendingState)
        {
            if (!_pendingState.TryRemove(pair.Key, out var state)) continue;

            SaveState(pair.Key, state.Divider, state.LastRead);
        }
    }

    public void SaveState(string channelId, long dividerSeq, long lastReadSeq) => _database.Write(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = SaveStateSql;
        command.Parameters.AddWithValue("$channel", channelId);
        command.Parameters.AddWithValue("$divider", dividerSeq);
        command.Parameters.AddWithValue("$lastRead", lastReadSeq);
        command.ExecuteNonQuery();
    }, "save state " + channelId);

    public void Trim(string channelId, int limit)
    {
        if (limit <= 0) return;

        _database.Write(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = TrimSql;
            command.Parameters.AddWithValue("$channel", channelId);
            command.Parameters.AddWithValue("$limit", limit);
            command.ExecuteNonQuery();
        }, "trim " + channelId);
    }

    private void WriterLoop()
    {
        var batch = new List<ChatboxMessage>(128);

        while (true)
        {
            try
            {
                if (!_pending.TryTake(out var first, FlushIntervalMs))
                {
                    FlushState();
                    if (_stopping) return;
                    continue;
                }

                batch.Add(first);
                while (batch.Count < 512 && _pending.TryTake(out var next))
                    batch.Add(next);

                Flush(batch);
                batch.Clear();
                FlushState();
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }
            catch (Exception ex)
            {
                batch.Clear();
                Service.Log.Error(ex, "[Chatbox] Message writer loop failed");
            }
        }
    }

    private void Flush(List<ChatboxMessage> batch)
    {
        if (batch.Count == 0) return;

        _database.Transact(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = InsertSql;

            var parameters = command.Parameters;
            parameters.Add("$seq", SqliteType.Integer);
            parameters.Add("$channel", SqliteType.Text);
            parameters.Add("$origin", SqliteType.Integer);
            parameters.Add("$timestamp", SqliteType.Integer);
            parameters.Add("$authorKey", SqliteType.Text);
            parameters.Add("$authorName", SqliteType.Text);
            parameters.Add("$authorWorld", SqliteType.Text);
            parameters.Add("$avatarUrl", SqliteType.Text);
            parameters.Add("$authorColor", SqliteType.Integer);
            parameters.Add("$chatType", SqliteType.Integer);
            parameters.Add("$discordMessage", SqliteType.Integer);
            parameters.Add("$discordChannel", SqliteType.Integer);
            parameters.Add("$raw", SqliteType.Text);
            parameters.Add("$attachments", SqliteType.Text);
            parameters.Add("$reply", SqliteType.Text);
            parameters.Add("$mentionsMe", SqliteType.Integer);
            parameters.Add("$isSelf", SqliteType.Integer);
            parameters.Add("$filteredAd", SqliteType.Integer);
            parameters.Add("$source", SqliteType.Blob);

            foreach (var message in batch)
            {
                parameters["$seq"].Value = message.Seq;
                parameters["$channel"].Value = message.ChannelId;
                parameters["$origin"].Value = (int)message.Origin;
                parameters["$timestamp"].Value = ToUnixMs(message.Timestamp);
                parameters["$authorKey"].Value = message.AuthorKey;
                parameters["$authorName"].Value = message.AuthorName;
                parameters["$authorWorld"].Value = message.AuthorWorld;
                parameters["$avatarUrl"].Value = (object?)message.AvatarUrl ?? DBNull.Value;
                parameters["$authorColor"].Value = message.AuthorColor is { } color
                    ? PackColor(color)
                    : (object)DBNull.Value;
                parameters["$chatType"].Value = (int)message.GameChatType;
                parameters["$discordMessage"].Value = unchecked((long)message.DiscordMessageId);
                parameters["$discordChannel"].Value = unchecked((long)message.DiscordChannelId);
                parameters["$raw"].Value = message.RawContent;
                parameters["$attachments"].Value = SerializeAttachments(message.Attachments);
                parameters["$reply"].Value = message.Reply is null
                    ? DBNull.Value
                    : JsonSerializer.Serialize(message.Reply, Json);
                parameters["$mentionsMe"].Value = message.MentionsMe ? 1 : 0;
                parameters["$isSelf"].Value = message.IsSelf ? 1 : 0;
                parameters["$filteredAd"].Value = message.FilteredAsAd ? 1 : 0;
                parameters["$source"].Value = (object?)message.SourcePayload ?? DBNull.Value;

                command.ExecuteNonQuery();
                _writtenSinceTrim.AddOrUpdate(message.ChannelId, 1, (_, existing) => existing + 1);
            }
        }, "flush messages");

        TrimDirtyChannels();
    }

    private void TrimDirtyChannels()
    {
        foreach (var pair in _writtenSinceTrim)
        {
            if (pair.Value < TrimSlack) continue;
            if (!_writtenSinceTrim.TryUpdate(pair.Key, 0, pair.Value)) continue;

            Trim(pair.Key, _limitFor(pair.Key));
        }
    }

    private static string SerializeAttachments(IReadOnlyList<string> attachments) =>
        attachments.Count == 0 ? string.Empty : JsonSerializer.Serialize(attachments, Json);

    private static ChatboxMessage ReadMessage(SqliteDataReader reader)
    {
        var attachmentsJson = reader.IsDBNull(13) ? string.Empty : reader.GetString(13);
        var replyJson = reader.IsDBNull(14) ? null : reader.GetString(14);

        return new ChatboxMessage
        {
            Seq = reader.GetInt64(0),
            ChannelId = reader.GetString(1),
            Origin = (ChatboxOrigin)reader.GetInt32(2),
            Timestamp = FromUnixMs(reader.GetInt64(3)),
            AuthorKey = reader.GetString(4),
            AuthorName = reader.GetString(5),
            AuthorWorld = reader.GetString(6),
            AvatarUrl = reader.IsDBNull(7) ? null : reader.GetString(7),
            AuthorColor = reader.IsDBNull(8) ? null : UnpackColor((uint)reader.GetInt64(8)),
            GameChatType = (XivChatType)reader.GetInt32(9),
            DiscordMessageId = unchecked((ulong)reader.GetInt64(10)),
            DiscordChannelId = unchecked((ulong)reader.GetInt64(11)),
            RawContent = reader.GetString(12),
            Attachments = DeserializeAttachments(attachmentsJson),
            Reply = replyJson is null ? null : Deserialize<ChatboxReplyRef>(replyJson),
            MentionsMe = reader.GetInt32(15) != 0,
            IsSelf = reader.GetInt32(16) != 0,
            FilteredAsAd = reader.GetInt32(17) != 0,
            SourcePayload = reader.IsDBNull(18) ? null : (byte[])reader.GetValue(18),
        };
    }

    private static IReadOnlyList<string> DeserializeAttachments(string json)
    {
        if (string.IsNullOrEmpty(json)) return Array.Empty<string>();
        return Deserialize<string[]>(json) ?? Array.Empty<string>();
    }

    private static T? Deserialize<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static long ToUnixMs(DateTime timestamp)
    {
        var utc = timestamp.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(timestamp, DateTimeKind.Local).ToUniversalTime()
            : timestamp.ToUniversalTime();

        return new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();
    }

    private static DateTime FromUnixMs(long value) =>
        DateTimeOffset.FromUnixTimeMilliseconds(value).ToLocalTime().DateTime;

    private static uint PackColor(Vector4 color) =>
        ((uint)(Math.Clamp(color.X, 0f, 1f) * 255f) << 24)
        | ((uint)(Math.Clamp(color.Y, 0f, 1f) * 255f) << 16)
        | ((uint)(Math.Clamp(color.Z, 0f, 1f) * 255f) << 8)
        | (uint)(Math.Clamp(color.W, 0f, 1f) * 255f);

    private static Vector4 UnpackColor(uint packed) => new(
        ((packed >> 24) & 0xFF) / 255f,
        ((packed >> 16) & 0xFF) / 255f,
        ((packed >> 8) & 0xFF) / 255f,
        (packed & 0xFF) / 255f);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stopping = true;

        _pending.CompleteAdding();
        _writer.Join(TimeSpan.FromSeconds(3));

        var remaining = new List<ChatboxMessage>();
        while (_pending.TryTake(out var message))
            remaining.Add(message);

        Flush(remaining);
        FlushState();
        _pending.Dispose();
    }

    private const string LoadSql = """
        SELECT seq, channel_id, origin, timestamp, author_key, author_name, author_world,
               avatar_url, author_color, game_chat_type, discord_message_id, discord_channel_id,
               raw_content, attachments, reply, mentions_me, is_self, filtered_ad, source
        FROM messages
        WHERE channel_id = $channel
        ORDER BY seq DESC
        LIMIT $limit;
        """;

    private const string InsertSql = """
        INSERT OR REPLACE INTO messages
            (seq, channel_id, origin, timestamp, author_key, author_name, author_world,
             avatar_url, author_color, game_chat_type, discord_message_id, discord_channel_id,
             raw_content, attachments, reply, mentions_me, is_self, filtered_ad, source)
        VALUES
            ($seq, $channel, $origin, $timestamp, $authorKey, $authorName, $authorWorld,
             $avatarUrl, $authorColor, $chatType, $discordMessage, $discordChannel,
             $raw, $attachments, $reply, $mentionsMe, $isSelf, $filteredAd, $source);
        """;

    private const string SaveStateSql = """
        INSERT INTO channel_state(channel_id, divider_seq, last_read_seq)
        VALUES ($channel, $divider, $lastRead)
        ON CONFLICT(channel_id) DO UPDATE SET divider_seq = $divider, last_read_seq = $lastRead;
        """;

    private const string TrimSql = """
        DELETE FROM messages
        WHERE channel_id = $channel
          AND seq <= (
              SELECT seq FROM messages
              WHERE channel_id = $channel
              ORDER BY seq DESC
              LIMIT 1 OFFSET $limit
          );
        """;
}
