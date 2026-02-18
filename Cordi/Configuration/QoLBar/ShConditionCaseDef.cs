using Cordi.Configuration.QoLBar;
using Newtonsoft.Json;

namespace Cordi.Configuration.QoLBar;

public class ShConditionCaseDef
{
    [JsonProperty("value")]
    public string Value { get; set; } = string.Empty;

    [JsonProperty("operator")]
    public ConditionOperator Operator { get; set; } = ConditionOperator.Equals;

    [JsonProperty("override")]
    public ShCfg Override { get; set; } = new();
}
