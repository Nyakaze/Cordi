using Newtonsoft.Json;

namespace Cordi.Configuration.QoLBar;

public class DynamicVarEntry
{
    /// <summary>User-chosen variable name written into VariableService each tick, e.g. "job".</summary>
    [JsonProperty("name")]
    public string VariableName { get; set; } = string.Empty;

    /// <summary>Which game-state value to read.</summary>
    [JsonProperty("source")]
    public DynamicVarSource Source { get; set; } = DynamicVarSource.JobAbbr;

    /// <summary>Whether this entry is active.</summary>
    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;
}
