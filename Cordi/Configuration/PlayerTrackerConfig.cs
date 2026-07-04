using System;

namespace Cordi.Configuration;

[Serializable]
public class PlayerTrackerConfig
{
    /// <summary>
    /// Opt-in master switch for the Player Tracker. Tracking is disabled by default; the player
    /// must explicitly enable it. While disabled, no observations are recorded, nearby scanning
    /// does not run, and no encounters are stored.
    /// </summary>
    public bool Enabled { get; set; } = false;
}
