using Newtonsoft.Json;

namespace Cordi.Configuration.QoLBar;

public class ShConditionCase
{
    [JsonProperty("op")]
    public ConditionOperator Operator { get; set; } = ConditionOperator.Equals;

    [JsonProperty("value")]
    public string Value { get; set; } = string.Empty;

    [JsonProperty("override")]
    public ShCfg Override { get; set; } = new();
}
