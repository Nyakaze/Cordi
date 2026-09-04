using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;

namespace Cordi.Services.Chatbox;

public sealed class ChatboxDatabase : IDisposable
{
    private const int SchemaVersion = 5;

    private static bool _nativeReady;
    private static readonly object NativeGate = new();

    private readonly object _gate = new();
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public ChatboxDatabase(string directory)
    {
        EnsureNativeLoaded();

        Directory.CreateDirectory(directory);
        FilePath = Path.Combine(directory, "chatbox.db");

        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = FilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        }.ToString());

        _connection.Open();
        Configure();
        Migrate();
    }

    public string FilePath { get; }

    public bool Available => !_disposed;

    private static void EnsureNativeLoaded()
    {
        lock (NativeGate)
        {
            if (_nativeReady) return;
            _nativeReady = true;

            var pluginDirectory = Service.PluginInterface.AssemblyLocation.Directory?.FullName;
            if (string.IsNullOrEmpty(pluginDirectory)) return;

            var native = Path.Combine(pluginDirectory, "e_sqlite3.dll");
            if (!File.Exists(native)) return;

            try
            {
                NativeLibrary.Load(native);
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "[Chatbox] Failed to preload e_sqlite3.dll");
            }
        }
    }

    private void Configure()
    {
        Execute(_connection, "PRAGMA journal_mode=WAL;");
        Execute(_connection, "PRAGMA synchronous=NORMAL;");
        Execute(_connection, "PRAGMA temp_store=MEMORY;");
        Execute(_connection, "PRAGMA foreign_keys=ON;");
        Execute(_connection, "PRAGMA busy_timeout=4000;");
    }

    private void Migrate()
    {
        Execute(_connection, """
            CREATE TABLE IF NOT EXISTS meta (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS messages (
                seq                INTEGER PRIMARY KEY,
                channel_id         TEXT    NOT NULL,
                origin             INTEGER NOT NULL,
                timestamp          INTEGER NOT NULL,
                author_key         TEXT    NOT NULL,
                author_name        TEXT    NOT NULL,
                author_world       TEXT    NOT NULL,
                avatar_url         TEXT,
                author_color       INTEGER,
                game_chat_type     INTEGER NOT NULL,
                discord_message_id INTEGER NOT NULL,
                discord_channel_id INTEGER NOT NULL,
                raw_content        TEXT    NOT NULL,
                attachments        TEXT,
                reply              TEXT,
                mentions_me        INTEGER NOT NULL,
                is_self            INTEGER NOT NULL,
                filtered_ad        INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_messages_channel_seq ON messages(channel_id, seq);

            CREATE TABLE IF NOT EXISTS channel_state (
                channel_id    TEXT PRIMARY KEY,
                divider_seq   INTEGER NOT NULL DEFAULT 0,
                last_read_seq INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS blobs (
                hash    TEXT PRIMARY KEY,
                bytes   BLOB NOT NULL,
                size    INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS images (
                url        TEXT PRIMARY KEY,
                hash       TEXT NOT NULL REFERENCES blobs(hash) ON DELETE CASCADE,
                fetched_at INTEGER NOT NULL,
                last_used  INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_images_last_used ON images(last_used);

            CREATE TABLE IF NOT EXISTS embeds (
                url        TEXT PRIMARY KEY,
                payload    TEXT NOT NULL,
                fetched_at INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_embeds_fetched ON embeds(fetched_at);

            CREATE TABLE IF NOT EXISTS emotes (
                id        INTEGER PRIMARY KEY,
                name      TEXT    NOT NULL,
                animated  INTEGER NOT NULL DEFAULT 0,
                last_seen INTEGER NOT NULL,
                url       TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_emotes_last_seen ON emotes(last_seen);

            CREATE TABLE IF NOT EXISTS hidden_embeds (
                seq INTEGER NOT NULL,
                url TEXT    NOT NULL,
                PRIMARY KEY (seq, url)
            );
            """);

        EnsureColumn("messages", "filtered_ad", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn("messages", "source", "BLOB");
        EnsureColumn("emotes", "url", "TEXT");

        Execute(_connection, $"INSERT OR REPLACE INTO meta(key, value) VALUES ('schema', '{SchemaVersion}');");
    }

    private void EnsureColumn(string table, string column, string definition)
    {
        using var probe = _connection.CreateCommand();
        probe.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $column;";
        probe.Parameters.AddWithValue("$column", column);

        if (Convert.ToInt32(probe.ExecuteScalar()) > 0) return;

        Execute(_connection, $"ALTER TABLE {table} ADD COLUMN {column} {definition};");
    }

    public void Write(Action<SqliteConnection> action, string context)
    {
        if (_disposed) return;

        lock (_gate)
        {
            if (_disposed) return;

            try
            {
                action(_connection);
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, $"[Chatbox] Database write failed: {context}");
            }
        }
    }

    public void Transact(Action<SqliteConnection> action, string context)
    {
        Write(connection =>
        {
            using var transaction = connection.BeginTransaction();
            action(connection);
            transaction.Commit();
        }, context);
    }

    public T Read<T>(Func<SqliteConnection, T> reader, T fallback, string context)
    {
        if (_disposed) return fallback;

        lock (_gate)
        {
            if (_disposed) return fallback;

            try
            {
                return reader(_connection);
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, $"[Chatbox] Database read failed: {context}");
                return fallback;
            }
        }
    }

    public long FileSizeBytes()
    {
        try
        {
            var total = 0L;
            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                var info = new FileInfo(FilePath + suffix);
                if (info.Exists) total += info.Length;
            }

            return total;
        }
        catch
        {
            return 0;
        }
    }

    public void Compact()
    {
        Write(connection =>
        {
            Execute(connection, "PRAGMA wal_checkpoint(TRUNCATE);");
            Execute(connection, "VACUUM;");
        }, "compact");
    }

    public static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                Execute(_connection, "PRAGMA wal_checkpoint(TRUNCATE);");
            }
            catch (Exception ex)
            {
                Service.Log.Debug($"[Chatbox] Checkpoint on shutdown failed: {ex.Message}");
            }

            _connection.Dispose();
        }
    }
}
