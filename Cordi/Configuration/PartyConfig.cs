using System;

namespace Cordi.Configuration;

[Serializable]
public class PartyConfig
{
    public bool Enabled { get; set; } = false;
    public bool DiscordEnabled { get; set; } = true;
    public string DiscordChannelId { get; set; } = string.Empty;

    public bool NotifyJoin { get; set; } = true;
    public bool NotifyLeave { get; set; } = true;
    public bool NotifyFull { get; set; } = false;
    public bool ShowGearLevel { get; set; } = false;
    public bool ExcludeAlliance { get; set; } = false;

    public bool IncludeSelf { get; set; } = false;
    public bool ShowSavageProgress { get; set; } = true;
}
