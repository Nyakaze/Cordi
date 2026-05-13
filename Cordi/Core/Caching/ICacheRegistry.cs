using System.Collections.Generic;

namespace Cordi.Core.Caching;

public interface ICacheRegistry
{
    void Register(ICacheStats cache);
    void Unregister(string name);
    IReadOnlyList<ICacheStats> All { get; }
}
