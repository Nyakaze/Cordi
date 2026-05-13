using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cordi.Core.Caching;
using Dalamud.Plugin.Services;
using NetStone;
using NetStone.Search.Character;
using NetStone.Model.Parseables.Search.Character;
using Cordi.Core;
using Cordi.Configuration;

namespace Cordi.Services.Features;

public class LodestoneService : IDisposable
{
    private static readonly IPluginLog Logger = Service.Log;
    private readonly CordiPlugin _plugin;
    private LodestoneClient? _lodestone;
    private readonly Cache<string, string> _avatarCache;
    private readonly Cache<string, int> _gearLevelCache;
    private readonly Cache<string, string> _characterCache;

    public IReadOnlyCollection<string> AvatarCacheKeys => _avatarCache.Keys;
    public bool TryGetAvatar(string key, out string url) => _avatarCache.TryGet(key, out url);

    public LodestoneService(CordiPlugin plugin)
    {
        _plugin = plugin;
        _avatarCache = new Cache<string, string>("lodestone.avatars", plugin.CacheRegistry,
            maxSize: 500, ttl: TimeSpan.FromHours(6));
        _gearLevelCache = new Cache<string, int>("lodestone.gearLevels", plugin.CacheRegistry,
            maxSize: 200, ttl: TimeSpan.FromHours(1));
        _characterCache = new Cache<string, string>("lodestone.characterIds", plugin.CacheRegistry,
            maxSize: 1000, ttl: TimeSpan.FromDays(1));
    }

    public async Task InitializeAsync()
    {
        if (_lodestone != null) return;

        try
        {
            _lodestone = await LodestoneClient.GetClientAsync();
            Logger.Info("NetStone LodestoneClient initialized.");

            // Load cached character IDs from configuration
            foreach (var kvp in _plugin.Config.Lodestone.CharacterIdCache)
            {
                _characterCache.Set(kvp.Key, kvp.Value);
            }
            Logger.Info($"Loaded {_plugin.Config.Lodestone.CharacterIdCache.Count} cached character IDs from configuration.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to initialize NetStone: {ex.Message}");
        }
    }

    public async Task<string> GetAvatarUrlAsync(string name, string world)
    {
        var key = $"{name}@{world}";
        if (_avatarCache.TryGet(key, out var cachedUrl))
        {
            return cachedUrl;
        }

        // Check for custom avatar first, but still fetch character to cache ID
        bool hasCustomAvatar = _plugin.Config.Chat.CustomAvatars.TryGetValue(key, out var customAvatar);

        string fallbackUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(name)}&background=random";

        // Always fetch character to ensure character ID is cached (needed for Tomestone)
        var character = await GetCharacterAsync(name, world);

        // If custom avatar is set, use it
        if (hasCustomAvatar && !string.IsNullOrEmpty(customAvatar))
        {
            _avatarCache.Set(key, customAvatar);
            return customAvatar;
        }

        // Otherwise use Lodestone avatar if available
        if (character != null && character.Avatar != null)
        {
            var avatar = character.Avatar.ToString();
            _avatarCache.Set(key, avatar);
            return avatar;
        }

        return fallbackUrl;
    }

    public async Task<int> GetAverageItemLevelAsync(string name, string world)
    {
        var key = $"{name}@{world}";
        if (_gearLevelCache.TryGet(key, out var cachedLevel))
        {
            return cachedLevel;
        }

        var character = await GetCharacterAsync(name, world);
        if (character == null || character.Gear == null) return 0;

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

            int totalILvl = validItems.Sum(x => x!.ItemLevel);
            int avg = totalILvl / validItems.Count;

            _gearLevelCache.Set(key, avg);
            Logger.Debug($"Fetched Gear Level for {name}@{world}: {avg} (Items: {validItems.Count})");
            return avg;
        }
        catch (Exception ex)
        {
            Logger.Debug($"Failed to calculate gear level for {name}@{world}: {ex.Message}");
            return 0;
        }
    }

    private async Task<NetStone.Model.Parseables.Character.LodestoneCharacter?> GetCharacterAsync(string name, string world)
    {
        if (_lodestone == null)
        {
            await InitializeAsync();
            if (_lodestone == null) return null;
        }

        var key = $"{name}@{world}";

        try
        {
            // Check if we have the character ID cached
            if (_characterCache.TryGet(key, out var cachedId))
            {
                Logger.Debug($"Using cached character ID for {name}@{world}: {cachedId}");
                return await _lodestone.GetCharacter(cachedId);
            }

            // Search for character
            var query = new CharacterSearchQuery
            {
                CharacterName = name,
                World = world
            };
            CharacterSearchEntry? entry = null;

            for (int i = 0; i < 10; i++)
            {
                Logger.Debug($"Searching for character {name}@{world} (Page {i})");
                var page = await _lodestone.SearchCharacter(query, page: i);
                if (page == null) break;

                entry = page.Results.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (entry != null) break;
                if (page.CurrentPage >= page.NumPages) break;

                await Task.Delay(250);
            }
            if (entry == null || string.IsNullOrEmpty(entry.Id)) return null;

            Logger.Debug($"Found character {name}@{world}, caching ID: {entry.Id}");

            // Cache the character ID for future lookups (in-memory and persistent)
            _characterCache.Set(key, entry.Id);
            _plugin.Config.Lodestone.CharacterIdCache[key] = entry.Id;
            _plugin.Config.Save();

            return await _lodestone.GetCharacter(entry.Id);
        }
        catch (Exception ex)
        {
            Logger.Debug($"Lodestone character lookup failed for {name}@{world}: {ex.Message}");
            return null;
        }
    }

    public async Task<string?> GetLodestoneIdAsync(string name, string world)
    {
        if (_lodestone == null)
        {
            await InitializeAsync();
            if (_lodestone == null) return null;
        }

        var key = $"{name}@{world}";

        // Check cache first
        if (_characterCache.TryGet(key, out var cachedId))
        {
            Logger.Debug($"Using cached Lodestone ID for {name}@{world}: {cachedId}");
            return cachedId;
        }

        return null;
    }

    public async Task<string?> ResolveLodestoneIdAsync(string name, string world)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(world)) return null;

        var key = $"{name}@{world}";
        if (_characterCache.TryGet(key, out var cached)) return cached;

        await GetCharacterAsync(name, world);
        return _characterCache.TryGet(key, out var resolved) ? resolved : null;
    }


    public void ClearCache()
    {
        _avatarCache.Clear();
        _gearLevelCache.Clear();
        _characterCache.Clear();
        _plugin.Config.Lodestone.CharacterIdCache.Clear();
        _plugin.Config.Save();
        Logger.Info("Cleared Lodestone cache.");
    }

    public void ClearAvatarCache() => ClearCache();

    public void UpdateAvatarCache(string key, string url)
    {
        _avatarCache.Set(key, url);
        Logger.Info($"Updated avatar cache for {key}: {url}");
    }

    public void InvalidateAvatarCache(string key)
    {
        if (_avatarCache.Remove(key))
        {
            Logger.Info($"Invalidated avatar cache for {key}");
        }
    }

    public void Dispose()
    {
    }
}
