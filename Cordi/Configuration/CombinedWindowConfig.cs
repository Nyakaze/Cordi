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

    public float BackgroundOpacity { get; set; } = 1.0f;
    public bool HideTitleBar { get; set; } = false;
    public bool TextShadow { get; set; } = false;
}
