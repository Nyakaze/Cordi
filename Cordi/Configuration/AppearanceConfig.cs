using System;

namespace Cordi.Configuration;

[Serializable]
public class AppearanceConfig
{
    public const string DefaultAccentHex = "#7C3AED";

    public string AccentColor { get; set; } = DefaultAccentHex;
}
