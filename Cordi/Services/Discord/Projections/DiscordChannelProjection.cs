using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Connection;
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

    private readonly ConcurrentDictionary<ulong, DiscordGuild> _guilds = new();
    private readonly ConcurrentDictionary<ulong, DiscordMember> _members = new();
    private readonly ConcurrentDictionary<ulong, DiscordChannel> _channels = new();
    private readonly ConcurrentDictionary<ulong, DiscordChannel> _threads = new();
    private readonly ConcurrentDictionary<ulong, string> _resolvedThreadNames = new();
    private readonly ConcurrentDictionary<ulong, byte> _pendingThreadFetches = new();
    private readonly ConcurrentDictionary<ulong, byte> _missingThreads = new();
    private readonly ConcurrentDictionary<ulong, byte> _loadedForums = new();
    private readonly ConcurrentDictionary<ulong, DateTime> _threadRetryAt = new();

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

    public IReadOnlyList<DiscordGuild> Guilds => _guilds.Values.OrderBy(guild => guild.Name).ToArray();

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
        _connection.Register<GuildEmojisUpdatedEvent>((e, _) =>
            UpdateGuild(e.GuildId, guild => guild with { Emojis = e.Emojis }));
        _connection.Register<RoleCreatedEvent>((e, _) => StoreRole(e.GuildId, e.Role));
        _connection.Register<RoleUpdatedEvent>((e, _) => StoreRole(e.GuildId, e.Role));
        _connection.Register<RoleDeletedEvent>((e, _) => UpdateGuild(e.GuildId, guild => guild with
        {
            Roles = guild.Roles.Where(role => role.Id != e.RoleId).ToArray()
        }));
        _connection.Register<MemberJoinedEvent>((e, _) => StoreMember(e.Member));
        _connection.Register<MemberUpdatedEvent>((e, _) => StoreMember(e.Member));
        _connection.Register<MemberLeftEvent>((e, ct) =>
        {
            _members.TryRemove(e.UserId.Value, out _);

            return Task.CompletedTask;
        });
        _connection.Register<GuildMembersChunkEvent>((e, _) =>
        {
            foreach (var member in e.Members) _members[member.User.Id.Value] = member;

            return Task.CompletedTask;
        });
    }

    public DiscordMember? FindMember(ulong userId) =>
        _members.TryGetValue(userId, out var member) ? member : null;

    public DiscordRole? FindRole(ulong roleId)
    {
        foreach (var guild in _guilds.Values)
            if (guild.Role(roleId) is { } role)
                return role;

        return null;
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
        ResolveThread(threadId, out var name);

        return name;
    }

    public ThreadStatus ResolveThread(ulong threadId, out string name)
    {
        if (_threads.TryGetValue(threadId, out var thread))
        {
            name = thread.Name;
            return ThreadStatus.Known;
        }

        if (_resolvedThreadNames.TryGetValue(threadId, out var resolved))
        {
            name = resolved;
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

                foreach (var post in posts) Store(post, guildId);

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

    private Task OnReadyAsync(ReadyEvent e)
    {
        Clear();
        return Task.CompletedTask;
    }

    private Task OnGuildAvailableAsync(GuildAvailableEvent e, CancellationToken ct)
    {
        _guilds[e.GuildId.Value] = e.Guild;

        foreach (var member in e.Members) _members[member.User.Id.Value] = member;
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

        _guilds.TryRemove(guildId, out _);

        foreach (var member in _members.Values.Where(m => m.GuildId?.Value == guildId))
            _members.TryRemove(member.User.Id.Value, out _);

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

    private Task StoreMember(DiscordMember member)
    {
        _members[member.User.Id.Value] = member;

        return Task.CompletedTask;
    }

    private Task StoreRole(Snowflake guildId, DiscordRole role) =>
        UpdateGuild(guildId, guild => guild with
        {
            Roles = guild.Roles.Where(existing => existing.Id != role.Id).Append(role).ToArray()
        });

    private Task UpdateGuild(Snowflake guildId, Func<DiscordGuild, DiscordGuild> update)
    {
        if (_guilds.TryGetValue(guildId.Value, out var guild))
            _guilds[guildId.Value] = update(guild);

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
        _guilds.Clear();
        _members.Clear();
        _channels.Clear();
        _threads.Clear();
        _resolvedThreadNames.Clear();
        _pendingThreadFetches.Clear();
        _missingThreads.Clear();
        _loadedForums.Clear();
        _threadRetryAt.Clear();

        Rebuild();
    }

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

                Store(thread, thread.GuildId);
                _resolvedThreadNames[threadId] = thread.Name;

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
