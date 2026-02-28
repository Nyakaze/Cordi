using System;

namespace Cordi.Configuration;

[Serializable]
public class NearbyConfig
{
    public bool Enabled { get; set; } = false;
    public bool WindowEnabled { get; set; } = true;
    public bool WindowLocked { get; set; } = false;
    public bool WindowNoResize { get; set; } = false;
    public bool OpenOnLogin { get; set; } = false;
    public bool IgnoreEsc { get; set; } = false;

    // Display Toggles (Similar to Peeper structure)
    public bool ShowDirection { get; set; } = true;
    public bool ShowDistance { get; set; } = true;
    public bool ShowCurrentTarget { get; set; } = true;

    // Feature Behavior Flags
    public bool IncludeSelf { get; set; } = false;
    public bool PrioritizeTargetingMe { get; set; } = true;

    // Sorting option (0 = Distance, 1 = Name)
    public int SortMode { get; set; } = 0;
}
