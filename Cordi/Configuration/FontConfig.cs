using System;

namespace Cordi.Configuration;

[Serializable]
public class FontConfig
{
    public float GlobalScale { get; set; } = 1.0f;
    public bool Bold { get; set; } = false;
}
