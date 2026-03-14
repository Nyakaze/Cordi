using System;

namespace Cordi.Configuration;

[Serializable]
public class CombinedWindowConfig
{
    public bool Enabled { get; set; } = false;
    public bool SwapPanels { get; set; } = false;
    public bool OpenOnLogin { get; set; } = false;
    public bool WindowLocked { get; set; } = false;
    public bool WindowNoResize { get; set; } = false;
    public bool IgnoreEsc { get; set; } = false;
}
