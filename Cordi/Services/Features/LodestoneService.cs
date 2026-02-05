using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    private readonly ConcurrentDictionary<string, string> _avatarCache = new();
    private readonly ConcurrentDictionary<string, int> _gearLevelCache = new();
    private readonly ConcurrentDictionary<string, string> _characterCache = new();

    public IReadOnlyDictionary<string, string> AvatarCache => _avatarCache;

    public LodestoneService(CordiPlugin plugin)
    {
        _plugin = plugin;
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
                _characterCache[kvp.Key] = kvp.Value;
            }
            Logger.Info($"Loaded {_characterCache.Count} cached character IDs from configuration.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to initialize NetStone: {ex.Message}");
        }
    }

    public async Task<string> GetAvatarUrlAsync(string name, string world)
    {
        var key = $"{name}@{world}";
        if (_avatarCache.TryGetValue(key, out var cachedUrl))
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
            _avatarCache[key] = customAvatar;
            return customAvatar;
        }

        // Otherwise use Lodestone avatar if available
        if (character != null && character.Avatar != null)
        {
            var avatar = character.Avatar.ToString();
            _avatarCache[key] = avatar;
            return avatar;
        }

        return fallbackUrl;
    }

    public async Task<int> GetAverageItemLevelAsync(string name, string world)
    {
        var key = $"{name}@{world}";
        if (_gearLevelCache.TryGetValue(key, out var cachedLevel))
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

            _gearLevelCache[key] = avg;
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
            if (_characterCache.TryGetValue(key, out var cachedId))
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
            _characterCache[key] = entry.Id;
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
        if (_characterCache.TryGetValue(key, out var cachedId))
        {
            Logger.Debug($"Using cached Lodestone ID for {name}@{world}: {cachedId}");
            return cachedId;
        }

        return null;
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
        _avatarCache[key] = url;
        Logger.Info($"Updated avatar cache for {key}: {url}");
    }

    public void InvalidateAvatarCache(string key)
    {
        if (_avatarCache.TryRemove(key, out _))
        {
            Logger.Info($"Invalidated avatar cache for {key}");
        }
    }

    public void Dispose()
    {
    }
}
