using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Connection;
using Crovus.Cache;
using Crovus.Events;
using Crovus.Models;
using Crovus.Rest;

namespace Cordi.Services.Discord.Projections;

public enum ThreadStatus
{
    Known,
    Pending,
    Missing,
}

public sealed class DiscordChannelProjection : IDisposable
{
    private const string LogSource = "Projections";

    private readonly CordiPlugin _plugin;
    private readonly DiscordConnection _connection;

    private readonly ConcurrentDictionary<ulong, byte> _pendingThreadFetches = new();
    private readonly ConcurrentDictionary<ulong, byte> _missingThreads = new();
    private readonly ConcurrentDictionary<ulong, byte> _loadedForums = new();
    private readonly ConcurrentDictionary<ulong, DateTime> _threadRetryAt = new();
    private readonly object _gate = new();

    private IReadOnlyList<DiscordChannel> _textChannels = [];
    private IReadOnlyList<DiscordChannel> _forumChannels = [];
    private long _built = -1;

    private bool _bound;
    private bool _disposed;

    public DiscordChannelProjection(CordiPlugin plugin, DiscordConnection connection)
    {
        _plugin = plugin;
        _connection = connection;
    }

    public IReadOnlyList<DiscordChannel> TextChannels
    {
        get
        {
            Refresh();

            return Volatile.Read(ref _textChannels);
        }
    }

    public IReadOnlyList<DiscordChannel> ForumChannels
    {
        get
        {
            Refresh();

            return Volatile.Read(ref _forumChannels);
        }
    }

    public IReadOnlyList<DiscordGuild> Guilds =>
        Cache?.Guilds.OrderBy(guild => guild.Name).ToArray() ?? [];

    public void Bind()
    {
        if (_bound || _disposed) return;
        _bound = true;

        _connection.Ready += OnReadyAsync;
    }

    public DiscordMember? FindMember(ulong userId)
    {
        if (Cache is not { } cache) return null;

        var id = new Snowflake(userId);

        foreach (var guild in cache.Guilds)
            if (cache.FindMember(guild.Id, id) is { } member)
                return member;

        return null;
    }

    public DiscordRole? FindRole(ulong roleId) => Cache?.FindRole(new Snowflake(roleId));

    public DiscordChannel? Find(ulong channelId) => Cache?.FindChannel(new Snowflake(channelId));

    public IReadOnlyDictionary<ulong, string> GetThreadsForForum(ulong forumChannelId) =>
        Cache?.ThreadsOf(new Snowflake(forumChannelId))
            .ToDictionary(thread => thread.Id.Value, thread => thread.Name)
        ?? new Dictionary<ulong, string>();

    public string GetThreadName(ulong threadId)
    {
        ResolveThread(threadId, out var name);

        return name;
    }

    public ThreadStatus ResolveThread(ulong threadId, out string name)
    {
        if (Find(threadId) is { } thread)
        {
            name = thread.Name;
            return ThreadStatus.Known;
        }

        name = threadId.ToString();

        if (_missingThreads.ContainsKey(threadId)) return ThreadStatus.Missing;

        ResolveThreadName(threadId);

        return ThreadStatus.Pending;
    }

    public void EnsureForumThreadsLoaded(ulong forumChannelId)
    {
        if (_connection.Context is not { } context) return;
        if (Find(forumChannelId)?.GuildId is not { } guildId) return;
        if (!_loadedForums.TryAdd(forumChannelId, 0)) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var posts = await context.Services.Threads.GetPostsAsync(guildId, forumChannelId, archivedLimit: 100);

                foreach (var post in posts) await StoreAsync(context.Cache, post, guildId);

                Log.Debug(LogSource, $"Loaded {posts.Count} post(s) from forum {forumChannelId}");
            }
            catch (Exception ex)
            {
                _loadedForums.TryRemove(forumChannelId, out _);

                Log.Warning(LogSource, $"Failed to load posts from forum {forumChannelId}: {ex.Message}");
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _connection.Ready -= OnReadyAsync;

        Clear();
    }

    private IDiscordCache? Cache => _connection.Context?.Cache;

    private Task OnReadyAsync(ReadyEvent e)
    {
        Clear();

        return Task.CompletedTask;
    }

    private void Clear()
    {
        _pendingThreadFetches.Clear();
        _missingThreads.Clear();
        _loadedForums.Clear();
        _threadRetryAt.Clear();

        lock (_gate)
        {
            Volatile.Write(ref _textChannels, []);
            Volatile.Write(ref _forumChannels, []);
            Volatile.Write(ref _built, -1);
        }
    }

    private void Refresh()
    {
        if (Cache is not { } cache) return;

        var version = cache.ChannelsVersion;

        if (Interlocked.Read(ref _built) == version) return;

        lock (_gate)
        {
            if (Interlocked.Read(ref _built) == version) return;

            var known = cache.Channels;

            Volatile.Write(ref _textChannels, known
                .Where(channel => channel.Type is ChannelType.GuildText or ChannelType.GuildAnnouncement)
                .OrderBy(channel => channel.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray());

            Volatile.Write(ref _forumChannels, known
                .Where(channel => channel.Type is ChannelType.GuildForum)
                .OrderBy(channel => channel.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray());

            Interlocked.Exchange(ref _built, version);
        }
    }

    private static ValueTask StoreAsync(IDiscordCache cache, DiscordChannel channel, Snowflake? guildId) =>
        cache.SetChannelAsync(channel.GuildId is null && guildId is { } id ? channel.In(id) : channel);

    private void ResolveThreadName(ulong threadId)
    {
        if (_connection.Context is not { } context) return;
        if (_threadRetryAt.TryGetValue(threadId, out var retryAt) && DateTime.UtcNow < retryAt) return;
        if (!_pendingThreadFetches.TryAdd(threadId, 0)) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var thread = await context.Services.Channels.GetAsync(threadId);

                await StoreAsync(context.Cache, thread, thread.GuildId);

                Log.Debug(LogSource, $"Resolved thread {threadId}: {thread.Name}");
            }
            catch (DiscordRestException ex) when (ex.StatusCode is HttpStatusCode.NotFound || ex.ErrorCode == 10003)
            {
                _missingThreads[threadId] = 0;

                Log.Debug(LogSource, $"Thread {threadId} no longer exists");
            }
            catch (Exception ex)
            {
                _threadRetryAt[threadId] = DateTime.UtcNow.AddMinutes(5);

                Log.Warning(LogSource, $"Failed to resolve thread {threadId}: {ex.Message}");
            }
            finally
            {
                _pendingThreadFetches.TryRemove(threadId, out _);
            }
        });
    }

    private CordiLogService Log => _plugin.LogService;
}
