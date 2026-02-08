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
    public Dictionary<string, ExtraChatConnection> ExtraChatMappings { get; set; } = new();
}

[Serializable]
public class ExtraChatConnection
{
    public string DiscordChannelId { get; set; } = "";
    public int ExtraChatNumber { get; set; } = 0;
    public string? ExtraChatGuid { get; set; }
}
