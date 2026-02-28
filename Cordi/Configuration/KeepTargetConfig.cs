using System;

namespace Cordi.Configuration;

[Serializable]
public class KeepTargetConfig
{
    public bool Enabled { get; set; } = false;
    public string TargetName { get; set; } = string.Empty;
}
