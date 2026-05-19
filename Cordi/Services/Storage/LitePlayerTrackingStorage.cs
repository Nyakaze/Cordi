using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Cordi.Domain.Tracking;
using LiteDB;

namespace Cordi.Services.Storage;

public class LitePlayerTrackingStorage : IPlayerTrackingStorage
{
    private const string CollectionName = "tracked_players";

    private static readonly object _mapperLock = new();
    private static bool _mappersRegistered;

    private readonly LiteDatabase _db;
    private readonly ILiteCollection<TrackedPlayer> _coll;
    private bool _disposed;

    public LitePlayerTrackingStorage(string filePath)
    {
        RegisterMappers();

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var connectionString = $"Filename={filePath};Connection=Shared";
        _db = new LiteDatabase(connectionString);
        _coll = _db.GetCollection<TrackedPlayer>(CollectionName);

        _coll.EnsureIndex(p => p.ContentId);
        _coll.EnsureIndex(p => p.LodestoneId);
        _coll.EnsureIndex(p => p.NameWorldKey);
        _coll.EnsureIndex(p => p.Stats.LastSeen);
    }

    public TrackedPlayer? GetByContentId(ulong contentId)
    {
        if (contentId == 0) return null;
        return _coll.FindOne(p => p.ContentId == contentId);
    }

    private static void RegisterMappers()
    {
        lock (_mapperLock)
        {
            if (_mappersRegistered) return;
            BsonMapper.Global.RegisterType(
                serialize: v => new BsonDocument
                {
                    ["X"] = v.X,
                    ["Y"] = v.Y,
                    ["Z"] = v.Z,
                },
                deserialize: bson => new Vector3(
                    (float)bson["X"].AsDouble,
                    (float)bson["Y"].AsDouble,
                    (float)bson["Z"].AsDouble));
            _mappersRegistered = true;
        }
    }

    public TrackedPlayer? GetByLodestoneId(string lodestoneId)
    {
        if (string.IsNullOrWhiteSpace(lodestoneId)) return null;
        return _coll.FindOne(p => p.LodestoneId == lodestoneId);
    }

    public TrackedPlayer? GetByNameWorldKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return _coll.FindOne(p => p.NameWorldKey == key);
    }

    public TrackedPlayer? GetByLocalId(Guid id) =>
        _coll.FindOne(p => p.LocalId == id);

    public IReadOnlyList<TrackedPlayer> GetAll(int skip = 0, int limit = 100) =>
        _coll.Query()
            .OrderByDescending(p => p.Stats.LastSeen)
            .Skip(skip)
            .Limit(limit)
            .ToList();

    public IReadOnlyList<TrackedPlayer> Search(string query, int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(query))
            return GetAll(0, limit);

        var q = query.ToLowerInvariant();
        return _coll.Query()
            .Where(p => p.Info.Name.ToLower().Contains(q)
                     || p.Info.World.ToLower().Contains(q)
                     || p.Notes.ToLower().Contains(q))
            .OrderByDescending(p => p.Stats.LastSeen)
            .Limit(limit)
            .ToList();
    }

    public IReadOnlyList<TrackedPlayer> GetProvisionalForLookup(DateTime retryBefore, int limit)
    {
        var candidates = _coll.Query()
            .Where(p => p.IsProvisional
                     && (p.LastLodestoneLookupAt == null || p.LastLodestoneLookupAt < retryBefore))
            .ToList();

        return candidates
            .OrderBy(p => p.LastLodestoneLookupAt ?? DateTime.MinValue)
            .Take(limit)
            .ToList();
    }

    public void Upsert(TrackedPlayer player) =>
        _coll.Upsert(player.LocalId, player);

    public bool Delete(Guid localId) =>
        _coll.Delete(localId);

    public int Count() => _coll.Count();

    public int CountConfirmed() => _coll.Count(p => p.IsProvisional == false);

    public int CountProvisional() => _coll.Count(p => p.IsProvisional == true);

    public int CountSeenSince(DateTime threshold) =>
        _coll.Count(p => p.Stats.LastSeen >= threshold);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _db.Dispose();
    }
}
