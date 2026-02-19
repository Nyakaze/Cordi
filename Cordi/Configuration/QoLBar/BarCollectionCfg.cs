using System;
using Newtonsoft.Json;

namespace Cordi.Configuration.QoLBar;

public class BarCollectionCfg
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("name")]
    public string Name { get; set; } = "Collection";

    /// <summary>Whether the collection is collapsed in the settings UI.</summary>
    [JsonProperty("collapsed")]
    public bool Collapsed { get; set; } = false;
}
