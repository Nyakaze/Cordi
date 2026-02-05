using System;
using System.Collections.Generic;
using System.Linq;
using Cordi.Configuration;
using Cordi.Core;

namespace Cordi.Services.Features;

public class RememberMeService : IDisposable
{
    private readonly CordiPlugin plugin;

    public RememberMeService(CordiPlugin plugin)
    {
        this.plugin = plugin;
    }

    public RememberedPlayerEntry? FindPlayer(string name, string world)
    {
        if (!plugin.Config.RememberMe.Enabled) return null;

        return plugin.Config.RememberMe.RememberedPlayers
            .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                              && p.World.Equals(world, StringComparison.OrdinalIgnoreCase));
    }

    public RememberedPlayerEntry? FindPlayerByLodestoneId(string lodestoneId)
    {
        if (!plugin.Config.RememberMe.Enabled || string.IsNullOrWhiteSpace(lodestoneId))
            return null;

        return plugin.Config.RememberMe.RememberedPlayers
            .FirstOrDefault(p => p.LodestoneId.Equals(lodestoneId, StringComparison.OrdinalIgnoreCase));
    }

    public void AddOrUpdatePlayer(string name, string world, string? lodestoneId = null, string? notes = null)
    {
        if (!plugin.Config.RememberMe.Enabled) return;

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
        }
        else
        {
            var newEntry = new RememberedPlayerEntry(name, world, lodestoneId ?? string.Empty)
            {
                Notes = notes ?? string.Empty
            };
            plugin.Config.RememberMe.RememberedPlayers.Add(newEntry);
        }

        plugin.Config.Save();
    }

    public void UpdateNotes(string name, string world, string notes)
    {
        if (!plugin.Config.RememberMe.Enabled) return;

        var player = FindPlayer(name, world);
        if (player != null)
        {
            player.Notes = notes;
            plugin.Config.Save();
        }
    }

    public void UpdateLastSeen(string name, string world)
    {
        if (!plugin.Config.RememberMe.Enabled) return;

        var player = FindPlayer(name, world);
        if (player != null)
        {
            player.LastSeen = DateTime.Now;
            plugin.Config.Save();
        }
    }

    public void RemovePlayer(string name, string world)
    {
        if (!plugin.Config.RememberMe.Enabled) return;

        var player = FindPlayer(name, world);
        if (player != null)
        {
            plugin.Config.RememberMe.RememberedPlayers.Remove(player);
            plugin.Config.Save();
        }
    }

    public List<RememberedPlayerEntry> GetAllPlayers()
    {
        if (!plugin.Config.RememberMe.Enabled)
            return new List<RememberedPlayerEntry>();

        return plugin.Config.RememberMe.RememberedPlayers
            .OrderByDescending(p => p.LastSeen)
            .ToList();
    }

    public List<RememberedPlayerEntry> SearchPlayers(string searchText)
    {
        if (!plugin.Config.RememberMe.Enabled || string.IsNullOrWhiteSpace(searchText))
            return GetAllPlayers();

        var search = searchText.ToLower();
        return plugin.Config.RememberMe.RememberedPlayers
            .Where(p => p.Name.ToLower().Contains(search)
                     || p.World.ToLower().Contains(search)
                     || p.Notes.ToLower().Contains(search))
            .OrderByDescending(p => p.LastSeen)
            .ToList();
    }

    public void Dispose()
    {
    }
}
