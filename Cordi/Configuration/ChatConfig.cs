using System;
using System.Collections.Generic;
using Dalamud.Game.Text;

namespace Cordi.Configuration;

[Serializable]
public class ChatConfig
{
    public List<ChannelMapping> Mappings { get; set; } = new();
    public Dictionary<string, string> TellThreadMappings { get; set; } = new();
    public Dictionary<string, string> CustomAvatars { get; set; } = new();
}
