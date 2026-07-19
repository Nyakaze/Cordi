using System;
using System.Collections.Generic;
using System.Numerics;
using Newtonsoft.Json;

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

    public bool DisableSoundInPvP { get; set; } = false;
    public bool DisableDiscordInPvP { get; set; } = false;


    public bool SoundEnabled { get; set; } = true;
    public float SoundVolume { get; set; } = 1.0f;
    public Guid SoundDevice { get; set; } = Guid.Empty;

    public bool ShowDirection { get; set; } = true;
    public bool ShowDistance { get; set; } = true;
    public bool ShowDirectionInHistory { get; set; } = true;
    public bool ShowDistanceInHistory { get; set; } = true;
    public bool ShowCurrentTarget { get; set; } = true;

    public Vector4 TargetingHighlightColor { get; set; } = new Vector4(1f, 0.5f, 0.5f, 1f);

    public bool TargetingGlowEnabled { get; set; } = false;
    public Vector4 TargetingGlowColor { get; set; } = new Vector4(0f, 0f, 0f, 1f);
    public float TargetingGlowThickness { get; set; } = 3f;

    public float BackgroundOpacity { get; set; } = 1.0f;
    public bool HideTitleBar { get; set; } = false;
    public bool TextShadow { get; set; } = false;

    public bool ShowTargetingDot { get; set; } = false;
    public Vector4 TargetingDotColor { get; set; } = new Vector4(1f, 0.3f, 0.3f, 1f);
    public float TargetingDotSize { get; set; } = 6f;
    public float TargetingDotYOffset { get; set; } = 2.0f;

    public bool SkipRepeatedNotifications { get; set; } = false;
    public int RepeatedNotificationsCooldown { get; set; } = 60;

    public bool UnhideFromVisibility { get; set; } = false;
    public bool UnhideVoidedPlayers { get; set; } = false;

    /// <summary>
    /// Feature-level kill switch for the "Unhide lookers from Visibility plugin" option.
    /// The option was removed from the UI while it's not fully functioning, so the auto-unhide
    /// behavior is forced off regardless of the saved <see cref="UnhideFromVisibility"/> value.
    /// Flip to true (and restore the checkbox in TrackerTab) to bring the feature back.
    /// </summary>
    public const bool UnhideFromVisibilityFeatureEnabled = false;

    /// <summary>
    /// Effective auto-unhide state for runtime code — honours both the user's saved toggle and
    /// the <see cref="UnhideFromVisibilityFeatureEnabled"/> kill switch.
    /// </summary>
    [JsonIgnore]
    public bool UnhideFromVisibilityEffective => UnhideFromVisibilityFeatureEnabled && UnhideFromVisibility;

    public List<CordiPeepBlacklistEntry> Blacklist { get; set; } = new();
}
