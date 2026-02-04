using System;
using System.Collections.Generic;

namespace Cordi.Configuration;

[Serializable]
public class CordiPeepConfig
{
    public bool Enabled { get; set; } = false;
    public bool DiscordEnabled { get; set; } = true;
    public bool DetectWhenClosed { get; set; } = true;
    public string SoundPath { get; set; } = string.Empty;
    public string DiscordChannelId { get; set; } = string.Empty;
    public bool WindowEnabled { get; set; } = true;
    public bool OpenOnLogin { get; set; } = false;
    public bool WindowLocked { get; set; } = false;
    public bool WindowNoResize { get; set; } = false;
    public bool FocusOnHover { get; set; } = false;
    public bool AltClickExamine { get; set; } = false;
    public bool IncludeSelf { get; set; } = false;
    public bool LogParty { get; set; } = true;
    public bool LogAlliance { get; set; } = true;
    public bool LogCombat { get; set; } = true;


    public bool SoundEnabled { get; set; } = true;
    public float SoundVolume { get; set; } = 1.0f;
    public Guid SoundDevice { get; set; } = Guid.Empty;

    public List<CordiPeepBlacklistEntry> Blacklist { get; set; } = new();
}
