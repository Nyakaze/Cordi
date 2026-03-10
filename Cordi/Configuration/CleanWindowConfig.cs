using System;
using Newtonsoft.Json;

namespace Cordi.Configuration;

public enum OutputSize
{
    Native = 0,
    _1440p = 1440,
    _1080p = 1080,
    _720p = 720,
}

[Serializable]
public class CleanWindowConfig
{
    /// <summary>Master toggle for the clean window feature. Not saved to config.</summary>
    [JsonIgnore]
    public bool Enabled { get; set; } = false;

    /// <summary>Whether the clean window shows native game UI elements.</summary>
    public bool ShowGameUI { get; set; } = true;

    /// <summary>X position offset for the output window.</summary>
    public int XPosition { get; set; } = 0;

    /// <summary>Y position offset for the output window.</summary>
    public int YPosition { get; set; } = 0;

    /// <summary>Window z-order: 0 = Normal, 1 = Behind.</summary>
    public int WindowOrder { get; set; } = 1;

    /// <summary>Render target index for the game view WITH UI. Default from MaskedCarnivale: 107.</summary>
    public int RenderIndexWithUI { get; set; } = 107;

    /// <summary>Render target index for the game view WITHOUT UI. Default from MaskedCarnivale: 71.</summary>
    public int RenderIndexWithoutUI { get; set; } = 71;

    /// <summary>Target height for the output window. Native means match game resolution exactly.</summary>
    public OutputSize OutputSize { get; set; } = OutputSize.Native;

    /// <summary>Whether the window is movable and resizable.</summary>
    public bool MovableWindow { get; set; } = false;
}
