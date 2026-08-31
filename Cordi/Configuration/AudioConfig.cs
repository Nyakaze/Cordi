using System;

namespace Cordi.Configuration;

[Serializable]
public class AudioConfig
{
    public Guid OutputDevice { get; set; } = Guid.Empty;
}
