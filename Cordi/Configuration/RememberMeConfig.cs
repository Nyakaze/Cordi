using System;
using System.Collections.Generic;

namespace Cordi.Configuration;

[Serializable]
public class RememberMeConfig
{
    public bool Enabled { get; set; } = true;
    public List<RememberedPlayerEntry> RememberedPlayers { get; set; } = new();
}
