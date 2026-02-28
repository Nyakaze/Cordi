using System.Collections.Generic;
using Newtonsoft.Json;

namespace Cordi.Configuration.QoLBar;

public enum ShortcutType
{
    Command,
    Category,
    Spacer
}

public enum CategoryAnchor
{
    Auto,
    TopRight,
    TopLeft,
    TopCenter,
    BottomRight,
    BottomLeft,
    BottomCenter,
    RightTop,
    RightBottom,
    RightCenter,
    LeftTop,
    LeftBottom,
    LeftCenter
}

public class ShCfg
{
    [JsonProperty("name")]
    public string? Name { get; set; } = null;

    [JsonProperty("type")]
    public ShortcutType Type { get; set; } = ShortcutType.Command;

    [JsonProperty("command")]
    public string? Command { get; set; } = null;

    [JsonProperty("color")]
    public uint Color { get; set; } = 0xFFFFFFFF;

    [JsonProperty("iconId")]
    public int IconId { get; set; } = 0;

    [JsonProperty("customIconPath")]
    public string? CustomIconPath { get; set; } = null;

    [JsonProperty("iconOnly")]
    public bool IconOnly { get; set; } = false;

    [JsonProperty("buttonSize")]
    public float ButtonSize { get; set; } = 0;

    [JsonProperty("buttonHeight")]
    public float ButtonHeight { get; set; } = 0;

    [JsonProperty("opacity")]
    public float Opacity { get; set; } = 1.0f;

    [JsonProperty("spacing")]
    public float[] Spacing { get; set; } = { 0, 0 };

    [JsonProperty("useFrame")]
    public bool UseFrame { get; set; } = false;

    [JsonProperty("clickThrough")]
    public bool ClickThrough { get; set; } = false;

    [JsonProperty("tooltip")]
    public string? Tooltip { get; set; } = null;

    [JsonProperty("cornerRadius")]
    public float CornerRadius { get; set; } = 0.0f;

    [JsonProperty("hoverOpen")]
    public bool HoverOpen { get; set; } = false;

    [JsonProperty("colorAnimation")]
    public int ColorAnimation { get; set; } = 0;

    [JsonProperty("iconZoom")]
    public float IconZoom { get; set; } = 1.0f;

    [JsonProperty("iconOffset")]
    public float[] IconOffset { get; set; } = { 0, 0 };

    [JsonProperty("iconRotation")]
    public float IconRotation { get; set; } = 0;

    [JsonProperty("cooldownAction")]
    public int CooldownAction { get; set; } = 0;

    [JsonProperty("cooldownStyle")]
    public int CooldownStyle { get; set; } = 0;

    [JsonProperty("hotkey")]
    public int Hotkey { get; set; } = 0;

    [JsonProperty("hotkeyPassToGame")]
    public bool HotkeyPassToGame { get; set; } = false;

    [JsonProperty("categoryNoBackground")]
    public bool CategoryNoBackground { get; set; } = false;

    [JsonProperty("categoryColumns")]
    public int CategoryColumns { get; set; } = 0;

    [JsonProperty("categoryFontScale")]
    public float CategoryFontScale { get; set; } = 1.0f;

    [JsonProperty("_i")]
    public int _i { get; set; } = 0;

    [JsonProperty("closeOnAction")]
    public bool CloseOnAction { get; set; } = false;

    [JsonProperty("subList")]
    public List<ShCfg> SubList { get; set; } = new();

    [JsonProperty("categoryAnchor")]
    public CategoryAnchor CategoryAnchor { get; set; } = CategoryAnchor.Auto;

    [JsonProperty("conditions")]
    public List<ShCondition> Conditions { get; set; } = new();

    public void CopyFrom(ShCfg source)
    {
        Name = source.Name;
        Type = source.Type;
        Command = source.Command;
        Color = source.Color;
        IconId = source.IconId;
        CustomIconPath = source.CustomIconPath;
        IconOnly = source.IconOnly;
        ButtonSize = source.ButtonSize;
        ButtonHeight = source.ButtonHeight;
        Opacity = source.Opacity;
        Spacing = source.Spacing;
        UseFrame = source.UseFrame;
        ClickThrough = source.ClickThrough;
        Tooltip = source.Tooltip;
        CornerRadius = source.CornerRadius;
        HoverOpen = source.HoverOpen;
        ColorAnimation = source.ColorAnimation;
        IconZoom = source.IconZoom;
        IconOffset = source.IconOffset;
        IconRotation = source.IconRotation;
        CooldownAction = source.CooldownAction;
        CooldownStyle = source.CooldownStyle;
        Hotkey = source.Hotkey;
        HotkeyPassToGame = source.HotkeyPassToGame;
        CategoryNoBackground = source.CategoryNoBackground;
        CategoryColumns = source.CategoryColumns;
        CategoryFontScale = source.CategoryFontScale;
        _i = source._i;
        CloseOnAction = source.CloseOnAction;
        CategoryAnchor = source.CategoryAnchor;
        SubList = source.SubList;
        Conditions = source.Conditions;
    }

    public ShCfg Clone() => (ShCfg)MemberwiseClone();
}
