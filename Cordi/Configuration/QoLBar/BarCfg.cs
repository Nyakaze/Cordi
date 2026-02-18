using System.Collections.Generic;
using Newtonsoft.Json;

namespace Cordi.Configuration.QoLBar;

public enum BarDock
{
    Top,
    Right,
    Bottom,
    Left,
    Undocked
}

public enum BarAlign
{
    LeftOrTop,
    Center,
    RightOrBottom
}

public enum BarVisibility
{
    Slide,
    Immediate,
    Always
}

public class BarCfg
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("hidden")]
    public bool Hidden { get; set; } = false;

    [JsonProperty("dockSide")]
    public BarDock DockSide { get; set; } = BarDock.Bottom;

    [JsonProperty("alignment")]
    public BarAlign Alignment { get; set; } = BarAlign.Center;

    [JsonProperty("visibility")]
    public BarVisibility Visibility { get; set; } = BarVisibility.Slide;

    [JsonProperty("hint")]
    public bool Hint { get; set; } = true;

    [JsonProperty("buttonWidth")]
    public float ButtonWidth { get; set; } = 100f;

    [JsonProperty("buttonHeight")]
    public float ButtonHeight { get; set; } = 0;

    [JsonProperty("columns")]
    public int Columns { get; set; } = 0;

    [JsonProperty("scale")]
    public float Scale { get; set; } = 1.0f;

    [JsonProperty("fontScale")]
    public float FontScale { get; set; } = 1.0f;

    [JsonProperty("spacing")]
    public float[] Spacing { get; set; } = { 4, 4 };

    [JsonProperty("position")]
    public float[] Position { get; set; } = { 0.5f, 0 };

    [JsonProperty("lockedPosition")]
    public bool LockedPosition { get; set; } = false;

    [JsonProperty("noBackground")]
    public bool NoBackground { get; set; } = false;

    [JsonProperty("opacity")]
    public float Opacity { get; set; } = 1.0f;

    [JsonProperty("cornerRadius")]
    public float CornerRadius { get; set; } = 8.0f;

    [JsonProperty("clickThrough")]
    public bool ClickThrough { get; set; } = false;

    [JsonProperty("editing")]
    public bool Editing { get; set; } = false;

    [JsonProperty("conditionSet")]
    public int ConditionSet { get; set; } = -1;

    [JsonProperty("hotkey")]
    public int Hotkey { get; set; } = 0;

    [JsonProperty("revealAreaScale")]
    public float RevealAreaScale { get; set; } = 1.0f;

    [JsonProperty("shortcuts")]
    public List<ShCfg> ShortcutList { get; set; } = new();
}
