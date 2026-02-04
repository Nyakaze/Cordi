using System;

namespace Cordi.Configuration;

[Serializable]
public class PeeperStats
{
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public long Count { get; set; } = 0;
    public DateTime LastSeen { get; set; }
}
