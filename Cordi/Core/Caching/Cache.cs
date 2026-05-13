using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Cordi.Core.Caching;

public class Cache<TKey, TValue> : ICacheStats, IDisposable where TKey : notnull
{
    private readonly ICacheRegistry _registry;
    private readonly TimeSpan? _ttl;
    private readonly ConcurrentDictionary<TKey, Entry> _store = new();
    private long _hits;
    private long _misses;
    private bool _disposed;

    private sealed class Entry
    {
        public TValue Value = default!;
        public DateTime AddedAt;
        public DateTime LastAccessed;
    }

    public string Name { get; }
    public int Capacity { get; }
    public int Count => _store.Count;
    public long Hits => Interlocked.Read(ref _hits);
    public long Misses => Interlocked.Read(ref _misses);
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public Cache(string name, ICacheRegistry registry, int maxSize = 200, TimeSpan? ttl = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Cache name must not be empty", nameof(name));
        if (maxSize < 1)
            throw new ArgumentOutOfRangeException(nameof(maxSize), "maxSize must be >= 1");

        Name = name;
        Capacity = maxSize;
        _ttl = ttl;
        _registry = registry;
        registry.Register(this);
    }

    public void Set(TKey key, TValue value)
    {
        var now = DateTime.UtcNow;
        _store[key] = new Entry { Value = value, AddedAt = now, LastAccessed = now };
        if (_store.Count > Capacity) EvictLru();
    }

    public bool TryAdd(TKey key, TValue value)
    {
        var now = DateTime.UtcNow;
        var entry = new Entry { Value = value, AddedAt = now, LastAccessed = now };
        if (!_store.TryAdd(key, entry)) return false;
        if (_store.Count > Capacity) EvictLru();
        return true;
    }

    public bool TryGet(TKey key, out TValue value)
    {
        if (_store.TryGetValue(key, out var entry))
        {
            if (IsExpired(entry))
            {
                _store.TryRemove(key, out _);
                Interlocked.Increment(ref _misses);
                value = default!;
                return false;
            }
            entry.LastAccessed = DateTime.UtcNow;
            Interlocked.Increment(ref _hits);
            value = entry.Value;
            return true;
        }
        Interlocked.Increment(ref _misses);
        value = default!;
        return false;
    }

    public bool Contains(TKey key)
    {
        if (!_store.TryGetValue(key, out var entry)) return false;
        if (IsExpired(entry))
        {
            _store.TryRemove(key, out _);
            return false;
        }
        return true;
    }

    public bool Remove(TKey key) => _store.TryRemove(key, out _);

    public void Clear() => _store.Clear();

    public IReadOnlyCollection<TKey> Keys => _store.Keys.ToList();

    private bool IsExpired(Entry entry) =>
        _ttl.HasValue && DateTime.UtcNow - entry.AddedAt > _ttl.Value;

    private void EvictLru()
    {
        int overflow = _store.Count - Capacity;
        if (overflow <= 0) return;

        var oldest = _store
            .OrderBy(kv => kv.Value.LastAccessed)
            .Take(overflow)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var k in oldest)
            _store.TryRemove(k, out _);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _registry.Unregister(Name);
        _store.Clear();
    }
}
