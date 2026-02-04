using System;

namespace Cordi.Configuration;

[Serializable]
public class EmoteLogBlacklistEntry
{
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public bool DisableDiscord { get; set; } = false;
}
