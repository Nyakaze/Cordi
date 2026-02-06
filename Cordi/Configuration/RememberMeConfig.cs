using System;
using System.Collections.Generic;

namespace Cordi.Configuration;

[Serializable]
public class RememberMeConfig
{
    public bool Enabled { get; set; } = true;
    public bool EnableExamineFeature { get; set; } = false;
    public List<RememberedPlayerEntry> RememberedPlayers { get; set; } = new();
    public List<RememberedPlayerEntry> ExaminedPlayers { get; set; } = new();
}
