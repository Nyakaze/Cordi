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

public sealed partial class UiTheme
{
    private Vector4? accentOverride;

    public Vector4 Accent => accentOverride ?? GlobalAccent;
    public Vector4 AccentHover => Lerp(Accent, Vector4.One, 0.12f);
    public Vector4 AccentSoft => new(Accent.X, Accent.Y, Accent.Z, 0.18f);
    public Vector4 AccentSelected => new(Accent.X, Accent.Y, Accent.Z, 0.28f);
    public Vector4 AccentBorder => new(Accent.X, Accent.Y, Accent.Z, 0.55f);
    public Vector4 SliderGrab => Accent;
    public Vector4 SliderGrabActive => AccentHover;

    public Vector4 AccentText;
    public Vector4 WindowBg;
    public Vector4 WindowBorder;
    public Vector4 TitleBg;
    public Vector4 TitleBgActive;
    public Vector4 CardBg;
    public Vector4 SidebarBg;
    public Vector4 PanelBg;
    public Vector4 RowBg;
    public Vector4 RowHover;
    public Vector4 Border;
    public Vector4 Text;
    public Vector4 MutedText;
    public Vector4 FaintText;
    public Vector4 Hover;
    public Vector4 Active;
    public Vector4 FrameBg;
    public Vector4 FrameBgHover;
    public Vector4 FrameBgActive;
    public Vector4 Tab;
    public Vector4 TabActive;
    public Vector4 TabHovered;

    public static readonly Vector4 ColorSuccess = new(0.24f, 0.00f, 0.65f, 1f);
    public static readonly Vector4 ColorSuccessText = new(0.81f, 0.62f, 1.00f, 1f);
    public static readonly Vector4 ColorDanger = new(0.565f, 0.0f, 0.0f, 1f);
    public static readonly Vector4 ColorDangerText = new(1.0f, 0.4f, 0.4f, 1f);
    public static readonly Vector4 ColorCheckboxOn = new(0.35f, 0.75f, 0.45f, 1f);

    public static readonly Vector4 ColorOnline = new(0.24f, 0.85f, 0.47f, 1f);
    public static readonly Vector4 ColorOffline = new(0.45f, 0.45f, 0.52f, 1f);

    public static readonly Vector4 TileBlue = new(0.35f, 0.51f, 0.98f, 1f);
    public static readonly Vector4 TilePurple = new(0.55f, 0.36f, 0.96f, 1f);
    public static readonly Vector4 TilePink = new(0.93f, 0.35f, 0.71f, 1f);
    public static readonly Vector4 TileAmber = new(0.98f, 0.71f, 0.18f, 1f);
    public static readonly Vector4 TileGreen = new(0.24f, 0.78f, 0.48f, 1f);
    public static readonly Vector4 TileTeal = new(0.20f, 0.76f, 0.78f, 1f);
    public static readonly Vector4 TileRed = new(0.94f, 0.36f, 0.36f, 1f);

    public static float GlobalFontScale = 1.0f;
    public static bool GlobalFontBold = false;
    public static readonly Vector4 DefaultAccent = new(0.486f, 0.227f, 0.929f, 1f);

    public static Vector4 GlobalAccent = DefaultAccent;

    public static readonly (string Name, Vector4 Color)[] AccentPresets =
    {
        ("Violet", new Vector4(0.486f, 0.227f, 0.929f, 1f)),
        ("Indigo", new Vector4(0.310f, 0.275f, 0.898f, 1f)),
        ("Blue", new Vector4(0.145f, 0.514f, 0.918f, 1f)),
        ("Teal", new Vector4(0.078f, 0.722f, 0.651f, 1f)),
        ("Green", new Vector4(0.133f, 0.773f, 0.369f, 1f)),
        ("Amber", new Vector4(0.961f, 0.620f, 0.043f, 1f)),
        ("Rose", new Vector4(0.957f, 0.247f, 0.369f, 1f)),
        ("Pink", new Vector4(0.925f, 0.282f, 0.600f, 1f)),
    };

    public static Vector4 ParseAccent(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return DefaultAccent;

        try
        {
            var parsed = ColorConvertor.ToVector4(hex);
            return new Vector4(parsed.X, parsed.Y, parsed.Z, 1f);
        }
        catch (Exception)
        {
            return DefaultAccent;
        }
    }

    public static string ToAccentHex(Vector4 color)
    {
        static int Channel(float value) => Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
        return $"#{Channel(color.X):X2}{Channel(color.Y):X2}{Channel(color.Z):X2}";
    }

    public float RadiusBase = 8f;
    public float PadBase = 10f;
    public float GapBase = 8f;

    private const float PadYRatio = 0.9f;
    private const float ActionsColumnWidth = 80f;
    private const float CollapsableHeaderHeight = 35f;

    public const float SidebarWidth = 210f;
    public const float NavItemHeight = 36f;
    public const float SettingsRowHeight = 56f;
    public const float IconTileSize = 34f;
    public const float StatChipHeight = 56f;
    public const float ControlHeight = 34f;
    public const float ToggleWidth = 42f;
    public const float ToggleHeight = 22f;
    public const float TooltipCursorOffset = 24f;
    public const float ActionButtonSize = 34f;
    public const float EmojiPickerWidth = 420f;
    public const float EmojiPickerHeight = 470f;
    public const float EmojiPickerMinWidth = 260f;
    public const float EmojiPickerMinHeight = 200f;
    public const float EmojiPickerGripSize = 14f;
    public const float ConfirmDialogWidth = 380f;

    public float Radius(float mul = 1f) => RadiusBase * ImGuiHelpers.GlobalScale * mul;
    public float PadX(float mul = 1f) => PadBase * ImGuiHelpers.GlobalScale * mul;
    public float PadY(float mul = 1f) => (PadBase * PadYRatio) * ImGuiHelpers.GlobalScale * mul;
    public float Gap(float mul = 1f) => GapBase * ImGuiHelpers.GlobalScale * mul;
    public float Scaled(float value) => value * ImGuiHelpers.GlobalScale * GlobalFontScale;
    public float ScaledActionsWidth => ActionsColumnWidth * ImGuiHelpers.GlobalScale;

    public UiTheme(Vector4? accentOverride = null)
    {
        ApplyPreset(accentOverride);
    }

    public void ApplyPreset(Vector4? accentOverride = null)
    {
        WindowBg = new(0.047f, 0.035f, 0.071f, 1f);
        SidebarBg = new(0.035f, 0.027f, 0.055f, 1f);
        PanelBg = new(0.071f, 0.055f, 0.110f, 1f);
        CardBg = new(0.098f, 0.078f, 0.145f, 1f);
        RowBg = new(0.110f, 0.086f, 0.161f, 1f);
        RowHover = new(0.141f, 0.114f, 0.204f, 1f);
        Border = new(1f, 1f, 1f, 0.055f);
        WindowBorder = new(1f, 1f, 1f, 0.075f);
        TitleBg = new(0.055f, 0.043f, 0.086f, 1f);
        TitleBgActive = new(0.071f, 0.055f, 0.110f, 1f);
        Text = new(0.937f, 0.933f, 0.961f, 1f);
        MutedText = new(0.588f, 0.573f, 0.651f, 1f);
        FaintText = new(0.427f, 0.412f, 0.494f, 1f);
        Hover = new(1f, 1f, 1f, 0.04f);
        Active = new(1f, 1f, 1f, 0.08f);
        FrameBg = new(0.129f, 0.106f, 0.192f, 1f);
        FrameBgHover = new(0.161f, 0.133f, 0.235f, 1f);
        FrameBgActive = new(0.192f, 0.157f, 0.278f, 1f);
        Tab = new(0.098f, 0.078f, 0.145f, 1f);
        TabActive = new(0.176f, 0.145f, 0.259f, 1f);
        TabHovered = new(0.141f, 0.114f, 0.204f, 1f);
        this.accentOverride = accentOverride;
        AccentText = new(1f, 1f, 1f, 1f);
    }
}
