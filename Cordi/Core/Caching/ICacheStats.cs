using System;

namespace Cordi.Core.Caching;

public interface ICacheStats
{
    string Name { get; }
    int Count { get; }
    int Capacity { get; }
    long Hits { get; }
    long Misses { get; }
    DateTime CreatedAt { get; }
}
