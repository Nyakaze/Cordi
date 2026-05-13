using System;
using System.Collections.Generic;
using Cordi.Domain.Tracking;

namespace Cordi.Services.Storage;

public interface IPlayerTrackingStorage : IDisposable
{
    TrackedPlayer? GetByContentId(ulong contentId);
    TrackedPlayer? GetByLodestoneId(string lodestoneId);
    TrackedPlayer? GetByNameWorldKey(string key);
    TrackedPlayer? GetByLocalId(Guid id);

    IReadOnlyList<TrackedPlayer> GetAll(int skip = 0, int limit = 100);
    IReadOnlyList<TrackedPlayer> Search(string query, int limit = 100);
    IReadOnlyList<TrackedPlayer> GetProvisionalForLookup(DateTime retryBefore, int limit);

    void Upsert(TrackedPlayer player);
    bool Delete(Guid localId);
    int Count();
}
