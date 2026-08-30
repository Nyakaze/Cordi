using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Connection;
using Crovus.Events;
using Crovus.Models;

namespace Cordi.Services.Discord.Projections;

public sealed class DiscordChannelProjection : IDisposable
{
    private const string LogSource = "Projections";

    private readonly CordiPlugin _plugin;
    private readonly DiscordConnection _connection;

    private readonly ConcurrentDictionary<ulong, DiscordChannel> _channels = new();
    private readonly ConcurrentDictionary<ulong, DiscordChannel> _threads = new();
    private readonly ConcurrentDictionary<ulong, string> _resolvedThreadNames = new();
    private readonly ConcurrentDictionary<ulong, byte> _pendingThreadFetches = new();

    private IReadOnlyList<DiscordChannel> _textChannels = [];
    private IReadOnlyList<DiscordChannel> _forumChannels = [];

    private bool _bound;
    private bool _disposed;

    public DiscordChannelProjection(CordiPlugin plugin, DiscordConnection connection)
    {
        _plugin = plugin;
        _connection = connection;
    }

    public IReadOnlyList<DiscordChannel> TextChannels => Volatile.Read(ref _textChannels);

    public IReadOnlyList<DiscordChannel> ForumChannels => Volatile.Read(ref _forumChannels);

    public void Bind()
    {
        if (_bound || _disposed) return;
        _bound = true;

        _connection.Ready += OnReadyAsync;

        _connection.Register<GuildAvailableEvent>(OnGuildAvailableAsync);
        _connection.Register<GuildUnavailableEvent>(OnGuildUnavailableAsync);
        _connection.Register<ChannelCreatedEvent>((e, _) => Track(e.Channel, e.Guild?.Id));
        _connection.Register<ChannelUpdatedEvent>((e, _) => Track(e.Channel, e.Guild?.Id));
        _connection.Register<ChannelDeletedEvent>((e, _) => Forget(e.Channel.Id));
        _connection.Register<ThreadCreatedEvent>((e, _) => Track(e.Thread, e.GuildId));
        _connection.Register<ThreadUpdatedEvent>((e, _) => Track(e.Thread, e.GuildId));
        _connection.Register<ThreadDeletedEvent>((e, _) => Forget(e.Thread.Id));
        _connection.Register<ThreadListSyncEvent>(OnThreadListSyncAsync);
    }

    public DiscordChannel? Find(ulong channelId)
    {
        if (_channels.TryGetValue(channelId, out var channel)) return channel;
        if (_threads.TryGetValue(channelId, out var thread)) return thread;

        return null;
    }

    public IReadOnlyDictionary<ulong, string> GetThreadsForForum(ulong forumChannelId) =>
        _threads.Values
            .Where(thread => thread.ParentId?.Value == forumChannelId)
            .ToDictionary(thread => thread.Id.Value, thread => thread.Name);

    public string GetThreadName(ulong threadId)
    {
        if (_threads.TryGetValue(threadId, out var thread)) return thread.Name;
        if (_resolvedThreadNames.TryGetValue(threadId, out var resolved)) return resolved;

        ResolveThreadName(threadId);

        return threadId.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _connection.Ready -= OnReadyAsync;

        Clear();
    }

    private Task OnReadyAsync(ReadyEvent e)
    {
        Clear();
        return Task.CompletedTask;
    }

    private Task OnGuildAvailableAsync(GuildAvailableEvent e, CancellationToken ct)
    {
        foreach (var channel in e.Channels) Store(channel, e.GuildId);
        foreach (var thread in e.Threads) Store(thread, e.GuildId);

        Rebuild();

        Log.Debug(LogSource,
            $"Projected guild '{e.GuildName}': {e.Channels.Count} channel(s), {e.Threads.Count} thread(s)");

        return Task.CompletedTask;
    }

    private Task OnGuildUnavailableAsync(GuildUnavailableEvent e, CancellationToken ct)
    {
        var guildId = e.GuildId.Value;

        foreach (var channel in _channels.Values.Where(c => c.GuildId?.Value == guildId))
            _channels.TryRemove(channel.Id.Value, out _);

        foreach (var thread in _threads.Values.Where(t => t.GuildId?.Value == guildId))
            _threads.TryRemove(thread.Id.Value, out _);

        Rebuild();

        Log.Debug(LogSource, $"Dropped projection for guild '{e.Guild.Name}'");

        return Task.CompletedTask;
    }

    private Task OnThreadListSyncAsync(ThreadListSyncEvent e, CancellationToken ct)
    {
        foreach (var thread in e.Threads) Store(thread, e.GuildId);

        Rebuild();

        return Task.CompletedTask;
    }

    private Task Track(DiscordChannel channel, Snowflake? guildId)
    {
        Store(channel, guildId);
        Rebuild();

        return Task.CompletedTask;
    }

    private Task Forget(Snowflake channelId)
    {
        _channels.TryRemove(channelId.Value, out _);
        _threads.TryRemove(channelId.Value, out _);
        _resolvedThreadNames.TryRemove(channelId.Value, out _);

        Rebuild();

        return Task.CompletedTask;
    }

    private void Store(DiscordChannel channel, Snowflake? guildId)
    {
        var known = guildId is { } id ? channel.In(id) : channel;

        if (known.IsThread) _threads[known.Id.Value] = known;
        else _channels[known.Id.Value] = known;
    }

    private void Rebuild()
    {
        var known = _channels.Values.ToList();

        Volatile.Write(ref _textChannels, known
            .Where(channel => channel.Type is ChannelType.GuildText or ChannelType.GuildAnnouncement)
            .OrderBy(channel => channel.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray());

        Volatile.Write(ref _forumChannels, known
            .Where(channel => channel.Type is ChannelType.GuildForum)
            .OrderBy(channel => channel.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    private void Clear()
    {
        _channels.Clear();
        _threads.Clear();
        _resolvedThreadNames.Clear();
        _pendingThreadFetches.Clear();

        Rebuild();
    }

    private void ResolveThreadName(ulong threadId)
    {
        if (_connection.Context is not { } context) return;
        if (!_pendingThreadFetches.TryAdd(threadId, 0)) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var thread = await context.Services.Channels.GetAsync(threadId);

                _resolvedThreadNames[threadId] = thread.Name;

                Log.Debug(LogSource, $"Resolved thread {threadId}: {thread.Name}");
            }
            catch (Exception ex)
            {
                _resolvedThreadNames[threadId] = $"[Unknown Thread {threadId}]";

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
