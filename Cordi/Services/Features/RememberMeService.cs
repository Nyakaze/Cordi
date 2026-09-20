using System;
using System.Collections.Generic;
using System.Linq;
using Cordi.Configuration;
using Cordi.Core;

namespace Cordi.Services.Features;

public class RememberMeService : IDisposable
{
    private readonly Configuration.Configuration _config;
    private readonly CordiLogService Log;
    private const string LogSource = "RememberMe";

    public RememberMeService(Configuration.Configuration config, CordiLogService log)
    {
        _config = config;
        Log = log;
    }

    private RememberedPlayerEntry? FindInList(List<RememberedPlayerEntry> list, string name, string world)
    {
        return list.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            p.World.Equals(world, StringComparison.OrdinalIgnoreCase));
    }

    public RememberedPlayerEntry? FindPlayer(string name, string world)
    {
        return FindInList(_config.RememberMe.RememberedPlayers, name, world);
    }

    public RememberedPlayerEntry? FindPlayerByLodestoneId(string lodestoneId)
    {
        if (string.IsNullOrWhiteSpace(lodestoneId))
            return null;

        return _config.RememberMe.RememberedPlayers
        .FirstOrDefault(p => p.LodestoneId.Equals(lodestoneId, StringComparison.OrdinalIgnoreCase));
    }

    public void AddOrUpdatePlayer(string name, string world, string? lodestoneId = null, string? notes = null, PlayerGlamour? glamour = null)
    {
        var existing = FindPlayer(name, world);

        if (existing != null)
        {
            existing.LastSeen = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(lodestoneId) && string.IsNullOrWhiteSpace(existing.LodestoneId))
            {
                existing.LodestoneId = lodestoneId;
            }

            if (!string.IsNullOrWhiteSpace(notes))
            {
                existing.Notes = notes;
            }

            if (glamour != null)
            {
                existing.Glamour = glamour;
            }
        }
        else
        {
            var newEntry = new RememberedPlayerEntry(name, world, lodestoneId ?? string.Empty)
            {
                Notes = notes ?? string.Empty,
                Glamour = glamour
            };
            _config.RememberMe.RememberedPlayers.Add(newEntry);
            Log.Info(LogSource, $"Remembered new player: {name}@{world}");
        }

        _config.Save();
    }

    public void UpdateNotes(string name, string world, string notes)
    {
        var player = FindPlayer(name, world);
        if (player != null)
        {
            player.Notes = notes;
            _config.Save();
        }
    }

    public void UpdateLastSeen(string name, string world)
    {
        var player = FindPlayer(name, world);
        if (player != null)
        {
            player.LastSeen = DateTime.Now;
            _config.Save();
        }
    }

    public void RemovePlayer(string name, string world)
    {
        var player = FindInList(_config.RememberMe.RememberedPlayers, name, world);
        if (player != null)
        {
            _config.RememberMe.RememberedPlayers.Remove(player);
            Log.Info(LogSource, $"Removed player: {name}@{world}");
            _config.Save();
        }
    }

    public List<RememberedPlayerEntry> GetAllPlayers()
    {
        return _config.RememberMe.RememberedPlayers.OrderByDescending(p => p.LastSeen).ToList();
    }

    public List<RememberedPlayerEntry> SearchPlayers(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return GetAllPlayers();

        var search = searchText.ToLower();
        return _config.RememberMe.RememberedPlayers
            .Where(p => p.Name.ToLower().Contains(search)
                     || p.World.ToLower().Contains(search)
                     || (p.Notes?.ToLower().Contains(search) ?? false))
            .OrderByDescending(p => p.LastSeen)
            .ToList();
    }

    public void Dispose()
    {
    }
}
