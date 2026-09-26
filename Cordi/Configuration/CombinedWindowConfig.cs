using System;
using Newtonsoft.Json;

namespace Cordi.Configuration;

public enum OverlaySlot
{
    TopLeft = 0,
    TopRight = 1,
    BottomLeft = 2,
    BottomRight = 3,
}

[Serializable]
public class CombinedWindowConfig : IWindowChromeConfig
{
    bool IWindowChromeConfig.LockPosition => WindowLocked;
    bool IWindowChromeConfig.LockSize => WindowNoResize;

    public bool Enabled { get; set; } = true;
    public OverlaySlot EmoteLogSlot { get; set; } = OverlaySlot.TopLeft;
    public OverlaySlot PeeperSlot { get; set; } = OverlaySlot.TopRight;
    public bool OpenOnLogin { get; set; } = false;
    public bool WindowLocked { get; set; } = false;
    public bool WindowNoResize { get; set; } = false;
    public bool IgnoreEsc { get; set; } = false;

    public float BackgroundOpacity { get; set; } = 1.0f;
    public bool HideTitleBar { get; set; } = false;
    public bool TextShadow { get; set; } = false;

    [JsonProperty("SwapPanels", NullValueHandling = NullValueHandling.Ignore)]
    public bool? LegacySwapPanels { get; set; }

    public static int RowOf(OverlaySlot slot) => (int)slot / 2;

    public static int ColumnOf(OverlaySlot slot) => (int)slot % 2;
}
