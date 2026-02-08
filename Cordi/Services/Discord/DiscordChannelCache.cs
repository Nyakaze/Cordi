using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cordi.Core;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace Cordi.Services.Discord;

/// <summary>
/// Centralized cache for Discord channels to avoid duplicate caching logic across UI tabs.
/// Updates are event-driven to ensure thread safety and performance.
/// </summary>
public class DiscordChannelCache : IDisposable
{
    private readonly CordiPlugin _plugin;
    private DiscordClient? _client;

    private volatile List<DiscordChannel> _textChannels = new();
    private volatile List<DiscordChannel> _forumChannels = new();
    private Dictionary<ulong, string> _threadsByParent = new();
    private readonly object _threadLock = new();

    public IReadOnlyList<DiscordChannel> TextChannels => _textChannels;
    public IReadOnlyList<DiscordChannel> ForumChannels => _forumChannels;

    public DiscordChannelCache(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public void Bind(DiscordClient client)
    {
        Unbind();
        _client = client;
        _client.GuildDownloadCompleted += OnGuildDownloadCompleted;
        _client.ChannelCreated += OnChannelCheck;
        _client.ChannelDeleted += OnChannelCheck;
        _client.ChannelUpdated += OnChannelCheck;
        _client.ThreadCreated += OnThreadCheck;
        _client.ThreadDeleted += OnThreadCheck;
        _client.ThreadUpdated += OnThreadCheck;

        if (client.Guilds.Count > 0) UpdateCache();
    }

    public void Unbind()
    {
        if (_client == null) return;
        _client.GuildDownloadCompleted -= OnGuildDownloadCompleted;
        _client.ChannelCreated -= OnChannelCheck;
        _client.ChannelDeleted -= OnChannelCheck;
        _client.ChannelUpdated -= OnChannelCheck;
        _client.ThreadCreated -= OnThreadCheck;
        _client.ThreadDeleted -= OnThreadCheck;
        _client.ThreadUpdated -= OnThreadCheck;
        _client = null;

        _textChannels = new List<DiscordChannel>();
        _forumChannels = new List<DiscordChannel>();
        lock (_threadLock) _threadsByParent.Clear();
    }

    private Task OnGuildDownloadCompleted(DiscordClient sender, GuildDownloadCompletedEventArgs e)
    {
        UpdateCache();
        return Task.CompletedTask;
    }

    private Task OnChannelCheck(DiscordClient sender, ChannelCreateEventArgs e) { UpdateCache(); return Task.CompletedTask; }
    private Task OnChannelCheck(DiscordClient sender, ChannelDeleteEventArgs e) { UpdateCache(); return Task.CompletedTask; }
    private Task OnChannelCheck(DiscordClient sender, ChannelUpdateEventArgs e) { UpdateCache(); return Task.CompletedTask; }

    private Task OnThreadCheck(DiscordClient sender, ThreadCreateEventArgs e) { UpdateThreadCache(); return Task.CompletedTask; }
    private Task OnThreadCheck(DiscordClient sender, ThreadDeleteEventArgs e) { UpdateThreadCache(); return Task.CompletedTask; }
    private Task OnThreadCheck(DiscordClient sender, ThreadUpdateEventArgs e) { UpdateThreadCache(); return Task.CompletedTask; }

    private void UpdateCache()
    {
        if (_client == null) return;

        try
        {
            var allChannels = _client.Guilds.Values
                .SelectMany(g => g.Channels.Values)
                .ToList();

            _textChannels = allChannels.Where(c => c.Type == ChannelType.Text || c.Type == ChannelType.News).ToList();
            _forumChannels = allChannels.Where(c => c.Type == ChannelType.GuildForum).ToList();

            UpdateThreadCache();
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Failed to update channel cache");
        }
    }

    private void UpdateThreadCache()
    {
        if (_client == null) return;
        try
        {
            var newThreadParams = new Dictionary<ulong, string>();
            foreach (var guild in _client.Guilds.Values)
            {
                foreach (var thread in guild.Threads.Values)
                {
                    newThreadParams[thread.Id] = thread.Name;
                }
            }

            lock (_threadLock)
            {
                _threadsByParent = newThreadParams;
            }
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Failed to update thread cache");
        }
    }

    /// <summary>
    /// Gets threads that belong to a specific parent forum channel.
    /// Thread-safe.
    /// </summary>
    public Dictionary<ulong, string> GetThreadsForForum(ulong forumChannelId)
    {
        if (_client == null) return new Dictionary<ulong, string>();

        var result = new Dictionary<ulong, string>();

        try
        {
            foreach (var guild in _client.Guilds.Values)
            {
                if (guild.Channels.TryGetValue(forumChannelId, out _))
                {
                    // It's in this guild.
                    foreach (var thread in guild.Threads.Values)
                    {
                        if (thread.ParentId == forumChannelId)
                        {
                            result[thread.Id] = thread.Name;
                        }
                    }
                    return result;
                }
            }
        }
        catch { }

        return result;
    }

    /// <summary>
    /// Deprecated: Cache is now event-driven.
    /// </summary>
    public void RefreshIfNeeded()
    {
        // No-op
    }

    /// <summary>
    /// Forces a cache update.
    /// </summary>
    public void Invalidate()
    {
        UpdateCache();
    }

    public void Dispose()
    {
        Unbind();
    }
}
