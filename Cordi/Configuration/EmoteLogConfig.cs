using System;
using System.Collections.Generic;

namespace Cordi.Configuration;

[Serializable]
public class EmoteLogConfig
{
    public string ChannelId { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool IncludeSelf { get; set; } = true;
    public bool CollapseDuplicates { get; set; } = false;
    public bool ShowReplyButton { get; set; } = true;
    public bool DiscordEnabled { get; set; } = true;
    public bool DetectWhenClosed { get; set; } = true;
    public bool WindowEnabled { get; set; } = true;
    public bool WindowOpenOnLogin { get; set; } = false;
    public bool WindowLocked { get; set; } = false;
    public bool WindowLockPosition { get; set; } = false;
    public bool WindowLockSize { get; set; } = false;

    public List<EmoteLogBlacklistEntry> Blacklist { get; set; } = new();
}
