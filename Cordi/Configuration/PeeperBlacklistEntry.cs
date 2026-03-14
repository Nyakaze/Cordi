using System;

namespace Cordi.Configuration;

[Serializable]
public class CordiPeepBlacklistEntry : IBlacklistEntry
{
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public bool DisableSound { get; set; } = false;
    public bool DisableDiscord { get; set; } = false;
}
