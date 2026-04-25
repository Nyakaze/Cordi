using System;
using System.Collections.Generic;

namespace Cordi.Configuration;

[Serializable]
public class SlashCommandConfig
{
    public bool Enabled { get; set; } = false;
    public string GuildId { get; set; } = string.Empty;
    public string CommandChannelId { get; set; } = string.Empty;
    public List<CustomSlashCommand> Commands { get; set; } = new();
    public List<CommandGroup> Groups { get; set; } = new();
}

[Serializable]
public class CommandGroup
{
    public string Name { get; set; } = string.Empty;
}

[Serializable]
public class CustomSlashCommand
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GameCommand { get; set; } = string.Empty;
    public bool IsEmote { get; set; } = false;
    public bool IsEnabled { get; set; } = true;
    public string Group { get; set; } = string.Empty;
    public List<SlashCommandParameter> Parameters { get; set; } = new();
}

[Serializable]
public class SlashCommandParameter
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; } = false;
}
