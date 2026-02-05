using System;

namespace Cordi.Configuration;

[Serializable]
public class DiscordConfig
{
    public string BotToken { get; set; } = string.Empty;
    public bool BotStarted { get; set; } = false;
    public string DefaultChannelId { get; set; } = string.Empty;
    public bool AllowDiscordCommands { get; set; } = false;
    public string CommandPrefix { get; set; } = "!";
}
