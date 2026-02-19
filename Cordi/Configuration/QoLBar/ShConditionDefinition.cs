using Cordi.Configuration.QoLBar;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Cordi.Configuration.QoLBar;

public class ShConditionDefinition
{
    [JsonProperty("id")]
    public string ID { get; set; } = System.Guid.NewGuid().ToString();

    [JsonProperty("name")]
    public string Name { get; set; } = "Current Job";

    [JsonProperty("variable")]
    public string Variable { get; set; } = string.Empty;
}
