using System;
using System.Collections.Generic;

namespace Cordi.Configuration;

[Serializable]
public class LodestoneConfig
{
    public Dictionary<string, string> CharacterIdCache { get; set; } = new();
}
