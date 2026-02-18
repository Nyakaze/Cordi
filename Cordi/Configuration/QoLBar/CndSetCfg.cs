using System.Collections.Generic;
using Newtonsoft.Json;

namespace Cordi.Configuration.QoLBar;

public class CndSetCfg
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("conditions")]
    public List<CndCfg> Conditions { get; set; } = new();
}
