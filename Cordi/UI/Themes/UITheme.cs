
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

public sealed class UiTheme
{
    public Vector4 Accent;
    public Vector4 AccentText;
    public Vector4 WindowBg;
    public Vector4 WindowBorder;
    public Vector4 TitleBg;
    public Vector4 TitleBgActive;
    public Vector4 CardBg;
    public Vector4 Text;
    public Vector4 MutedText;
    public Vector4 Hover;
    public Vector4 Active;
    public Vector4 FrameBg;
    public Vector4 FrameBgHover;
    public Vector4 FrameBgActive;
    public Vector4 SliderGrab;
    public Vector4 SliderGrabActive;
    public Vector4 Tab;
    public Vector4 TabActive;
    public Vector4 TabHovered;
    public static readonly Vector4 ColorSuccess = new(0.24f, 0.00f, 0.65f, 1f);
    public static readonly Vector4 ColorSuccessText = new(0.81f, 0.62f, 1.00f, 1f);
    public static readonly Vector4 ColorDanger = new(0.565f, 0.0f, 0.0f, 1f);
    public static readonly Vector4 ColorDangerText = new(1.0f, 0.4f, 0.4f, 1f);
    public static readonly Vector4 ColorCheckboxOn = new(0.35f, 0.75f, 0.45f, 1f);

    public static float GlobalFontScale = 1.0f;
    public static bool GlobalFontBold = false;

    public float RadiusBase = 8f;
    public float PadBase = 10f;
    public float GapBase = 8f;

    private const float PadYRatio = 0.9f;
    private const float ActionsColumnWidth = 80f;
    private const float CollapsableHeaderHeight = 35f;

    public float Radius(float mul = 1f) => RadiusBase * ImGuiHelpers.GlobalScale * mul;
    public float PadX(float mul = 1f) => PadBase * ImGuiHelpers.GlobalScale * mul;
    public float PadY(float mul = 1f) => (PadBase * PadYRatio) * ImGuiHelpers.GlobalScale * mul;
    public float Gap(float mul = 1f) => GapBase * ImGuiHelpers.GlobalScale * mul;
    private float ScaledActionsWidth => ActionsColumnWidth * ImGuiHelpers.GlobalScale;

    public UiTheme(Vector4? accentOverride = null)
    {
        ApplyPreset(accentOverride);
    }

    public void ApplyPreset(Vector4? accentOverride = null)
    {
        WindowBg = new(0.09f, 0.09f, 0.11f, 1f);
        WindowBorder = new(0.22f, 0.22f, 0.26f, 1f);
        TitleBg = new(0.12f, 0.12f, 0.16f, 1f);
        TitleBgActive = new(0.14f, 0.14f, 0.18f, 1f);
        CardBg = new(0.14f, 0.14f, 0.17f, 1f);
        Text = new(0.92f, 0.92f, 0.96f, 1f);
        MutedText = new(0.70f, 0.70f, 0.78f, 1f);
        Hover = new(1f, 1f, 1f, 0.04f);
        Active = new(1f, 1f, 1f, 0.08f);
        FrameBg = new(0.18f, 0.18f, 0.22f, 1f);
        FrameBgHover = new(0.22f, 0.22f, 0.26f, 1f);
        FrameBgActive = new(0.26f, 0.26f, 0.30f, 1f);
        SliderGrab = new(0.54f, 0.64f, 0.98f, 1f);
        SliderGrabActive = new(0.60f, 0.70f, 1.00f, 1f);
        Tab = new(0.15f, 0.15f, 0.19f, 1f);
        TabActive = new(0.23f, 0.23f, 0.28f, 1f);
        TabHovered = new(0.20f, 0.20f, 0.25f, 1f);
        Accent = accentOverride ?? ColorConvertor.ToVector4("#3c00a5");
        AccentText = new(0.08f, 0.08f, 0.10f, 1f);
    }


    private const int WindowColorCount = 4;
    private const int WindowVarCount = 3;

    private IDisposable? _activeWindowColorScope;
    private IDisposable? _activeWindowStyleScope;

    public void ApplyFontScale(float extraMul = 1f)
    {
        ImGui.SetWindowFontScale(GlobalFontScale * extraMul);
    }

    public void PushWindow()
    {
        _activeWindowColorScope = ImRaii.PushColor(ImGuiCol.WindowBg, WindowBg)
            .Push(ImGuiCol.Border, WindowBorder)
            .Push(ImGuiCol.TitleBg, TitleBg)
            .Push(ImGuiCol.TitleBgActive, TitleBgActive);
        _activeWindowStyleScope = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, Radius(1.2f))
            .Push(ImGuiStyleVar.WindowBorderSize, 1f * ImGuiHelpers.GlobalScale)
            .Push(ImGuiStyleVar.WindowPadding, new Vector2(PadX(), PadY()));
    }

    public void PopWindow()
    {
        _activeWindowStyleScope?.Dispose();
        _activeWindowColorScope?.Dispose();
        _activeWindowStyleScope = null;
        _activeWindowColorScope = null;
    }


    private sealed class ActionDisposable : IDisposable
    {
        private readonly Action _onDispose;
        public ActionDisposable(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }

    public IDisposable CardScope(string id, Vector2 minSize = default, bool border = true)
    {
        var avail = ImGui.GetContentRegionAvail();
        if (minSize.X <= 0) minSize.X = avail.X;

        var style = ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, Radius())
            .Push(ImGuiStyleVar.FramePadding, new Vector2(PadX(0.6f), PadY(0.6f)));

        var color = ImRaii.PushColor(ImGuiCol.ChildBg, CardBg);
        if (border)
            color.Push(ImGuiCol.Border, WindowBorder);

        var child = ImRaii.Child(id, minSize, border);
        float gap = Gap(0.25f);

        return new ActionDisposable(() =>
        {
            child.Dispose();
            color.Dispose();
            style.Dispose();
            ImGui.Dummy(new Vector2(0, gap));
        });
    }


    public bool PrimaryButton(string label, Vector2 size = default)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Button, Accent)
            .Push(ImGuiCol.ButtonHovered, Lerp(Accent, Vector4.One, 0.08f))
            .Push(ImGuiCol.ButtonActive, Lerp(Accent, Vector4.Zero, 0.10f));
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, Radius());
        var clicked = Button(label, size);
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return clicked;
    }

    public bool SecondaryButton(string label, Vector2 size = default)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Button, FrameBg)
            .Push(ImGuiCol.ButtonHovered, FrameBgHover)
            .Push(ImGuiCol.ButtonActive, FrameBgActive);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, Radius());
        var clicked = Button(label, size);
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return clicked;
    }

    public bool Button(string label, Vector2 size = default)
    {
        var ret = ImGui.Button(label, size);
        HoverHandIfItem();
        return ret;
    }
    public bool Button(string label, string tooltip, Vector2 size = default)
    {
        var ret = ImGui.Button(label, size);
        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }
        HoverHandIfItem();
        return ret;
    }

    public bool Checkbox(string label, ref bool v)
    {
        var ret = ImGui.Checkbox(label, ref v);
        HoverHandIfItem();
        return ret;
    }
    public bool Checkbox(string label, string tooltip,ref bool v)
    {
        var ret = ImGui.Checkbox(label, ref v);
        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }
        HoverHandIfItem();
        return ret;
    }

    public bool InvisibleButton(string id, Vector2 size = default)
    {
        var ret = ImGui.InvisibleButton(id, size);
        HoverHandIfItem();
        return ret;
    }

    public bool EnumCombo<T>(string id, ref T value) where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        int currentIndex = Array.IndexOf(values, value);
        if (currentIndex < 0) currentIndex = 0;

        string[] names = Enum.GetNames<T>();

        bool changed = false;
        if (ImGui.Combo(id, ref currentIndex, names, names.Length))
        {
            value = values[currentIndex];
            changed = true;
        }
        HoverHandIfItem();
        return changed;
    }
    
    public bool EnumCombo<T>(string id, string tooltip ,ref T value) where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        int currentIndex = Array.IndexOf(values, value);
        if (currentIndex < 0) currentIndex = 0;

        string[] names = Enum.GetNames<T>();

        bool changed = false;
        if (ImGui.Combo(id, ref currentIndex, names, names.Length))
        {
            value = values[currentIndex];
            changed = true;
        }
        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }
        HoverHandIfItem();
        return changed;
    }

    public bool IconButton(string id, FontAwesomeIcon icon, string tooltip = "")
    {
        bool clicked = ImGuiComponents.IconButton(id, icon);

        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }
        HoverHandIfItem();

        return clicked;
    }

    public bool SuccessIconButton(string id, FontAwesomeIcon icon, string tooltip = "")
    {
        using var color = ImRaii.PushColor(ImGuiCol.Button, Accent)
            .Push(ImGuiCol.ButtonHovered, Lerp(Accent, Vector4.One, 0.08f))
            .Push(ImGuiCol.ButtonActive, Lerp(Accent, Vector4.Zero, 0.10f));
        var clicked = IconButton(id, icon, tooltip);
        return clicked;
    }

    public bool SecondaryIconButton(string id, FontAwesomeIcon icon, string tooltip = "")
    {
        using var color = ImRaii.PushColor(ImGuiCol.Button, FrameBg)
            .Push(ImGuiCol.ButtonHovered, FrameBgHover)
            .Push(ImGuiCol.ButtonActive, FrameBgActive);
        var clicked = IconButton(id, icon, tooltip);
        return clicked;
    }

    public bool DangerIconButton(string id, FontAwesomeIcon icon, string tooltip = "")
    {
        using var color = ImRaii.PushColor(ImGuiCol.Button, ColorDanger)
            .Push(ImGuiCol.ButtonHovered, Lerp(ColorDanger, Vector4.One, 0.08f))
            .Push(ImGuiCol.ButtonActive, Lerp(ColorDanger, Vector4.Zero, 0.10f));
        var clicked = IconButton(id, icon, tooltip);
        return clicked;
    }

    public bool IconToggleButton(string id, ref bool state, float size = 28f)
    {
        float scale = ImGuiHelpers.GlobalScale;
        var s = new Vector2(size * scale, size * scale);


        var colText = new Vector4(1f, 1f, 1f, 1f);

        bool pressed = Button(id, s);
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);


        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var center = (pos + max) * 0.5f;

        if (!state)
        {
            var a = center + new Vector2(-4f * scale, -6f * scale);
            var b = center + new Vector2(-4f * scale, 6f * scale);
            var c = center + new Vector2(6f * scale, 0f * scale);
            draw.AddTriangleFilled(a, b, c, ImGui.GetColorU32(colText));
        }
        else
        {
            float r = 6f * scale;
            draw.AddRectFilled(center - new Vector2(r, r), center + new Vector2(r, r), ImGui.GetColorU32(colText), 2f * scale);
        }

        if (pressed)
            state = !state;

        return pressed;
    }


    private IDisposable? _activeInputColorScope;
    private IDisposable? _activeInputStyleScope;
    public void PushInputScope()
    {
        _activeInputColorScope = ImRaii.PushColor(ImGuiCol.FrameBg, FrameBg)
            .Push(ImGuiCol.FrameBgHovered, FrameBgHover)
            .Push(ImGuiCol.FrameBgActive, FrameBgActive)
            .Push(ImGuiCol.SliderGrab, SliderGrab)
            .Push(ImGuiCol.SliderGrabActive, SliderGrabActive);
        _activeInputStyleScope = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, Radius())
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(Gap(), Gap(0.6f)));
    }

    public void PopInputScope()
    {
        _activeInputStyleScope?.Dispose();
        _activeInputColorScope?.Dispose();
        _activeInputStyleScope = null;
        _activeInputColorScope = null;
    }


    public void Badge(ReadOnlySpan<char> text, Vector4? bg = null, Vector4? fg = null)
    {
        var bgCol = bg ?? Accent;
        var fgCol = fg ?? AccentText;

        var label = text.ToString();
        var padding = new Vector2(PadX(0.5f), PadY(0.3f));
        var size = ImGui.CalcTextSize(label) + padding * 2f;

        var p = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();

        dl.AddRectFilled(p, p + size, ImGui.GetColorU32(bgCol), Radius(0.8f));
        ImGui.SetCursorScreenPos(p + padding);
        var backup = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        using (ImRaii.PushColor(ImGuiCol.Text, fgCol))
        {
            ImGui.TextUnformatted(label);
        }
        ImGui.SetCursorScreenPos(p + size + new Vector2(Gap(0.5f), 0));
    }

    public UiBadgeToggleResult BadgeToggle(
    string id,
    ref bool state,
    ReadOnlySpan<char> label,
    float height = 28f,
    bool iconOnRight = true,
    Vector4? bgOn = null,
    Vector4? bgOff = null,
    Vector4? fg = null,
    float sidePaddingMul = 0.8f
    )
    {
        float scale = ImGuiHelpers.GlobalScale;

        var _bgOn = bgOn ?? new Vector4(0.80f, 0.30f, 0.28f, 1f);
        var _bgOff = bgOff ?? new Vector4(0.30f, 0.78f, 0.40f, 1f);
        var _fg = fg ?? new Vector4(1f, 1f, 1f, 1f);

        float hPx = height * scale;
        float padX = PadX(sidePaddingMul);
        float padY = MathF.Max((hPx - ImGui.GetTextLineHeight()) * 0.5f, PadY(0.3f));
        float gap = Gap(0.5f);
        float iconBox = hPx - 2f * (PadY(0.25f));
        iconBox = MathF.Max(iconBox, 14f * scale);

        string text = label.ToString();
        var textSize = ImGui.CalcTextSize(text);

        float wPx = padX + (iconBox + gap) + textSize.X + padX;

        var p = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var rMin = p;
        var rMax = p + new Vector2(wPx, hPx);
        var rectSize = rMax - rMin;

        ImGui.InvisibleButton("##" + id, rectSize);
        ImGui.SetItemAllowOverlap();
        bool pressed = ImGui.IsItemClicked(ImGuiMouseButton.Left);

        var bg = state ? _bgOn : _bgOff;
        dl.AddRectFilled(rMin, rMax, ImGui.GetColorU32(bg), Radius(0.9f));
        dl.AddRect(rMin, rMax, ImGui.GetColorU32(WindowBorder), Radius(0.9f));



        Vector2 iconMin, iconMax, textPos;

        if (iconOnRight)
        {
            textPos = new Vector2(rMin.X + padX, rMin.Y + (hPx - textSize.Y) * 0.5f);
            iconMin = new Vector2(rMax.X - padX - iconBox, rMin.Y + (hPx - iconBox) * 0.5f);
        }
        else
        {
            iconMin = new Vector2(rMin.X + padX, rMin.Y + (hPx - iconBox) * 0.5f);
            textPos = new Vector2(iconMin.X + iconBox + gap, rMin.Y + (hPx - textSize.Y) * 0.5f);
        }
        iconMax = iconMin + new Vector2(iconBox, iconBox);

        using (ImRaii.PushColor(ImGuiCol.Text, _fg))
        {
            ImGui.SetCursorScreenPos(textPos);
            ImGui.TextUnformatted(text);
        }

        var center = (iconMin + iconMax) * 0.5f;

        if (!state)
        {
            var a = center + new Vector2(-iconBox * 0.25f, -iconBox * 0.35f);
            var b = center + new Vector2(-iconBox * 0.25f, iconBox * 0.35f);
            var c = center + new Vector2(iconBox * 0.32f, 0f);
            dl.AddTriangleFilled(a, b, c, ImGui.GetColorU32(_fg));
        }
        else
        {
            float r = iconBox * 0.35f;
            dl.AddRectFilled(center - new Vector2(r, r), center + new Vector2(r, r), ImGui.GetColorU32(_fg), 2f * scale);
        }


        bool changed = false;
        if (pressed) { state = !state; changed = true; }

        ImGui.SetCursorScreenPos(new Vector2(rMax.X + Gap(0.5f), rMin.Y));

        return new UiBadgeToggleResult
        {
            Clicked = pressed,
            StateChanged = changed
        };
    }


    public bool ToggleSwitch(string id, ref bool value, float widthMul = 2.2f)
    {
        var h = ImGui.GetFrameHeight();
        var w = MathF.Max(h * widthMul, h * 1.8f);
        var r = h * 0.5f;

        var p = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();

        var bgOn = Accent;
        var bgOff = FrameBg;
        var colTrack = ImGui.GetColorU32(value ? bgOn : bgOff);
        dl.AddRectFilled(p, p + new Vector2(w, h), colTrack, r);

        var thumbCenterX = value ? (p.X + w - r) : (p.X + r);
        var thumbCol = ImGui.GetColorU32(new Vector4(1, 1, 1, 1));
        dl.AddCircleFilled(new Vector2(thumbCenterX, p.Y + r), r - 3f * ImGuiHelpers.GlobalScale, thumbCol);

        ImGui.InvisibleButton(id, new Vector2(w, h));
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var changed = false;
        if (ImGui.IsItemClicked())
        {
            value = !value;
            changed = true;
        }

        if (ImGui.IsItemFocused() && ImGui.IsKeyPressed(ImGuiKey.Space))
        {
            value = !value;
            changed = true;
        }

        return changed;
    }

    private Vector4 CardBgOr(Vector4? fallback = null) => CardBg != default ? CardBg : (fallback ?? new Vector4(0.11f, 0.12f, 0.14f, 1f));

    private Vector4 WindowBorderOr() => WindowBorder.W > 0 ? WindowBorder : new Vector4(0.23f, 0.25f, 0.30f, 1f);
    private Vector4 TextOr() => Text.W > 0 ? Text : new Vector4(0.92f, 0.92f, 0.96f, 1f);
    private Vector4 MutedOr() => MutedText.W > 0 ? MutedText : new Vector4(0.70f, 0.72f, 0.78f, 1f);


    public UiCardResult DrawPluginCard(
        string id,
        ref bool enabled,
        string title,
        string description,
        string authorRightAligned,
        string tagLabel = "Combat",
        Vector4? tagBg = null,
        float height = 88f
    )
    {
        float scale = ImGuiHelpers.GlobalScale;
        float pad = PadX(0.9f);
        float gap = Gap(1f);
        float radius = Radius(1.0f);

        var draw = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var availW = ImGui.GetContentRegionAvail().X;
        var size = new Vector2(availW, height * scale);

        ImGui.InvisibleButton(id, size);
        bool hoveredCard = ImGui.IsItemHovered();
        bool clickedCard = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        bool rightClicked = ImGui.IsItemClicked(ImGuiMouseButton.Right);

        var cardBg = CardBgOr();
        var border = WindowBorderOr();
        draw.AddRectFilled(start, start + size, ImGui.GetColorU32(cardBg), radius);
        draw.AddRect(start, start + size, ImGui.GetColorU32(border), radius);

        if (hoveredCard)
            draw.AddRectFilled(start, start + size, ImGui.GetColorU32(new Vector4(1, 1, 1, 0.05f)), radius);

        float fh = ImGui.GetFrameHeight();
        float cbSize = MathF.Max(fh * 0.95f, 18f * scale);
        var cbPos = new Vector2(start.X + pad, start.Y + (size.Y - cbSize) * 0.5f);
        var cbRect = new Vector2(cbPos.X + cbSize, cbPos.Y + cbSize);

        var colChkOff = Lerp(FrameBg, Vector4.Zero, 0f) is { W: > 0 } ? FrameBg : new Vector4(0.20f, 0.22f, 0.26f, 1f);
        var colChkOn = ColorCheckboxOn;

        draw.AddRectFilled(cbPos, cbRect, ImGui.GetColorU32(enabled ? colChkOn : colChkOff), 4f * scale);
        draw.AddRect(cbPos, cbRect, ImGui.GetColorU32(border), 4f * scale);

        if (enabled)
        {
            var a = new Vector2(cbPos.X + cbSize * 0.25f, cbPos.Y + cbSize * 0.55f);
            var b = new Vector2(cbPos.X + cbSize * 0.45f, cbPos.Y + cbSize * 0.75f);
            var c = new Vector2(cbPos.X + cbSize * 0.78f, cbPos.Y + cbSize * 0.30f);
            draw.AddLine(a, b, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), 2f * scale);
            draw.AddLine(b, c, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), 2f * scale);
        }

        ImGui.SetCursorScreenPos(cbPos);
        ImGui.InvisibleButton(id + "##chk", new Vector2(cbSize, cbSize));
        bool toggleChanged = false;
        if (ImGui.IsItemClicked()) { enabled = !enabled; toggleChanged = true; }

        float textLeft = cbRect.X + pad;
        float textRight = start.X + size.X - pad;
        float textTop = start.Y + pad;
        float textBottom = start.Y + size.Y - pad;

        using (ImRaii.PushColor(ImGuiCol.Text, TextOr()))
        {
            ImGui.SetCursorScreenPos(new Vector2(textLeft, textTop));
            ImGui.TextUnformatted(title);
        }

        var muted = MutedOr();
        var authSize = ImGui.CalcTextSize(authorRightAligned);
        using (ImRaii.PushColor(ImGuiCol.Text, muted))
        {
            ImGui.SetCursorScreenPos(new Vector2(textRight - authSize.X, textTop));
            ImGui.TextUnformatted(authorRightAligned);
        }

        using (ImRaii.PushColor(ImGuiCol.Text, muted))
        {
            ImGui.SetCursorScreenPos(new Vector2(textLeft, textTop + ImGui.GetTextLineHeightWithSpacing()));
            ImGui.PushTextWrapPos(textRight);
            ImGui.TextUnformatted(description);
            ImGui.PopTextWrapPos();
        }

        string tag = tagLabel;
        var tagPad = new Vector2(PadX(0.6f), PadY(0.4f));
        var tagSize = ImGui.CalcTextSize(tag);
        var tagPos = new Vector2(textLeft, textBottom - tagSize.Y - tagPad.Y);
        var tagRect = tagPos + tagSize + tagPad * 2f;

        var tagBgCol = tagBg ?? Accent;
        var tagTextCol = AccentText.W > 0 ? AccentText : new Vector4(1, 1, 1, 1);

        draw.AddRectFilled(tagPos, tagRect, ImGui.GetColorU32(tagBgCol), Radius(0.75f));
        draw.AddRect(tagPos, tagRect, ImGui.GetColorU32(border), Radius(0.75f));

        using (ImRaii.PushColor(ImGuiCol.Text, tagTextCol))
        {
            ImGui.SetCursorScreenPos(tagPos + tagPad);
            ImGui.TextUnformatted(tag);
        }

        ImGui.SetCursorScreenPos(tagPos);
        ImGui.InvisibleButton(id + "##tag", tagRect - tagPos);
        bool tagClicked = ImGui.IsItemClicked();

        float kebabW = 18f * scale;
        float kebabH = 12f * scale;
        var kebabPos = new Vector2(textRight - kebabW, textBottom - kebabH);
        for (int i = 0; i < 3; i++)
        {
            float y = kebabPos.Y + i * (kebabH / 2.5f);
            draw.AddLine(new Vector2(kebabPos.X, y), new Vector2(kebabPos.X + kebabW, y), ImGui.GetColorU32(muted), 2f * scale);
        }
        ImGui.SetCursorScreenPos(new Vector2(kebabPos.X, kebabPos.Y - 2f * scale));
        ImGui.InvisibleButton(id + "##menu", new Vector2(kebabW, kebabH + 4f * scale));
        bool menuClicked = ImGui.IsItemClicked();

        return new UiCardResult
        {
            Clicked = clickedCard,
            RightClicked = rightClicked,
            ToggleChanged = toggleChanged,
            TagClicked = tagClicked,
            MenuClicked = menuClicked
        };
    }

    public UiCardDynResult DrawPluginCardFlex(
        string id,
        ref bool enabled,
        bool showCheckbox,
        string title,
        Action<UiCardSlots, UiTheme>? drawTopRight = null,
        Action<UiCardSlots, UiTheme>? drawBody = null,
        Action<UiCardSlots, UiTheme>? drawBottomLeft = null,
        Action<UiCardSlots, UiTheme>? drawBottomRight = null,
        string defaultDescription = "",
        string defaultTopRightText = "",
        string defaultBottomLeftTag = "",
        float height = 88f
    )
    {
        float scale = ImGuiHelpers.GlobalScale;
        float pad = PadX(0.9f);
        float radius = Radius(1.0f);

        var draw = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var availW = ImGui.GetContentRegionAvail().X;
        var size = new Vector2(availW, height * scale);
        var cardRect = new UiRect(start, start + size);

        ImGui.InvisibleButton(id, size);
        ImGui.SetItemAllowOverlap();
        bool hovered = ImGui.IsItemHovered();
        bool clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        bool rightClicked = ImGui.IsItemClicked(ImGuiMouseButton.Right);

        if (hovered)
            draw.AddRectFilled(cardRect.Min, cardRect.Max, ImGui.GetColorU32(new Vector4(1, 1, 1, 0.05f)), radius);

        float fh = ImGui.GetFrameHeight();
        float cbSize = MathF.Max(fh * 0.95f, 18f * scale);
        var cbMin = new Vector2(cardRect.Min.X + pad, cardRect.Min.Y + (cardRect.Size.Y - cbSize) * 0.5f);
        var cbMax = cbMin + new Vector2(cbSize, cbSize);
        var cbRect = new UiRect(cbMin, cbMax);
        bool toggleChanged = false;

        if (showCheckbox)
        {
            var colChkOff = FrameBg;
            var colChkOn = ColorCheckboxOn;
            draw.AddRectFilled(cbRect.Min, cbRect.Max, ImGui.GetColorU32(enabled ? colChkOn : colChkOff), 4f * scale);
            draw.AddRect(cbRect.Min, cbRect.Max, ImGui.GetColorU32(WindowBorder), 4f * scale);
            if (enabled)
            {
                var a = new Vector2(cbMin.X + cbSize * 0.25f, cbMin.Y + cbSize * 0.55f);
                var b = new Vector2(cbMin.X + cbSize * 0.45f, cbMin.Y + cbSize * 0.75f);
                var c = new Vector2(cbMin.X + cbSize * 0.78f, cbMin.Y + cbSize * 0.30f);
                draw.AddLine(a, b, ImGui.GetColorU32(Vector4.One), 2f * scale);
                draw.AddLine(b, c, ImGui.GetColorU32(Vector4.One), 2f * scale);
            }
            if (InvisibleBtn(id + "##chk", cbRect)) { enabled = !enabled; toggleChanged = true; }
        }

        float textLeft = cbMax.X + pad;
        float textRight = cardRect.Max.X - pad;
        float textTop = cardRect.Min.Y + pad;
        float textBottom = cardRect.Max.Y - pad;

        var titlePos = new Vector2(textLeft, textTop);
        var titleSize = ImGui.CalcTextSize(title);
        var titleRect = new UiRect(titlePos, titlePos + titleSize);

        var topRightRect = new UiRect(
            new Vector2(textRight - 220f * scale, textTop),
            new Vector2(textRight, textTop + ImGui.GetTextLineHeight())
        );

        var bodyRect = new UiRect(
            new Vector2(textLeft, titleRect.Max.Y + 4f * scale),
            new Vector2(textRight, textBottom - 22f * scale)
        );

        var bottomLeftRect = new UiRect(
            new Vector2(textLeft, textBottom - 20f * scale),
            new Vector2(textLeft + 240f * scale, textBottom)
        );

        var bottomRightRect = new UiRect(
            new Vector2(textRight - 80f * scale, textBottom - 18f * scale),
            new Vector2(textRight, textBottom)
        );

        var slots = new UiCardSlots
        {
            Card = cardRect,
            Checkbox = cbRect,
            TitleLeft = titleRect,
            TopRight = topRightRect,
            Body = bodyRect,
            BottomLeft = bottomLeftRect,
            BottomRight = bottomRightRect
        };

        WithCursor(titleRect, () =>
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Text))
            {
                ImGui.TextUnformatted(title);
            }
        });

        if (drawTopRight is not null)
        {
            drawTopRight(slots, this);
        }
        else if (!string.IsNullOrEmpty(defaultTopRightText))
        {
            WithCursor(topRightRect, () =>
            {
                using (ImRaii.PushColor(ImGuiCol.Text, MutedText))
                {
                    var txt = defaultTopRightText;
                    var sz = ImGui.CalcTextSize(txt);
                    ImGui.SetCursorScreenPos(new Vector2(topRightRect.Max.X - sz.X, topRightRect.Min.Y));
                    ImGui.TextUnformatted(txt);
                }
            });
        }

        if (drawBody is not null)
        {
            drawBody(slots, this);
        }
        else if (!string.IsNullOrEmpty(defaultDescription))
        {
            WithCursor(bodyRect, () =>
            {
                using (ImRaii.PushColor(ImGuiCol.Text, MutedText))
                {
                    ImGui.PushTextWrapPos(bodyRect.Max.X);
                    ImGui.TextUnformatted(defaultDescription);
                    ImGui.PopTextWrapPos();
                }
            });
        }

        bool tagClicked = false;
        if (drawBottomLeft is not null)
        {
            drawBottomLeft(slots, this);
        }
        else if (!string.IsNullOrEmpty(defaultBottomLeftTag))
        {
            var tagPad = new Vector2(PadX(0.6f), PadY(0.4f));
            var sz = ImGui.CalcTextSize(defaultBottomLeftTag);
            var min = slots.BottomLeft.Min;
            var rect = new UiRect(min, min + sz + tagPad * 2f);

            draw.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(Accent), Radius(0.75f));
            draw.AddRect(rect.Min, rect.Max, ImGui.GetColorU32(WindowBorder), Radius(0.75f));
            using (ImRaii.PushColor(ImGuiCol.Text, AccentText))
            {
                WithCursor(new UiRect(rect.Min + tagPad, rect.Min + tagPad + sz), () =>
                {
                    ImGui.TextUnformatted(defaultBottomLeftTag);
                });
            }

            if (InvisibleBtn(id + "##tag", rect)) tagClicked = true;
        }

        bool menuClicked = false;
        if (drawBottomRight is not null)
        {
            drawBottomRight(slots, this);
        }
        else
        {
            float kebabW = 18f * scale;
            float kebabH = 12f * scale;
            var basePos = new Vector2(slots.BottomRight.Max.X - kebabW, slots.BottomRight.Min.Y);
            for (int i = 0; i < 3; i++)
            {
                float y = basePos.Y + i * (kebabH / 2.5f);
                draw.AddLine(new Vector2(basePos.X, y),
                             new Vector2(basePos.X + kebabW, y),
                             ImGui.GetColorU32(MutedText), 2f * scale);
            }
            var kebabRect = new UiRect(
                new Vector2(basePos.X, basePos.Y - 2f * scale),
                new Vector2(basePos.X + kebabW, basePos.Y + kebabH + 2f * scale)
            );
            if (InvisibleBtn(id + "##menu", kebabRect)) menuClicked = true;
        }

        return new UiCardDynResult
        {
            Clicked = clicked,
            RightClicked = rightClicked,
            ToggleChanged = toggleChanged,
            TagClicked = tagClicked,
            MenuClicked = menuClicked
        };
    }


    public void DrawPluginCardAuto(
        string id,
        string title,
        Action<float> drawContent,
        ref bool enabled,
        bool showCheckbox = false,
        string? mutedText = null,
        Action? drawHeaderRight = null)
    {
        float scale = ImGuiHelpers.GlobalScale;
        float padX = PadX(0.9f);
        float padY = PadY(0.9f);
        float radius = Radius(1.0f);

        var draw = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var availW = ImGui.GetContentRegionAvail().X;

        draw.ChannelsSplit(2);
        draw.ChannelsSetCurrent(1);

        using (var group = ImRaii.Group())
        {
            ImGui.Dummy(new Vector2(0, padY));

            float contentStartX = padX;

            if (showCheckbox)
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padX);
                bool chk = enabled;
                if (ImGui.Checkbox($"##chk_{id}", ref chk))
                {
                    enabled = chk;
                }

                ImGui.SameLine();
                contentStartX = 0;
                HoverHandIfItem();
            }
            else
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padX);
            }

            using (ImRaii.PushColor(ImGuiCol.Text, Text))
            {
                ImGui.TextUnformatted(title);
            }

            if (!string.IsNullOrEmpty(mutedText))
            {
                ImGui.SameLine();
                ImGui.TextColored(MutedText, mutedText);
            }

            if (drawHeaderRight != null)
            {
                ImGui.SameLine();
                float reservedWidth = 10f * scale; // Reduced to fit "((?))" icon snugly
                float headerRightCursorX = (startPos.X + availW - padX) - reservedWidth;
                if (headerRightCursorX < ImGui.GetCursorPosX()) headerRightCursorX = ImGui.GetCursorPosX() + 10f; // Prevent overlap
                ImGui.SetCursorScreenPos(new Vector2(headerRightCursorX, ImGui.GetCursorScreenPos().Y));

                drawHeaderRight();
                ImGui.NewLine(); // Ensure subsequent content starts on a new line
            }

            SpacerY(0.5f);

            using (ImRaii.PushIndent(padX))
            {
                float innerWidth = availW - (padX * 2);
                using (ImRaii.ItemWidth(innerWidth))
                {
                    drawContent(innerWidth);
                }
            }

            ImGui.Dummy(new Vector2(0, padY));
        }

        var itemMin = ImGui.GetItemRectMin();
        var itemMax = ImGui.GetItemRectMax();


        float totalH = itemMax.Y - startPos.Y;

        var endPos = new Vector2(startPos.X + availW, itemMax.Y);

        draw.ChannelsSetCurrent(0);


        draw.AddRectFilled(startPos, endPos, ImGui.GetColorU32(CardBg), radius);
        draw.AddRect(startPos, endPos, ImGui.GetColorU32(WindowBorder), radius);


        if (ImGui.IsMouseHoveringRect(startPos, endPos))
        {
            draw.AddRectFilled(startPos, endPos, ImGui.GetColorU32(new Vector4(1, 1, 1, 0.05f)), radius);
        }

        draw.ChannelsMerge();


        ImGui.SetCursorScreenPos(new Vector2(startPos.X, endPos.Y));
    }


    public void MutedLabel(string text)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, MutedText))
        {
            ImGui.TextUnformatted(text);
        }
    }
    public void SpacerY(float mul = 1f) => ImGui.Dummy(new Vector2(0, Gap(mul)));
    public void SpacerX(float mul = 1f) => ImGui.Dummy(new Vector2(Gap(mul), 0));
    public void SameLineGap(float mul = 1f) { ImGui.SameLine(); ImGui.Dummy(new Vector2(Gap(mul), 0)); ImGui.SameLine(); }

    static Vector4 Lerp(in Vector4 a, in Vector4 b, float t)
        => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, a.W + (b.W - a.W) * t);

    private static void WithCursor(UiRect r, Action draw)
    {
        ImGui.SetCursorScreenPos(r.Min);
        draw();
    }
    private static bool InvisibleBtn(string id, UiRect r)
    {
        ImGui.SetCursorScreenPos(r.Min);
        bool clicked = ImGui.InvisibleButton(id, r.Size);
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return clicked;
    }
    public void HoverHandIfItem() { if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand); }

    public void DrawCollapsableCardWithTable<T>(
        string id,
        string title,
        ref bool expanded,
        IEnumerable<T> collection,
        Action<T, int> drawRow,
        string[]? headers = null,
        bool showCount = false,
        int? explicitCount = null,
        Action? setupColumns = null,
        bool showHeaders = false,
        Action<float>? drawFooter = null,
        Action? drawTopContent = null,
        Action? extraRows = null,
        float maxTableHeight = 0,
        bool collapsible = true)
    {
        int count = explicitCount ?? collection.Count();
        title = showCount ? $"{title}: {count}" : title;

        float scale = ImGuiHelpers.GlobalScale;
        float headerHeight = CollapsableHeaderHeight * scale;
        if (headerHeight < ImGui.GetFrameHeightWithSpacing()) headerHeight = ImGui.GetFrameHeightWithSpacing();

        float padX = PadX(0.9f);
        float padY = PadY(0.9f);
        float radius = Radius(1.0f);

        var draw = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var availW = ImGui.GetContentRegionAvail().X;

        draw.ChannelsSplit(2);
        draw.ChannelsSetCurrent(1);

        using (var group = ImRaii.Group())
        {
            float titleCenterY = startPos.Y + (headerHeight - ImGui.GetTextLineHeight()) * 0.5f;
            ImGui.SetCursorScreenPos(new Vector2(startPos.X + padX, titleCenterY));

            using (ImRaii.PushColor(ImGuiCol.Text, Text))
            {
                ImGui.TextUnformatted(title);
            }

            if (collapsible)
            {
                using (ImRaii.PushFont(Dalamud.Interface.UiBuilder.IconFont))
                {
                    string icon = expanded ? FontAwesomeIcon.ChevronUp.ToIconString() : FontAwesomeIcon.ChevronDown.ToIconString();
                    var iconSize = ImGui.CalcTextSize(icon);
                    ImGui.SetCursorScreenPos(new Vector2(startPos.X + availW - padX - iconSize.X, titleCenterY));
                    ImGui.TextUnformatted(icon);
                }

                ImGui.SetCursorScreenPos(startPos);
                if (InvisibleButton($"##{id}HeaderBtn", new Vector2(availW, headerHeight)))
                {
                    expanded = !expanded;
                }
            }
            else
            {
                ImGui.SetCursorScreenPos(new Vector2(startPos.X, startPos.Y + headerHeight));
            }

            if (!collapsible || expanded)
            {
                // Calculate 95% width and centering offset
                float targetWidth = availW * 0.95f;
                float xOffset = (availW - targetWidth) / 2.0f;
                float effectiveStartX = startPos.X + xOffset;

                // Start content Y below header
                ImGui.SetCursorScreenPos(new Vector2(effectiveStartX, startPos.Y + headerHeight + Gap(0.2f)));

                if (count == 0 && drawFooter == null)
                {
                    ImGui.TextUnformatted("No items");
                }
                else
                {
                    if (drawTopContent != null)
                    {
                        ImGui.SetCursorScreenPos(new Vector2(effectiveStartX, ImGui.GetCursorScreenPos().Y));
                        ImGui.SetNextItemWidth(targetWidth);
                        drawTopContent();
                        SpacerY(0.5f);
                    }

                    if (count > 0 || extraRows != null)
                    {
                        ImGui.SetCursorScreenPos(new Vector2(effectiveStartX, ImGui.GetCursorScreenPos().Y));
                        DrawTable(id, collection, drawRow, headers, setupColumns, showHeaders, new Vector2(targetWidth, maxTableHeight), extraRows);
                    }

                    if (drawFooter != null)
                    {
                        SpacerY(0.5f);
                        ImGui.SetCursorScreenPos(new Vector2(effectiveStartX, ImGui.GetCursorScreenPos().Y));
                        drawFooter(targetWidth);
                    }
                }

                ImGui.Dummy(new Vector2(0, padY * 0.5f));
            }
        }
        var itemMin = ImGui.GetItemRectMin();
        var itemMax = ImGui.GetItemRectMax();

        float totalHeight = itemMax.Y - startPos.Y;
        if (totalHeight < headerHeight) totalHeight = headerHeight;

        var endPos = new Vector2(startPos.X + availW, startPos.Y + totalHeight);
        draw.ChannelsSetCurrent(0);

        draw.AddRectFilled(startPos, endPos, ImGui.GetColorU32(CardBg), radius);
        draw.AddRect(startPos, endPos, ImGui.GetColorU32(WindowBorder), radius);

        var headerRectMax = new Vector2(endPos.X, startPos.Y + headerHeight);
        var mousePos = ImGui.GetMousePos();
        bool headerHovered = mousePos.X >= startPos.X && mousePos.X < endPos.X &&
                             mousePos.Y >= startPos.Y && mousePos.Y < headerRectMax.Y;

        if (headerHovered)
        {
            draw.AddRectFilled(startPos, headerRectMax, ImGui.GetColorU32(new Vector4(1, 1, 1, 0.05f)), radius, ImDrawFlags.RoundCornersTop);
        }

        draw.ChannelsMerge();

        SpacerY(0.5f);
    }

    public void DrawTable<T>(
        string id,
        IEnumerable<T> collection,
        Action<T, int> drawRow,
        string[]? headers = null,
        Action? setupColumns = null,
        bool showHeaders = false,
        Vector2? outerSize = null,
        Action? extraRows = null)
    {
        int columns = headers?.Length ?? 1;
        var flags = ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp;
        if (outerSize != null && outerSize.Value.Y > 0) flags |= ImGuiTableFlags.ScrollY;

        using (var table = ImRaii.Table($"##table_{id}", columns, flags, outerSize ?? Vector2.Zero))
        {
            if (table)
            {
                if (setupColumns != null)
                {
                    setupColumns();
                }
                else if (headers != null)
                {
                    foreach (var h in headers)
                    {
                        ImGui.TableSetupColumn(h);
                    }
                }

                if (showHeaders && (headers != null || setupColumns != null))
                {
                    ImGui.TableHeadersRow();
                }

                // Snapshot collection to avoid modification errors during iteration
                var snapshot = collection.ToList();
                int idx = 0;
                foreach (var item in snapshot)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    drawRow(item, idx++);
                }
                extraRows?.Invoke();
            }
        }
    }

    public void DrawTable<T>(string id, IEnumerable<T> collection, Action<T, int> drawRow, int columns)
    {
        using (var table = ImRaii.Table($"##table_{id}", columns, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp))
        {
            if (table)
            {
                // Snapshot collection here too
                var snapshot = collection.ToList();
                int idx = 0;
                foreach (var item in snapshot)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    drawRow(item, idx++);
                }
            }
        }
    }

    private Dictionary<string, (int Index, string Value)> _stringEditStates = new();

    public void DrawStringTable(
        string id,
        string title,
        ref bool expanded,
        IList<string> list,
        Action onListModified,
        bool allowAdd = true,
        string itemName = "Item")
    {

        var headers = new[] { itemName, "Actions" };
        Action setupCols = () =>
        {
            ImGui.TableSetupColumn(itemName, ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, ScaledActionsWidth);
        };

        var postDrawActions = new List<Action>();

        Action<string, int> drawRow = (item, idx) =>
        {
            var editKey = id;
            bool isEditing = _stringEditStates.TryGetValue(editKey, out var state) && state.Index == idx;

            // Item Column
            if (isEditing)
            {
                float inputWidth = ImGui.GetContentRegionAvail().X;
                ImGui.SetNextItemWidth(inputWidth);

                // Focus newly added items
                if (string.IsNullOrEmpty(item) && ImGui.IsWindowAppearing())
                    ImGui.SetKeyboardFocusHere();

                string editValue = state.Value;
                if (ImGui.InputText($"##edit-{id}-{idx}", ref editValue, 256))
                {
                    _stringEditStates[editKey] = (idx, editValue);
                }
            }
            else
            {
                ImGui.Text(item);
            }

            ImGui.TableNextColumn();

            // Actions Column
            if (isEditing)
            {
                if(SuccessIconButton($"##save-{id}-{idx}", FontAwesomeIcon.Check, tooltip:"Save"))
                {
                    list[idx] = _stringEditStates[editKey].Value;
                    _stringEditStates.Remove(editKey);
                    onListModified();
                }

                ImGui.SameLine();

                if(SecondaryIconButton($"##cancel-{id}-{idx}", FontAwesomeIcon.Times, tooltip:"Cancel"))
                {
                    if (string.IsNullOrEmpty(list[idx]))
                    {
                        postDrawActions.Add(() =>
                        {
                            list.RemoveAt(idx);
                            onListModified();
                        });
                    }
                    _stringEditStates.Remove(editKey);
                }
            }
            else
            {
                
                if(SecondaryIconButton($"##edit-{id}-{idx}", FontAwesomeIcon.Edit, tooltip:"Edit Pattern"))
                {
                    _stringEditStates[editKey] = (idx, item);
                }
                ImGui.SameLine();

                if(DangerIconButton($"##del-{id}-{idx}", FontAwesomeIcon.Trash, tooltip:"Delete Pattern"))
                {
                    postDrawActions.Add(() =>
                    {
                        list.RemoveAt(idx);
                        onListModified();
                        // If we deleted the item being edited, clear state
                        if (_stringEditStates.TryGetValue(editKey, out var s) && s.Index == idx)
                            _stringEditStates.Remove(editKey);
                    });
                }
            }
        };

        Action drawFooter = () =>
        {
            if (allowAdd)
            {
                float avail = ImGui.GetContentRegionAvail().X;
                float width = avail * 0.95f;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((avail - width) * 0.5f));
                if (SecondaryButton($"Add new {itemName}##{id}", new Vector2(width, 0)))
                {
                    list.Add("");
                    _stringEditStates[id] = (list.Count - 1, "");
                    onListModified();
                }

            }
        };

        DrawCollapsableCardWithTable(
            id,
            title,
            ref expanded,
            list,
            drawRow,
            headers,
            setupColumns: setupCols,
            drawFooter: allowAdd ? (w) => drawFooter() : null
        );

        foreach (var action in postDrawActions) action();
    }

    public delegate void DrawDictionaryEditUI(string key, ref string currentValue, Action cancel);

    private Dictionary<string, (string Key, string Value)> _dictEditStates = new();
    private Dictionary<string, (string NewKey, string NewValue)> _dictAddStates = new();

    public void DrawDictionaryTable(
        string id,
        string title,
        ref bool expanded,
        IDictionary<string, string> dictionary,
        Action onModified,
        string[] headers,
        Action? setupColumns = null,
        Func<string, string, string>? getDisplayValue = null,
        DrawDictionaryEditUI? drawEditUI = null,
        Action<float>? drawFooter = null,
        bool allowAdd = false,
        bool collapsible = true)
    {
        var list = dictionary.ToList();
        var postDrawActions = new List<Action>();

        Action<KeyValuePair<string, string>, int> drawRow = (kvp, idx) =>
        {
            var key = kvp.Key;
            var value = kvp.Value;
            var editKey = id;

            bool isEditing = _dictEditStates.TryGetValue(editKey, out var state) && state.Key == key;

            ImGui.AlignTextToFramePadding();
            ImGui.Text(key);

            ImGui.TableNextColumn();

            if (isEditing)
            {
                // If custom edit UI is provided (e.g. for Dropdowns), use it.
                // Otherwise default to InputText.
                // We pass a 'cancel' action to the custom UI if needed.
                string currentEditValue = state.Value;

                if (drawEditUI != null)
                {
                    Action cancelAction = () => { _dictEditStates.Remove(editKey); };
                    drawEditUI(key, ref currentEditValue, cancelAction);

                    if (currentEditValue != state.Value)
                    {
                        _dictEditStates[editKey] = (key, currentEditValue);
                    }
                }
                else
                {
                    float inputWidth = ImGui.GetContentRegionAvail().X;
                    ImGui.SetNextItemWidth(inputWidth);
                    ImGui.InputText($"##edit-{id}-{key}", ref currentEditValue, 512);
                    _dictEditStates[editKey] = (key, currentEditValue);
                }
            }
            else
            {
                string display = getDisplayValue != null ? getDisplayValue(key, value) : value;
                ImGui.AlignTextToFramePadding();
                ImGui.Text(display);
            }

            ImGui.TableNextColumn();

            if (isEditing)
            {
                if (SuccessIconButton($"##save-{id}-{idx}", FontAwesomeIcon.Check, "Save"))
                {
                    dictionary[key] = state.Value;
                    _dictEditStates.Remove(editKey);
                    onModified();
                }

                ImGui.SameLine();

                if (SecondaryIconButton($"##cancel-{id}-{idx}", FontAwesomeIcon.Times, "Cancel"))
                {
                    _dictEditStates.Remove(editKey);
                }
            }
            else
            {
                if (SecondaryIconButton($"##edit-{id}-{idx}", FontAwesomeIcon.Pen, "Edit"))
                {
                    _dictEditStates[editKey] = (key, value);
                    _dictAddStates.Remove(id);
                }

                ImGui.SameLine();

                if (DangerIconButton($"##del-{id}-{idx}", FontAwesomeIcon.Trash, "Delete"))
                {
                    postDrawActions.Add(() =>
                    {
                        dictionary.Remove(key);
                        onModified();
                        if (_dictEditStates.TryGetValue(editKey, out var s) && s.Key == key)
                            _dictEditStates.Remove(editKey);
                    });
                }
            }
        };

        Action<float>? internalFooter = drawFooter;

        if (allowAdd)
        {
            internalFooter = (totalWidth) =>
            {
                if (drawFooter != null) drawFooter(totalWidth);
                float avail = totalWidth;

                if (_dictAddStates.TryGetValue(id, out var addState))
                {
                    float pad = PadX(0.6f);
                    float actionsWidth = ScaledActionsWidth;
                    float inputsTotalWidth = avail - actionsWidth - pad;
                    float keyWidth = inputsTotalWidth * 0.35f;
                    float valWidth = inputsTotalWidth - keyWidth - Gap();

                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + pad);

                    string nKey = addState.NewKey;
                    string nVal = addState.NewValue;

                    using (ImRaii.ItemWidth(keyWidth))
                    {
                        ImGui.InputTextWithHint($"##add-key-{id}", headers.Length > 0 ? headers[0] : "Key", ref nKey, 128);
                    }

                    ImGui.SameLine();
                    using (ImRaii.ItemWidth(valWidth))
                    {
                        ImGui.InputTextWithHint($"##add-val-{id}", headers.Length > 1 ? headers[1] : "Value", ref nVal, 512);
                    }

                    _dictAddStates[id] = (nKey, nVal);

                    ImGui.SameLine();
                    var saved = SuccessIconButton($"##mod-save-{id}", FontAwesomeIcon.Check,
                        dictionary.ContainsKey(nKey) ? "Key already exists" : "Add");
                    if (saved)
                    {
                        if (!string.IsNullOrWhiteSpace(nKey) && !dictionary.ContainsKey(nKey))
                        {
                            dictionary[nKey] = nVal;
                            _dictAddStates.Remove(id);
                            onModified();
                        }
                    }

                    ImGui.SameLine();
                    if (SecondaryIconButton($"##cancel-{id}", FontAwesomeIcon.Times, "Cancel"))
                    {
                        _dictAddStates.Remove(id);
                    }

                    SpacerY(1f);
                }
                float width = avail * 0.95f;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((avail - width) * 0.5f));
                if (Button($"Add New {title.Split(' ')[0]} {title.Split(' ')[1][..^2]}", new Vector2(width, 0)))
                {
                    _dictAddStates[id] = ("", "");
                    _dictEditStates.Remove(id);
                }
            }
                ;
        }

        DrawCollapsableCardWithTable(
            id,
            title,
            ref expanded,
            list,
            drawRow,
            headers,
            explicitCount: list.Count,
            setupColumns: setupColumns,
            drawFooter: internalFooter,
            collapsible: collapsible
        );

        foreach (var action in postDrawActions) action();
    }

    public bool ConfigCheckbox(string label, ref bool configValue, Action saveAction)
    {
        bool changed = ImGui.Checkbox(label, ref configValue);
        if (changed)
        {
            saveAction();
        }
        HoverHandIfItem();
        return changed;
    }

    public void ChannelPicker(
        string id,
        string currentId,
        IReadOnlyList<DSharpPlus.Entities.DiscordChannel>? channels,
        Action<string> onWaitSelection,
        string defaultLabel = "None",
        bool showLabel = true)
    {
        if (showLabel)
        {
            ImGui.TextColored(MutedText, "Discord Channel:");
        }

        string preview = defaultLabel;
        if (!string.IsNullOrEmpty(currentId) && channels != null)
        {
            var ch = channels.FirstOrDefault(c => c.Id.ToString() == currentId);
            if (ch != null) preview = $"#{ch.Name}";
            else preview = currentId;
        }

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - ImGui.GetStyle().FramePadding.X*2);
        using (var combo = ImRaii.Combo($"##{id}", preview))
        {
            if (combo)
            {
                if (ImGui.Selectable(defaultLabel, string.IsNullOrEmpty(currentId)))
                {
                    onWaitSelection(string.Empty);
                }

                if (channels != null)
                {
                    foreach (var channel in channels)
                    {
                        bool isSelected = channel.Id.ToString() == currentId;
                        // Use ID in label to prevent ImGui ID collisions with identical channel names
                        if (ImGui.Selectable($"#{channel.Name}##{channel.Id}", isSelected))
                        {
                            onWaitSelection(channel.Id.ToString());
                        }
                        if (isSelected) ImGui.SetItemDefaultFocus();
                    }
                    HoverHandIfItem();
                }
            }
        }
    }

    private Dictionary<string, (string Key, string Value)> _playerNoteEditStates = new();
    private Dictionary<string, (string NameWorld, string Note, bool FirstFrame)> _playerAddStates = new();

    public void DrawPlayerTable(
        string id,
        string title,
        ref bool expanded,
        IEnumerable<RememberedPlayerEntry> players,
        Action<RememberedPlayerEntry> onDelete,
        Action<RememberedPlayerEntry, string>? onSaveNote = null,
        Action<RememberedPlayerEntry>? onShowGlamour = null,
        Action<float>? drawFooter = null,
        string search = "",
        Action<string>? onSearch = null,
        Action<string, string>? onAdd = null,
        string emptyText = "No players found.",
        float maxTableHeight = 350)
    {
        var headers = new[] { "Player", "Last Seen", "Actions" };

        Action setupColumns = () =>
        {
            ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Last Seen", ImGuiTableColumnFlags.WidthFixed, 220f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, ScaledActionsWidth);
        };

        Action<RememberedPlayerEntry, int> drawRow = (player, idx) =>
        {
            // Column 1: Player Name & Notes/Info
            ImGui.TableSetColumnIndex(0);
            ImGui.Text(player.FullName);

            string editKey = $"{id}_{player.FullName}";
            bool isEditing = _playerNoteEditStates.TryGetValue(editKey, out var state);

            if (isEditing)
            {
                // Edit Note Input
                float inputWidth = ImGui.GetContentRegionAvail().X;
                string currentNote = state.Value;
                ImGui.SetNextItemWidth(inputWidth);
                if (ImGui.InputTextMultiline($"##editNote_{editKey}", ref currentNote, 1000, new Vector2(-1, 60)))
                {
                    _playerNoteEditStates[editKey] = (state.Key, currentNote);
                }
                HoverHandIfItem();
            }
            else
            {
                // Display Note if present
                if (onSaveNote != null && !string.IsNullOrWhiteSpace(player.Notes))
                {
                    ImGui.TextDisabled("Notes: ");
                    ImGui.SameLine();
                    ImGui.TextDisabled(player.Notes);
                }
            }

            // Column 2: Last Seen
            ImGui.TableSetColumnIndex(1);
            ImGui.TextDisabled(player.GetLastSeenRelative());

            // Column 3: Actions
            ImGui.TableSetColumnIndex(2);

            if (isEditing)
            {
                if (SuccessIconButton($"##save_{editKey}", FontAwesomeIcon.Check, "Save"))
                {
                    onSaveNote?.Invoke(player, state.Value);
                    _playerNoteEditStates.Remove(editKey);
                }

                ImGui.SameLine();

                if (SecondaryIconButton($"##cancel_{editKey}", FontAwesomeIcon.Times, "Cancel"))
                {
                    _playerNoteEditStates.Remove(editKey);
                }
            }
            else
            {
                bool shownAction = false;

                if (onSaveNote != null)
                {
                    if (SecondaryIconButton($"##edit_{editKey}", FontAwesomeIcon.Edit, "Edit Note"))
                    {
                        _playerNoteEditStates[editKey] = (player.FullName, player.Notes);
                    }
                    shownAction = true;
                }
                else if (onShowGlamour != null && player.Glamour != null)
                {
                    if (SecondaryIconButton($"##glamour_{editKey}", FontAwesomeIcon.Tshirt, "Show Glamour"))
                    {
                        onShowGlamour(player);
                    }
                    shownAction = true;
                }

                if (shownAction) ImGui.SameLine();

                if (DangerIconButton($"##del_{editKey}", FontAwesomeIcon.Trash, "Delete"))
                {
                    onDelete(player);
                    if (isEditing) _playerNoteEditStates.Remove(editKey);
                }
            }
        };

        Action? extraRows = null;
        bool isAdding = _playerAddStates.ContainsKey(id);

        if (isAdding)
        {
            extraRows = () =>
            {
                var s = _playerAddStates[id];
                string newNameWorld = s.NameWorld;
                string newNote = s.Note;

                ImGui.TableNextRow();

                // Column 0: Name and Note Input
                ImGui.TableSetColumnIndex(0);
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

                if (s.FirstFrame)
                {
                    ImGui.SetKeyboardFocusHere();
                    ImGui.SetScrollHereY(1.0f);
                    _playerAddStates[id] = (newNameWorld, newNote, false);
                }

                if (ImGui.InputTextWithHint($"##addName_{id}", "Name@World", ref newNameWorld, 100))
                {
                    _playerAddStates[id] = (newNameWorld, newNote, false);
                }

                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.InputTextWithHint($"##addNote_{id}", "Notes (Optional)...", ref newNote, 1000))
                {
                    _playerAddStates[id] = (newNameWorld, newNote, false);
                }

                // Column 1: Last Seen ("Now")
                ImGui.TableSetColumnIndex(1);
                ImGui.TextDisabled("Now");

                // Column 2: Actions (Save/Cancel)
                ImGui.TableSetColumnIndex(2);
                if (SuccessIconButton($"##saveAdd_{id}", FontAwesomeIcon.Check, "Add Player"))
                {
                    if (!string.IsNullOrWhiteSpace(newNameWorld))
                    {
                        onAdd?.Invoke(newNameWorld, newNote);
                        _playerAddStates.Remove(id);
                    }
                }

                ImGui.SameLine();

                if (SecondaryIconButton($"##cancelAdd_{id}", FontAwesomeIcon.Times, "Cancel"))
                {
                    _playerAddStates.Remove(id);
                }
            };
        }

        Action<float>? internalFooter = drawFooter;
        if (onAdd != null)
        {
            internalFooter = (w) =>
            {
                drawFooter?.Invoke(w);
                if (!isAdding)
                {
                    if (ImGui.Button("Add New Player", new Vector2(w, 0)))
                    {
                        _playerAddStates[id] = ("", "", true);
                    }
                    HoverHandIfItem();
                }
            };
        }

        Action drawSearch = () =>
        {
            string tempSearch = search;
            if (ImGui.InputTextWithHint($"##search_{id}", "Search...", ref tempSearch, 256))
            {
                onSearch?.Invoke(tempSearch);
            }
        };

        DrawCollapsableCardWithTable(
            id,
            title,
            ref expanded,
            players,
            drawRow,
            headers,
            true, // showCount
            null,
            setupColumns,
            true, // showHeaders
            internalFooter,
            drawSearch,
            extraRows,
            maxTableHeight
        );
    }
}
