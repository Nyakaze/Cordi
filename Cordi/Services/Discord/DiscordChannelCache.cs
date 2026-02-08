using System;
using System.Collections.Generic;
using System.Linq;
using Cordi.Core;
using DSharpPlus;
using DSharpPlus.Entities;

namespace Cordi.Services.Discord;

/// <summary>
/// Centralized cache for Discord channels to avoid duplicate caching logic across UI tabs.
/// </summary>
public class DiscordChannelCache
{
    private readonly CordiPlugin _plugin;
    private DateTime _lastFetch = DateTime.MinValue;
    private readonly TimeSpan _cacheInterval = TimeSpan.FromSeconds(5);

    private List<DiscordChannel> _textChannels = new();
    private List<DiscordChannel> _forumChannels = new();
    private Dictionary<ulong, string> _threadsByParent = new();

    public IReadOnlyList<DiscordChannel> TextChannels => _textChannels;
    public IReadOnlyList<DiscordChannel> ForumChannels => _forumChannels;

    public DiscordChannelCache(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    /// <summary>
    /// Refreshes the channel cache if the cache interval has expired.
    /// Call this at the start of each Draw() method.
    /// </summary>
    public void RefreshIfNeeded()
    {
        if (DateTime.Now - _lastFetch < _cacheInterval) return;

        _lastFetch = DateTime.Now;

        var client = _plugin.Discord?.Client;
        if (client == null)
        {
            _textChannels.Clear();
            _forumChannels.Clear();
            _threadsByParent.Clear();
            return;
        }

        var allChannels = client.Guilds.Values
            .SelectMany(g => g.Channels.Values)
            .ToList();

        _textChannels = allChannels.Where(c => c.Type == ChannelType.Text).ToList();
        _forumChannels = allChannels.Where(c => c.Type == ChannelType.GuildForum).ToList();
    }

    /// <summary>
    /// Gets threads that belong to a specific parent forum channel.
    /// </summary>
    public Dictionary<ulong, string> GetThreadsForForum(ulong forumChannelId)
    {
        var result = new Dictionary<ulong, string>();
        var client = _plugin.Discord?.Client;
        if (client == null) return result;

        foreach (var guild in client.Guilds.Values)
        {
            if (guild.Channels.ContainsKey(forumChannelId))
            {
                foreach (var thread in guild.Threads.Values)
                {
                    if (thread.ParentId == forumChannelId)
                    {
                        result[thread.Id] = thread.Name;
                    }
                }
                break;
            }
        }
        return result;
    }

    /// <summary>
    /// Forces a cache refresh on next access.
    /// </summary>
    public void Invalidate()
    {
        _lastFetch = DateTime.MinValue;
    }
}
