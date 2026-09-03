using System;
using System.Collections.Generic;
using Crovus.Models;

namespace Cordi.Services.Emojis;

public sealed class GuildEmoteGroup
{
    public required string Guild { get; init; }

    public required IReadOnlyList<CustomEmote> Emotes { get; init; }
}

public sealed class GuildEmoteCache
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    private readonly Func<IReadOnlyList<DiscordGuild>?> _source;
    private readonly object _gate = new();
    private readonly Dictionary<string, CustomEmote> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ulong, CustomEmote> _byId = new();
    private IReadOnlyList<GuildEmoteGroup> _groups = Array.Empty<GuildEmoteGroup>();
    private DateTime _refreshedAt = DateTime.MinValue;
    private int _version;

    public GuildEmoteCache(Func<IReadOnlyList<DiscordGuild>?> source)
    {
        _source = source;
    }

    public int Version
    {
        get
        {
            lock (_gate) return _version;
        }
    }

    public IReadOnlyList<GuildEmoteGroup> Groups
    {
        get
        {
            Refresh();
            lock (_gate) return _groups;
        }
    }

    public bool TryByName(string? name, out CustomEmote emote)
    {
        emote = default;
        if (string.IsNullOrEmpty(name)) return false;

        Refresh();
        lock (_gate) return _byName.TryGetValue(name, out emote);
    }

    public bool TryById(ulong id, out CustomEmote emote)
    {
        Refresh();
        lock (_gate) return _byId.TryGetValue(id, out emote);
    }

    public bool Contains(ulong id) => TryById(id, out _);

    public void Invalidate()
    {
        lock (_gate) _refreshedAt = DateTime.MinValue;
    }

    private void Refresh()
    {
        lock (_gate)
        {
            if (DateTime.UtcNow - _refreshedAt < RefreshInterval) return;
            _refreshedAt = DateTime.UtcNow;
        }

        var guilds = _source();
        if (guilds == null) return;

        var groups = new List<GuildEmoteGroup>();
        var byName = new Dictionary<string, CustomEmote>(StringComparer.OrdinalIgnoreCase);
        var byId = new Dictionary<ulong, CustomEmote>();

        foreach (var guild in guilds)
        {
            var emotes = new List<CustomEmote>();

            foreach (var emoji in guild.Emojis)
            {
                if (string.IsNullOrEmpty(emoji.Name)) continue;

                var emote = new CustomEmote(emoji.Id.Value, emoji.Name, emoji.Animated);

                emotes.Add(emote);
                byName[emote.Name] = emote;
                byId[emote.Id] = emote;
            }

            if (emotes.Count == 0) continue;

            emotes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            groups.Add(new GuildEmoteGroup
            {
                Guild = string.IsNullOrEmpty(guild.Name) ? "Server" : guild.Name,
                Emotes = emotes,
            });
        }

        lock (_gate)
        {
            _byName.Clear();
            foreach (var pair in byName) _byName[pair.Key] = pair.Value;

            _byId.Clear();
            foreach (var pair in byId) _byId[pair.Key] = pair.Value;

            _groups = groups;
            _version++;
        }
    }
}
