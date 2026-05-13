using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Cordi.Core.Caching;

public class CacheRegistry : ICacheRegistry
{
    private readonly ConcurrentDictionary<string, ICacheStats> _caches = new();

    public void Register(ICacheStats cache)
    {
        if (!_caches.TryAdd(cache.Name, cache))
            throw new InvalidOperationException($"Cache name '{cache.Name}' is already registered");
    }

    public void Unregister(string name) => _caches.TryRemove(name, out _);

    public IReadOnlyList<ICacheStats> All => _caches.Values.ToList();
}
