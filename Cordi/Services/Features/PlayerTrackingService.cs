using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Core.Caching;
using Cordi.Domain;
using Cordi.Domain.Tracking;
using Cordi.Services.Storage;

namespace Cordi.Services.Features;

public class PlayerTrackingService : IDisposable
{
    private readonly CordiPlugin _plugin;
    private readonly IPlayerTrackingStorage _storage;

    private readonly Cache<string, TrackedPlayer> _byLodestoneId;
    private readonly Cache<string, TrackedPlayer> _byNameWorldKey;

    private static readonly PropertyInfo[] _historyFields =
        typeof(PlayerInfo).GetProperties()
            .Where(p => p.GetCustomAttribute<TrackHistoryAttribute>() != null)
            .ToArray();

    private CordiLogService Log => _plugin.LogService;
    private const string LogSource = "PlayerTracker";

    public PlayerTrackingService(CordiPlugin plugin, IPlayerTrackingStorage storage, ICacheRegistry cacheRegistry)
    {
        _plugin = plugin;
        _storage = storage;
        _byLodestoneId = new Cache<string, TrackedPlayer>(
            "playerTracker.byLodestone", cacheRegistry, maxSize: 500, ttl: TimeSpan.FromMinutes(30));
        _byNameWorldKey = new Cache<string, TrackedPlayer>(
            "playerTracker.byNameWorld", cacheRegistry, maxSize: 500, ttl: TimeSpan.FromMinutes(30));
    }

    public void Observe(Player player, ObservationContext ctx)
    {
        if (string.IsNullOrWhiteSpace(player.Name) || string.IsNullOrWhiteSpace(player.World)) return;

        var nameWorldKey = MakeKey(player.Name, player.World);
        var newInfo = BuildPlayerInfo(player);

        var tracked = Resolve(player.ContentId, player.LodestoneId, nameWorldKey);
        var now = ctx.At ?? DateTime.UtcNow;

        if (tracked == null)
        {
            tracked = CreateNew(player.ContentId, player.LodestoneId, nameWorldKey, newInfo, ctx, now);
            Log.Info(LogSource, $"New player tracked: {player.FullName} via {ctx.Source}");
        }
        else
        {
            MergeKnownInfo(tracked.Info, newInfo);
            ApplyDiff(tracked, newInfo, now);
            FillContentIdIfMissing(tracked, player.ContentId);
            PromoteIfLodestoneFound(tracked, player.LodestoneId);
            RecomputeProvisional(tracked);
        }

        UpdateStats(tracked, ctx, now);
        Persist(tracked);
    }

    private static void FillContentIdIfMissing(TrackedPlayer tracked, ulong? contentId)
    {
        if (!contentId.HasValue || contentId.Value == 0) return;
        if (tracked.ContentId.HasValue) return;
        tracked.ContentId = contentId;
    }

    private static void RecomputeProvisional(TrackedPlayer tracked)
    {
        tracked.IsProvisional = !tracked.ContentId.HasValue && string.IsNullOrEmpty(tracked.LodestoneId);
    }

    private static PlayerInfo BuildPlayerInfo(Player player) => new()
    {
        Name = player.Name,
        World = player.World,
        RaceId = player.RaceId,
        TribeId = player.TribeId,
        Gender = player.Gender,
        FreeCompanyTag = player.FreeCompanyTag,
    };

    private static void MergeKnownInfo(PlayerInfo currentTracked, PlayerInfo incoming)
    {
        if (incoming.RaceId == null && currentTracked.RaceId != null) incoming.RaceId = currentTracked.RaceId;
        if (incoming.TribeId == null && currentTracked.TribeId != null) incoming.TribeId = currentTracked.TribeId;
        if (incoming.Gender == null && currentTracked.Gender != null) incoming.Gender = currentTracked.Gender;
        if (string.IsNullOrEmpty(incoming.FreeCompanyTag) && !string.IsNullOrEmpty(currentTracked.FreeCompanyTag))
            incoming.FreeCompanyTag = currentTracked.FreeCompanyTag;
    }

    public TrackedPlayer? GetByLodestoneId(string lodestoneId)
    {
        if (string.IsNullOrWhiteSpace(lodestoneId)) return null;
        if (_byLodestoneId.TryGet(lodestoneId, out var cached)) return cached;
        var found = _storage.GetByLodestoneId(lodestoneId);
        if (found != null) _byLodestoneId.Set(lodestoneId, found);
        return found;
    }

    public TrackedPlayer? GetByNameWorld(string name, string world)
    {
        var key = MakeKey(name, world);
        if (_byNameWorldKey.TryGet(key, out var cached)) return cached;
        var found = _storage.GetByNameWorldKey(key);
        if (found != null) _byNameWorldKey.Set(key, found);
        return found;
    }

    public IReadOnlyList<TrackedPlayer> Search(string query, int limit = 100) =>
        _storage.Search(query, limit);

    public IReadOnlyList<TrackedPlayer> GetRecent(int limit = 100) =>
        _storage.GetAll(0, limit);

    public int Count() => _storage.Count();
    public int CountConfirmed() => _storage.CountConfirmed();
    public int CountProvisional() => _storage.CountProvisional();
    public int CountSeenSince(DateTime threshold) => _storage.CountSeenSince(threshold);

    public bool Delete(Guid localId)
    {
        var tp = _storage.GetByLocalId(localId);
        if (tp == null) return false;
        if (!string.IsNullOrEmpty(tp.LodestoneId)) _byLodestoneId.Remove(tp.LodestoneId);
        _byNameWorldKey.Remove(tp.NameWorldKey);
        return _storage.Delete(localId);
    }

    public TrackedPlayer? GetByLocalId(Guid localId) => _storage.GetByLocalId(localId);

    public void SaveChanges(TrackedPlayer tracked)
    {
        _storage.Upsert(tracked);
        InvalidateCaches(tracked);
    }

    private TrackedPlayer? Resolve(ulong? contentId, string? lodestoneId, string nameWorldKey)
    {
        if (contentId.HasValue && contentId.Value != 0)
        {
            var byCid = _storage.GetByContentId(contentId.Value);
            if (byCid != null) return byCid;
        }

        if (!string.IsNullOrEmpty(lodestoneId))
        {
            var byId = GetByLodestoneId(lodestoneId);
            if (byId != null) return byId;
        }

        if (_byNameWorldKey.TryGet(nameWorldKey, out var cached)) return cached;
        var fromStorage = _storage.GetByNameWorldKey(nameWorldKey);
        if (fromStorage != null) _byNameWorldKey.Set(nameWorldKey, fromStorage);
        return fromStorage;
    }

    private static TrackedPlayer CreateNew(ulong? contentId, string? lodestoneId, string nameWorldKey, PlayerInfo info, ObservationContext ctx, DateTime now)
    {
        var tp = new TrackedPlayer
        {
            ContentId = (contentId.HasValue && contentId.Value != 0) ? contentId : null,
            LodestoneId = string.IsNullOrEmpty(lodestoneId) ? null : lodestoneId,
            NameWorldKey = nameWorldKey,
            IsProvisional = !(contentId.HasValue && contentId.Value != 0) && string.IsNullOrEmpty(lodestoneId),
            Info = info,
        };
        tp.Stats.FirstSeen = now;
        tp.Stats.FirstSeenVia = ctx.Source;

        foreach (var field in _historyFields)
        {
            var newVal = field.GetValue(info)?.ToString();
            tp.History.Add(new IdentityChange
            {
                When = now,
                Field = field.Name,
                OldValue = null,
                NewValue = newVal,
            });
        }
        return tp;
    }

    private static void ApplyDiff(TrackedPlayer tracked, PlayerInfo newInfo, DateTime now)
    {
        foreach (var field in _historyFields)
        {
            var oldVal = field.GetValue(tracked.Info)?.ToString();
            var newVal = field.GetValue(newInfo)?.ToString();
            if (oldVal == newVal) continue;

            tracked.History.Add(new IdentityChange
            {
                When = now,
                Field = field.Name,
                OldValue = oldVal,
                NewValue = newVal,
            });
            field.SetValue(tracked.Info, field.GetValue(newInfo));
        }
        tracked.NameWorldKey = MakeKey(tracked.Info.Name, tracked.Info.World);
    }

    private void PromoteIfLodestoneFound(TrackedPlayer tracked, string? lodestoneId)
    {
        if (string.IsNullOrEmpty(lodestoneId)) return;
        if (!string.IsNullOrEmpty(tracked.LodestoneId)) return;

        tracked.LodestoneId = lodestoneId;
        tracked.IsProvisional = false;
        Log.Info(LogSource, $"Promoted provisional → confirmed: {tracked.Info.Name}@{tracked.Info.World} (lodestone {lodestoneId})");
    }

    private static void UpdateStats(TrackedPlayer tracked, ObservationContext ctx, DateTime now)
    {
        tracked.Stats.SeenCount++;
        tracked.Stats.LastSeen = now;
        if (ctx.TerritoryId.HasValue) tracked.Stats.LastTerritoryId = ctx.TerritoryId;
        if (ctx.TerritoryName != null) tracked.Stats.LastTerritoryName = ctx.TerritoryName;
        if (ctx.Position.HasValue) tracked.Stats.LastPosition = ctx.Position;
    }

    private void Persist(TrackedPlayer tracked)
    {
        _storage.Upsert(tracked);
        if (!string.IsNullOrEmpty(tracked.LodestoneId))
            _byLodestoneId.Set(tracked.LodestoneId, tracked);
        _byNameWorldKey.Set(tracked.NameWorldKey, tracked);
    }

    public static string MakeKey(string name, string world) =>
        $"{name.ToLowerInvariant()}@{world.ToLowerInvariant()}";

    public IReadOnlyList<TrackedPlayer> GetProvisionalForLookup(TimeSpan retryAfter, int limit) =>
        _storage.GetProvisionalForLookup(DateTime.UtcNow - retryAfter, limit);

    public void MarkLookupAttempted(Guid localId)
    {
        var tp = _storage.GetByLocalId(localId);
        if (tp == null) return;
        tp.LastLodestoneLookupAt = DateTime.UtcNow;
        _storage.Upsert(tp);
        InvalidateCaches(tp);
    }

    public TrackedPlayer? PromoteProvisional(Guid localId, string lodestoneId)
    {
        if (string.IsNullOrWhiteSpace(lodestoneId)) return null;

        var provisional = _storage.GetByLocalId(localId);
        if (provisional == null) return null;
        if (!provisional.IsProvisional) return provisional;

        var existing = _storage.GetByLodestoneId(lodestoneId);

        if (existing == null)
        {
            provisional.LodestoneId = lodestoneId;
            provisional.IsProvisional = false;
            provisional.LastLodestoneLookupAt = DateTime.UtcNow;
            _storage.Upsert(provisional);
            InvalidateCaches(provisional);
            _byLodestoneId.Set(lodestoneId, provisional);
            Log.Info(LogSource, $"Promoted {provisional.Info.Name}@{provisional.Info.World} → lodestone {lodestoneId}");
            return provisional;
        }

        Merge(into: existing, from: provisional);
        existing.LastLodestoneLookupAt = DateTime.UtcNow;
        _storage.Upsert(existing);
        _storage.Delete(provisional.LocalId);
        InvalidateCaches(provisional);
        InvalidateCaches(existing);
        _byLodestoneId.Set(lodestoneId, existing);
        Log.Info(LogSource, $"Merged provisional {provisional.Info.Name}@{provisional.Info.World} into confirmed entry (lodestone {lodestoneId})");
        return existing;
    }

    private static void Merge(TrackedPlayer into, TrackedPlayer from)
    {
        into.Stats.SeenCount += from.Stats.SeenCount;
        if (from.Stats.FirstSeen != default && (into.Stats.FirstSeen == default || from.Stats.FirstSeen < into.Stats.FirstSeen))
        {
            into.Stats.FirstSeen = from.Stats.FirstSeen;
            into.Stats.FirstSeenVia = from.Stats.FirstSeenVia;
        }
        if (from.Stats.LastSeen > into.Stats.LastSeen)
        {
            into.Stats.LastSeen = from.Stats.LastSeen;
            into.Stats.LastTerritoryId = from.Stats.LastTerritoryId ?? into.Stats.LastTerritoryId;
            into.Stats.LastTerritoryName = from.Stats.LastTerritoryName ?? into.Stats.LastTerritoryName;
            into.Stats.LastPosition = from.Stats.LastPosition ?? into.Stats.LastPosition;
        }

        into.History.AddRange(from.History);
        into.History.Sort((a, b) => a.When.CompareTo(b.When));

        foreach (var tag in from.Tags)
            if (!into.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                into.Tags.Add(tag);
    }

    private void InvalidateCaches(TrackedPlayer tp)
    {
        if (!string.IsNullOrEmpty(tp.LodestoneId))
            _byLodestoneId.Remove(tp.LodestoneId);
        if (!string.IsNullOrEmpty(tp.NameWorldKey))
            _byNameWorldKey.Remove(tp.NameWorldKey);
    }

    public int MigrateFromRememberedPlayers(IReadOnlyList<RememberedPlayerEntry> entries)
    {
        if (_storage.Count() > 0) return 0;
        if (entries.Count == 0) return 0;

        int imported = 0;
        foreach (var e in entries)
        {
            if (string.IsNullOrWhiteSpace(e.Name) || string.IsNullOrWhiteSpace(e.World)) continue;

            var info = new PlayerInfo { Name = e.Name, World = e.World };
            var nameWorldKey = MakeKey(e.Name, e.World);
            var lodestoneId = string.IsNullOrWhiteSpace(e.LodestoneId) ? null : e.LodestoneId;
            var when = e.LastSeen == default ? DateTime.UtcNow : e.LastSeen.ToUniversalTime();

            var tp = new TrackedPlayer
            {
                LodestoneId = lodestoneId,
                NameWorldKey = nameWorldKey,
                IsProvisional = lodestoneId == null,
                Info = info,
                Notes = e.Notes ?? string.Empty,
            };
            tp.Stats.FirstSeen = when;
            tp.Stats.LastSeen = when;
            tp.Stats.SeenCount = 1;
            tp.Stats.FirstSeenVia = ObservationSource.Manual;

            foreach (var field in _historyFields)
            {
                tp.History.Add(new IdentityChange
                {
                    When = when,
                    Field = field.Name,
                    OldValue = null,
                    NewValue = field.GetValue(info)?.ToString(),
                });
            }

            _storage.Upsert(tp);
            imported++;
        }

        Log.Info(LogSource, $"Migrated {imported} player(s) from RememberedPlayerEntry list");
        return imported;
    }

    public void Dispose()
    {
        _byLodestoneId.Dispose();
        _byNameWorldKey.Dispose();
    }
}
