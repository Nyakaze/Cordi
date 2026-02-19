using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Cordi.Configuration.QoLBar;

public class QoLBarConfig
{
    [JsonProperty("bars")]
    public List<BarCfg> Bars { get; set; } = new();

    [JsonProperty("collections")]
    public List<BarCollectionCfg> Collections { get; set; } = new();

    [JsonProperty("cndSets")]
    public List<CndSetCfg> ConditionSets { get; set; } = new();

    [JsonProperty("cndDefs")]
    public List<ShConditionDefinition> ConditionDefinitions { get; set; } = new();

    [JsonProperty("dynVars")]
    public List<DynamicVarEntry> DynamicVariables { get; set; } = new();

    [JsonProperty("exportOnDelete")]
    public bool ExportOnDelete { get; set; } = true;

    [JsonProperty("useIconFrame")]
    public bool UseIconFrame { get; set; } = false;

    [JsonProperty("alwaysDisplayBars")]
    public bool AlwaysDisplayBars { get; set; } = false;

    [JsonProperty("noConditionCache")]
    public bool NoConditionCache { get; set; } = false;

    [JsonProperty("useHRIcons")]
    public bool UseHRIcons { get; set; } = false;

    [JsonProperty("backupTimer")]
    public int BackupTimer { get; set; } = 10;

    [JsonProperty("fontSize")]
    public float FontSize { get; set; } = 16f;

    [JsonProperty("pieOpacity")]
    public int PieOpacity { get; set; } = 200;

    [JsonProperty("pieAlternateAngle")]
    public bool PieAlternateAngle { get; set; } = false;

    [JsonProperty("piesAlwaysCenter")]
    public bool PiesAlwaysCenter { get; set; } = false;

    [JsonProperty("piesMoveMouse")]
    public bool PiesMoveMouse { get; set; } = false;

    [JsonProperty("piesReturnMouse")]
    public bool PiesReturnMouse { get; set; } = false;

    [JsonProperty("piesReadjustMouse")]
    public bool PiesReadjustMouse { get; set; } = false;

    [JsonProperty("optOutGameUIOffHide")]
    public bool OptOutGameUIOffHide { get; set; } = false;

    [JsonProperty("optOutCutsceneHide")]
    public bool OptOutCutsceneHide { get; set; } = false;

    [JsonProperty("optOutGPoseHide")]
    public bool OptOutGPoseHide { get; set; } = false;

    [JsonIgnore]
    private string configFilePath = string.Empty;

    public void Initialize(string configDir)
    {
        configFilePath = Path.Combine(configDir, "QoLBar.json");
    }

    public void Save()
    {
        if (string.IsNullOrEmpty(configFilePath)) return;
        try
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(configFilePath, json);
        }
        catch (Exception) { }
    }

    public static QoLBarConfig Load(string configDir)
    {
        var path = Path.Combine(configDir, "QoLBar.json");
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var cfg = JsonConvert.DeserializeObject<QoLBarConfig>(json) ?? new QoLBarConfig();
                cfg.Initialize(configDir);
                return cfg;
            }
            catch (Exception) { }
        }

        var newCfg = new QoLBarConfig();
        newCfg.Initialize(configDir);
        return newCfg;
    }
}
