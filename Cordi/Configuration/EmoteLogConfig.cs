using System;
using System.Collections.Generic;

namespace Cordi.Configuration;

[Serializable]
public class EmoteLogConfig : IWindowChromeConfig
{
    bool IWindowChromeConfig.LockPosition => WindowLockPosition;
    bool IWindowChromeConfig.LockSize => WindowLockSize;

    public string ChannelId { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool IncludeSelf { get; set; } = true;
    public bool CollapseDuplicates { get; set; } = false;
    public int MaxEntries { get; set; } = 200;
    public bool ShowReplyButton { get; set; } = true;
    public int EmoteBackHoldSeconds { get; set; } = 8;
    public bool UnhideFromVisibility { get; set; } = true;
    public bool DiscordEnabled { get; set; } = true;
    public bool DetectWhenClosed { get; set; } = true;
    public bool WindowEnabled { get; set; } = true;
    public bool WindowOpenOnLogin { get; set; } = false;
    public bool WindowLockPosition { get; set; } = false;
    public bool WindowLockSize { get; set; } = false;
    public bool IgnoreEsc { get; set; } = false;

    public float BackgroundOpacity { get; set; } = 1.0f;
    public bool HideTitleBar { get; set; } = false;
    public bool TextShadow { get; set; } = false;

    public List<EmoteLogBlacklistEntry> Blacklist { get; set; } = new();
}
