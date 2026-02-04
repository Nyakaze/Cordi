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

namespace Cordi.Services;

public class AvatarService : IDisposable
{
    private static readonly IPluginLog Logger = Service.Log;
    private readonly CordiPlugin _plugin;
    private LodestoneClient? _lodestone;
    private readonly ConcurrentDictionary<string, string> _avatarCache = new();

    public IReadOnlyDictionary<string, string> AvatarCache => _avatarCache;

    public AvatarService(CordiPlugin plugin)
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
        if (_plugin.Config.Chat.CustomAvatars.TryGetValue(key, out var customAvatar))
        {
            _avatarCache[key] = customAvatar;
            return customAvatar;
        }

        string fallbackUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(name)}&background=random";

        if (_lodestone == null)
        {
            await InitializeAsync();
            if (_lodestone == null) return fallbackUrl;
        }

        try
        {
            var query = new CharacterSearchQuery
            {
                CharacterName = name,
                World = world
            };

            CharacterSearchEntry? entry = null;

            for (int i = 1; i <= 10; i++)
            {
                Logger.Debug($"Searching for character {name}@{world} (Page {i})");
                var page = await _lodestone.SearchCharacter(query, page: i);
                if (page == null) break;

                entry = page.Results.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (entry != null) break;
                if (page.CurrentPage >= page.NumPages) break;

                await Task.Delay(250);
            }

            if (entry != null && !string.IsNullOrEmpty(entry.Id))
            {
                var character = await _lodestone.GetCharacter(entry.Id);

                if (character != null && character.Avatar != null)
                {
                    Logger.Debug($"Found character {name}@{world} with avatar {character.Avatar}");
                    var avatar = character.Avatar.ToString();
                    _avatarCache[key] = avatar;
                    return avatar;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"Lodestone lookup failed (NetStone) for {name}@{world}: {ex.Message}");
        }

        _avatarCache[key] = fallbackUrl;
        return fallbackUrl;
    }

    public void ClearAvatarCache()
    {
        _avatarCache.Clear();
        Logger.Info("Cleared avatar cache.");
    }

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
