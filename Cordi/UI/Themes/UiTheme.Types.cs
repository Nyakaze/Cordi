
using System;
using System.Numerics;
using Cordi.Extensions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Themes;
using Cordi.Configuration;
using Dalamud.Interface.Components;

public readonly struct UiCardResult
{
    public bool Clicked { get; init; }
    public bool RightClicked { get; init; }
    public bool ToggleChanged { get; init; }
    public bool TagClicked { get; init; }
    public bool MenuClicked { get; init; }
}
public readonly struct UiRect
{
    public readonly Vector2 Min, Max;
    public Vector2 Size => Max - Min;
    public UiRect(Vector2 min, Vector2 max) { Min = min; Max = max; }
}

public readonly struct UiCardDynResult
{
    public bool Clicked { get; init; }
    public bool RightClicked { get; init; }
    public bool ToggleChanged { get; init; }
    public bool MenuClicked { get; init; }
    public bool TagClicked { get; init; }
}

public struct UiCardSlots
{
    public UiRect Card;
    public UiRect Checkbox;
    public UiRect TitleLeft;
    public UiRect TopRight;
    public UiRect Body;
    public UiRect BottomLeft;
    public UiRect BottomRight;
}

public readonly struct UiBadgeToggleResult
{
    public bool Clicked { get; init; }
    public bool StateChanged { get; init; }
}
