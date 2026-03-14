using System;
using System.Collections.Generic;
using System.Numerics;

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
    public bool IgnoreEsc { get; set; } = false;
    public bool FocusOnHover { get; set; } = false;
    public bool AltClickExamine { get; set; } = false;
    public bool IncludeSelf { get; set; } = false;
    public bool LogParty { get; set; } = true;
    public bool LogAlliance { get; set; } = true;
    public bool LogCombat { get; set; } = true;

    public bool DisableSoundInCombat { get; set; } = false;
    public bool DisableDiscordInCombat { get; set; } = false;


    public bool SoundEnabled { get; set; } = true;
    public float SoundVolume { get; set; } = 1.0f;
    public Guid SoundDevice { get; set; } = Guid.Empty;

    public bool ShowDirection { get; set; } = true;
    public bool ShowDistance { get; set; } = true;
    public bool ShowDirectionInHistory { get; set; } = true;
    public bool ShowDistanceInHistory { get; set; } = true;
    public bool ShowCurrentTarget { get; set; } = true;

    public Vector4 TargetingHighlightColor { get; set; } = new Vector4(1f, 0.5f, 0.5f, 1f);

    public float BackgroundOpacity { get; set; } = 1.0f;
    public bool HideTitleBar { get; set; } = false;
    public bool TextShadow { get; set; } = false;

    public bool ShowTargetingDot { get; set; } = false;
    public Vector4 TargetingDotColor { get; set; } = new Vector4(1f, 0.3f, 0.3f, 1f);
    public float TargetingDotSize { get; set; } = 6f;
    public float TargetingDotYOffset { get; set; } = 2.0f;

    public List<CordiPeepBlacklistEntry> Blacklist { get; set; } = new();
}
