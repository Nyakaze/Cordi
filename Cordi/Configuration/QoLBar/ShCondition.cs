using Newtonsoft.Json;

namespace Cordi.Configuration.QoLBar;

public enum ConditionOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    Contains
}

public class ShCondition
{
    [JsonProperty("id")]
    public string ID { get; set; } = System.Guid.NewGuid().ToString();

    [JsonProperty("conditionId")]
    public string ConditionID { get; set; } = string.Empty;

    [JsonProperty("cases")]
    public System.Collections.Generic.List<ShConditionCase> Cases { get; set; } = new();
}
