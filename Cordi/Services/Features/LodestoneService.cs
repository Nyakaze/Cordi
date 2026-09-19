using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core.Caching;
using NetStone;
using NetStone.Search.Character;
using NetStone.Model.Parseables.Search.Character;
using Cordi.Core;
using Cordi.Domain;

namespace Cordi.Services.Features;

public class LodestoneService : IDisposable
{
    private const string LogSource = "Lodestone";
    private const int MaxSearchPages = 10;

    private readonly CordiPlugin _plugin;
    private readonly SemaphoreSlim _initGate = new(1, 1);
    private readonly Cache<string, string> _avatarCache;
    private readonly Cache<string, int> _gearLevelCache;
    private readonly Cache<string, byte> _notFound;
    private readonly LodestoneIdStore _ids;

    private LodestoneClient? _lodestone;

    public LodestoneService(CordiPlugin plugin, string configDirectory)
    {
        _plugin = plugin;
        _avatarCache = new Cache<string, string>("lodestone.avatars", plugin.CacheRegistry,
            maxSize: 500, ttl: TimeSpan.FromHours(6));
        _gearLevelCache = new Cache<string, int>("lodestone.gearLevels", plugin.CacheRegistry,
            maxSize: 200, ttl: TimeSpan.FromHours(1));
        _notFound = new Cache<string, byte>("lodestone.notFound", plugin.CacheRegistry,
            maxSize: 500, ttl: TimeSpan.FromHours(1));
        _ids = new LodestoneIdStore(configDirectory, plugin.LogService);
    }

    public LodestoneIdStore Ids => _ids;

    public IReadOnlyCollection<string> AvatarCacheKeys => _avatarCache.Keys;

    public bool TryGetAvatar(string key, out string url) => _avatarCache.TryGet(key, out url);

    private CordiLogService Log => _plugin.LogService;

    public async Task InitializeAsync()
    {
        if (_lodestone != null) return;

        await _initGate.WaitAsync();

        try
        {
            if (_lodestone != null) return;

            var migrating = _plugin.Config.Lodestone.CharacterIdCache.Count > 0;

            _ids.Load(_plugin.Config.Lodestone.CharacterIdCache);

            if (migrating) _plugin.Config.Save();

            _lodestone = await LodestoneClient.GetClientAsync();

            Log.Info(LogSource, $"NetStone ready with {_ids.Count} known character ids");
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, "Failed to initialize NetStone", ex);
        }
        finally
        {
            _initGate.Release();
        }
    }

    public async Task<string> GetAvatarUrlAsync(Player player)
    {
        if (!string.IsNullOrEmpty(player.AvatarUrl)) return player.AvatarUrl;

        var key = player.FullName;

        if (_avatarCache.TryGet(key, out var cached))
        {
            player.AvatarUrl = cached;
            return cached;
        }

        if (_plugin.Config.Chat.CustomAvatars.TryGetValue(key, out var custom) && !string.IsNullOrEmpty(custom))
        {
            _avatarCache.Set(key, custom);
            player.AvatarUrl = custom;
            return custom;
        }

        var character = await GetCharacterAsync(player);

        if (character?.Avatar != null)
        {
            var avatar = character.Avatar.ToString();
            _avatarCache.Set(key, avatar);
            player.AvatarUrl = avatar;
            return avatar;
        }

        return $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(player.Name)}&background=random";
    }

    public async Task<string?> ResolveLodestoneIdAsync(Player player)
    {
        if (!string.IsNullOrEmpty(player.LodestoneId)) return player.LodestoneId;
        if (string.IsNullOrWhiteSpace(player.Name) || string.IsNullOrWhiteSpace(player.World)) return null;

        if (_ids.TryGet(player.FullName, out var known))
        {
            player.LodestoneId = known;
            return known;
        }

        await GetCharacterAsync(player);

        return player.LodestoneId;
    }

    public string? PeekLodestoneId(Player player)
    {
        if (!string.IsNullOrEmpty(player.LodestoneId)) return player.LodestoneId;

        if (_ids.TryGet(player.FullName, out var known))
        {
            player.LodestoneId = known;
            return known;
        }

        return null;
    }

    public async Task<int> GetAverageItemLevelAsync(Player player)
    {
        var key = player.FullName;

        if (_gearLevelCache.TryGet(key, out var cachedLevel)) return cachedLevel;

        var character = await GetCharacterAsync(player);
        if (character?.Gear == null) return 0;

        try
        {
            var gear = character.Gear;
            var gearEntries = new[]
            {
                gear.Mainhand, gear.Offhand, gear.Head, gear.Body, gear.Hands,
                gear.Legs, gear.Feet, gear.Earrings, gear.Necklace, gear.Bracelets,
                gear.Ring1, gear.Ring2
            };

            var validItems = gearEntries.Where(x => x != null && x.Exists && x.ItemLevel > 0).ToList();
            if (validItems.Count == 0) return 0;

            var average = validItems.Sum(x => x!.ItemLevel) / validItems.Count;

            _gearLevelCache.Set(key, average);
            return average;
        }
        catch (Exception ex)
        {
            Log.Log(LogSource, CordiLogLevel.Warning, $"Failed to calculate the gear level for {key}", ex);
            return 0;
        }
    }

    private async Task<NetStone.Model.Parseables.Character.LodestoneCharacter?> GetCharacterAsync(Player player)
    {
        if (_lodestone == null)
        {
            await InitializeAsync();
            if (_lodestone == null) return null;
        }

        var key = player.FullName;

        try
        {
            if (_ids.TryGet(key, out var knownId))
            {
                player.LodestoneId = knownId;
                return await _lodestone.GetCharacter(knownId);
            }

            if (_notFound.Contains(key)) return null;

            var id = await SearchCharacterIdAsync(player.Name, player.World);

            if (string.IsNullOrEmpty(id))
            {
                _notFound.Set(key, 0);
                return null;
            }

            _ids.Set(key, id);
            player.LodestoneId = id;

            return await _lodestone.GetCharacter(id);
        }
        catch (Exception ex)
        {
            Log.Log(LogSource, CordiLogLevel.Warning, $"Lodestone lookup failed for {key}", ex);
            return null;
        }
    }

    private async Task<string?> SearchCharacterIdAsync(string name, string world)
    {
        if (_lodestone == null) return null;

        var query = new CharacterSearchQuery { CharacterName = name, World = world };

        for (var page = 0; page < MaxSearchPages; page++)
        {
            var results = await _lodestone.SearchCharacter(query, page: page);
            if (results == null) break;

            CharacterSearchEntry? entry = results.Results
                .FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (entry != null) return entry.Id;
            if (results.CurrentPage >= results.NumPages) break;

            await Task.Delay(250);
        }

        return null;
    }

    public void ClearCache()
    {
        _avatarCache.Clear();
        _gearLevelCache.Clear();
        _notFound.Clear();
        _ids.Clear();

        Log.Info(LogSource, "Cleared the lodestone caches");
    }

    public void UpdateAvatarCache(string key, string url)
    {
        _avatarCache.Set(key, url);

        var parts = key.Split('@', 2);

        if (parts.Length == 2 && _plugin.Players.TryGet(parts[0], parts[1], out var player))
            player.AvatarUrl = url;
    }

    public void InvalidateAvatarCache(string key) => _avatarCache.Remove(key);

    public void Dispose()
    {
        _ids.Flush();
        _initGate.Dispose();
    }
}
