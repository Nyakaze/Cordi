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
    [JsonProperty("conditionName")]
    public string ConditionName { get; set; } = string.Empty;
}
