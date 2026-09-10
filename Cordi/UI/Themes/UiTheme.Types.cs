
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

public readonly struct UiSuggestionItem
{
    public required string Label { get; init; }
    public string Detail { get; init; }
    public FontAwesomeIcon Icon { get; init; }
    public Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? Image { get; init; }
}

public readonly struct UiSuggestionHit
{
    public int Hovered { get; init; }
    public int Clicked { get; init; }
}

public enum UiNavBadgePlacement
{
    TopRight,
    MiddleRight,
}

public readonly struct UiNavItem
{
    public required string Label { get; init; }
    public Vector4 Accent { get; init; }
    public bool Active { get; init; }
    public bool Unread { get; init; }
    public bool ShowUnreadDot { get; init; }
    public string BadgeText { get; init; }
    public Vector4 BadgeColor { get; init; }
    public Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? Image { get; init; }
}

public readonly struct UiNavHit
{
    public bool Clicked { get; init; }
    public bool Hovered { get; init; }
}

public enum UiConfirmResult
{
    None,
    Confirmed,
    Cancelled,
}

public readonly struct UiBarButton
{
    public required string Label { get; init; }
    public float Width { get; init; }
    public bool Primary { get; init; }
    public string Tooltip { get; init; }
    public Action? OnClick { get; init; }
}
