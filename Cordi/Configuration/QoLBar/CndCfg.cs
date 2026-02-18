using Newtonsoft.Json;

namespace Cordi.Configuration.QoLBar;

public enum BinaryOperator
{
    AND,
    OR,
    EQUALS,
    XOR
}

public class CndCfg
{
    [JsonProperty("id")]
    public int ID { get; set; } = 0;

    [JsonProperty("arg")]
    public int Arg { get; set; } = 0;

    [JsonProperty("negate")]
    public bool Negate { get; set; } = false;

    [JsonProperty("op")]
    public BinaryOperator Operator { get; set; } = BinaryOperator.AND;
}
