using Cordi.Configuration.QoLBar;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Cordi.Configuration.QoLBar;

public class ShConditionDefinition
{
    [JsonProperty("name")]
    public string Name { get; set; } = "New Condition";

    [JsonProperty("variable")]
    public string Variable { get; set; } = string.Empty;

    [JsonProperty("cases")]
    public List<ShConditionCaseDef> Cases { get; set; } = new();
}
