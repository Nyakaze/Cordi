
using System;
using System.Numerics;
using Cordi.Extensions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface;
using System.Collections.Generic;
using System.Linq;


namespace Cordi.UI.Themes;


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
    public static Vector4 ColorSuccess => ColorConvertor.ToVector4("#3C00A5");
    public static Vector4 ColorSuccessText => ColorConvertor.ToVector4("#CF9FFF"); // Lighter purple/lilac for readability
    public static Vector4 ColorDanger => new(0.565f, 0.0f, 0.0f, 1f);
    public static Vector4 ColorDangerText => new(1.0f, 0.4f, 0.4f, 1f);

    public float RadiusBase = 8f;
    public float PadBase = 10f;
    public float GapBase = 8f;


    public float Radius(float mul = 1f) => RadiusBase * ImGuiHelpers.GlobalScale * mul;
    public float PadX(float mul = 1f) => PadBase * ImGuiHelpers.GlobalScale * mul;
    public float PadY(float mul = 1f) => (PadBase * 0.9f) * ImGuiHelpers.GlobalScale * mul;
    public float Gap(float mul = 1f) => GapBase * ImGuiHelpers.GlobalScale * mul;

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


    int _pushedWindowColors, _pushedWindowVars;

    public void PushWindow()
    {
        _pushedWindowColors = 0;
        _pushedWindowVars = 0;

        ImGui.PushStyleColor(ImGuiCol.WindowBg, WindowBg); _pushedWindowColors++;
        ImGui.PushStyleColor(ImGuiCol.Border, WindowBorder); _pushedWindowColors++;
        ImGui.PushStyleColor(ImGuiCol.TitleBg, TitleBg); _pushedWindowColors++;
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, TitleBgActive); _pushedWindowColors++;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, Radius(1.2f)); _pushedWindowVars++;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f * ImGuiHelpers.GlobalScale); _pushedWindowVars++;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(PadX(), PadY())); _pushedWindowVars++;
    }

    public void PopWindow()
    {
        if (_pushedWindowVars > 0) ImGui.PopStyleVar(_pushedWindowVars);
        if (_pushedWindowColors > 0) ImGui.PopStyleColor(_pushedWindowColors);
        _pushedWindowVars = _pushedWindowColors = 0;
    }


    public void BeginCard(string id, Vector2 minSize = default, bool border = true)
    {
        var avail = ImGui.GetContentRegionAvail();
        if (minSize.X <= 0) minSize.X = avail.X;

        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Radius());
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(PadX(0.6f), PadY(0.6f)));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        if (border)
            ImGui.PushStyleColor(ImGuiCol.Border, WindowBorder);

        ImGui.BeginChild(id, minSize, border);
        ImGui.PopStyleColor(border ? 2 : 1);
        ImGui.PopStyleVar(2);


    }

    public void EndCard()
    {
        ImGui.Dummy(new Vector2(0, Gap(0.25f)));
        ImGui.EndChild();
    }


    public bool PrimaryButton(string label, Vector2 size = default)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, Accent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Lerp(Accent, Vector4.One, 0.08f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Lerp(Accent, Vector4.Zero, 0.10f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Radius());
        var clicked = Button(label, size);
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(3);
        return clicked;
    }

    public bool SecondaryButton(string label, Vector2 size = default)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, FrameBg);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, FrameBgHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, FrameBgActive);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Radius());
        var clicked = Button(label, size);
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(3);
        return clicked;
    }

    public bool Button(string label, Vector2 size = default)
    {
        var ret = ImGui.Button(label, size);
        HoverHandIfItem();
        return ret;
    }

    public bool Checkbox(string label, ref bool v)
    {
        var ret = ImGui.Checkbox(label, ref v);
        HoverHandIfItem();
        return ret;
    }

    public bool InvisibleButton(string id, Vector2 size = default)
    {
        var ret = ImGui.InvisibleButton(id, size);
        HoverHandIfItem();
        return ret;
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


    int _pushedInputColors, _pushedInputVars;

    public void PushInputScope()
    {
        _pushedInputColors = 0; _pushedInputVars = 0;
        ImGui.PushStyleColor(ImGuiCol.FrameBg, FrameBg); _pushedInputColors++;
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, FrameBgHover); _pushedInputColors++;
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, FrameBgActive); _pushedInputColors++;
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, SliderGrab); _pushedInputColors++;
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, SliderGrabActive); _pushedInputColors++;
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Radius()); _pushedInputVars++;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(Gap(), Gap(0.6f))); _pushedInputVars++;
    }

    public void PopInputScope()
    {
        if (_pushedInputVars > 0) ImGui.PopStyleVar(_pushedInputVars);
        if (_pushedInputColors > 0) ImGui.PopStyleColor(_pushedInputColors);
        _pushedInputVars = _pushedInputColors = 0;
    }


    public bool BeginTabBar(string id, ImGuiTabBarFlags flags = ImGuiTabBarFlags.None)
    {
        ImGui.PushStyleColor(ImGuiCol.Tab, Tab);
        ImGui.PushStyleColor(ImGuiCol.TabActive, TabActive);
        ImGui.PushStyleColor(ImGuiCol.TabHovered, TabHovered);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, Radius());
        return ImGui.BeginTabBar(id, flags);
    }
    public void EndTabBar()
    {
        ImGui.EndTabBar();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(3);
    }

    public bool BeginTabItem(string label, ref bool open, ImGuiTabItemFlags flags = ImGuiTabItemFlags.None)
        => ImGui.BeginTabItem(label, ref open, flags);
    public bool BeginTabItem(string label, ImGuiTabItemFlags flags = ImGuiTabItemFlags.None)
        => ImGui.BeginTabItem(label, flags);
    public void EndTabItem() => ImGui.EndTabItem();


    public bool BeginTable(string id, int columns, ImGuiTableFlags flags =
        ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(PadX(0.6f), PadY(0.45f)));
        ImGui.PushStyleColor(ImGuiCol.TableBorderStrong, WindowBorder);
        var ok = ImGui.BeginTable(id, columns, flags);
        if (!ok)
        {
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();
        }
        return ok;
    }

    public void EndTable()
    {
        ImGui.EndTable();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
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
        ImGui.PushStyleColor(ImGuiCol.Text, fgCol);
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();
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

        ImGui.PushStyleColor(ImGuiCol.Text, _fg);
        ImGui.SetCursorScreenPos(textPos);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();

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
        var colChkOn = new Vector4(0.35f, 0.75f, 0.45f, 1f);

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

        ImGui.PushStyleColor(ImGuiCol.Text, TextOr());
        ImGui.SetCursorScreenPos(new Vector2(textLeft, textTop));
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();

        var muted = MutedOr();
        var authSize = ImGui.CalcTextSize(authorRightAligned);
        ImGui.PushStyleColor(ImGuiCol.Text, muted);
        ImGui.SetCursorScreenPos(new Vector2(textRight - authSize.X, textTop));
        ImGui.TextUnformatted(authorRightAligned);
        ImGui.PopStyleColor();

        ImGui.PushStyleColor(ImGuiCol.Text, muted);
        ImGui.SetCursorScreenPos(new Vector2(textLeft, textTop + ImGui.GetTextLineHeightWithSpacing()));
        ImGui.PushTextWrapPos(textRight);
        ImGui.TextUnformatted(description);
        ImGui.PopTextWrapPos();
        ImGui.PopStyleColor();

        string tag = tagLabel;
        var tagPad = new Vector2(PadX(0.6f), PadY(0.4f));
        var tagSize = ImGui.CalcTextSize(tag);
        var tagPos = new Vector2(textLeft, textBottom - tagSize.Y - tagPad.Y);
        var tagRect = tagPos + tagSize + tagPad * 2f;

        var tagBgCol = tagBg ?? Accent;
        var tagTextCol = AccentText.W > 0 ? AccentText : new Vector4(1, 1, 1, 1);

        draw.AddRectFilled(tagPos, tagRect, ImGui.GetColorU32(tagBgCol), Radius(0.75f));
        draw.AddRect(tagPos, tagRect, ImGui.GetColorU32(border), Radius(0.75f));

        ImGui.PushStyleColor(ImGuiCol.Text, tagTextCol);
        ImGui.SetCursorScreenPos(tagPos + tagPad);
        ImGui.TextUnformatted(tag);
        ImGui.PopStyleColor();

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
            var colChkOn = new Vector4(0.35f, 0.75f, 0.45f, 1f);
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
            ImGui.PushStyleColor(ImGuiCol.Text, Text);
            ImGui.TextUnformatted(title);
            ImGui.PopStyleColor();
        });

        if (drawTopRight is not null)
        {
            drawTopRight(slots, this);
        }
        else if (!string.IsNullOrEmpty(defaultTopRightText))
        {
            WithCursor(topRightRect, () =>
            {
                ImGui.PushStyleColor(ImGuiCol.Text, MutedText);
                var txt = defaultTopRightText;
                var sz = ImGui.CalcTextSize(txt);
                ImGui.SetCursorScreenPos(new Vector2(topRightRect.Max.X - sz.X, topRightRect.Min.Y));
                ImGui.TextUnformatted(txt);
                ImGui.PopStyleColor();
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
                ImGui.PushStyleColor(ImGuiCol.Text, MutedText);
                ImGui.PushTextWrapPos(bodyRect.Max.X);
                ImGui.TextUnformatted(defaultDescription);
                ImGui.PopTextWrapPos();
                ImGui.PopStyleColor();
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
            ImGui.PushStyleColor(ImGuiCol.Text, AccentText);
            WithCursor(new UiRect(rect.Min + tagPad, rect.Min + tagPad + sz), () =>
            {
                ImGui.TextUnformatted(defaultBottomLeftTag);
            });
            ImGui.PopStyleColor();

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
        string? mutedText = null)
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

        ImGui.BeginGroup();



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
        }
        else
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padX);
        }

        ImGui.PushStyleColor(ImGuiCol.Text, Text);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();

        if (!string.IsNullOrEmpty(mutedText))
        {
            ImGui.SameLine();
            ImGui.TextColored(MutedText, mutedText);
        }



        SpacerY(0.5f);


        ImGui.Indent(padX);
        float innerWidth = availW - (padX * 2);
        ImGui.PushItemWidth(innerWidth);

        drawContent(innerWidth);

        ImGui.PopItemWidth();
        ImGui.Unindent(padX);

        ImGui.Dummy(new Vector2(0, padY));

        ImGui.EndGroup();

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
        ImGui.PushStyleColor(ImGuiCol.Text, MutedText);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
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
        Action? drawFooter = null)
    {
        int count = explicitCount ?? collection.Count();
        title = showCount ? $"{title}: {count}" : title;

        float scale = ImGuiHelpers.GlobalScale;
        float headerHeight = 35f * scale;
        if (headerHeight < ImGui.GetFrameHeightWithSpacing()) headerHeight = ImGui.GetFrameHeightWithSpacing();

        float padX = PadX(0.9f);
        float padY = PadY(0.9f);
        float radius = Radius(1.0f);

        var draw = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var availW = ImGui.GetContentRegionAvail().X;

        draw.ChannelsSplit(2);
        draw.ChannelsSetCurrent(1);

        ImGui.BeginGroup();

        float titleCenterY = startPos.Y + (headerHeight - ImGui.GetTextLineHeight()) * 0.5f;
        ImGui.SetCursorScreenPos(new Vector2(startPos.X + padX, titleCenterY));

        ImGui.PushStyleColor(ImGuiCol.Text, Text);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();

        ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
        string icon = expanded ? FontAwesomeIcon.ChevronUp.ToIconString() : FontAwesomeIcon.ChevronDown.ToIconString();
        var iconSize = ImGui.CalcTextSize(icon);
        ImGui.SetCursorScreenPos(new Vector2(startPos.X + availW - padX - iconSize.X, titleCenterY));
        ImGui.TextUnformatted(icon);
        ImGui.PopFont();

        ImGui.SetCursorScreenPos(startPos);
        if (InvisibleButton($"##{id}HeaderBtn", new Vector2(availW, headerHeight)))
        {
            expanded = !expanded;
        }

        if (expanded)
        {
            ImGui.SetCursorScreenPos(new Vector2(startPos.X + padX, startPos.Y + headerHeight + Gap(0.2f)));

            if (count == 0 && drawFooter == null)
            {
                ImGui.TextUnformatted("No items");
            }
            else
            {
                if (count > 0)
                    DrawTable(id, collection, drawRow, headers, setupColumns, showHeaders);

                if (drawFooter != null)
                {
                    SpacerY(0.5f);
                    drawFooter();
                }
            }

            ImGui.Dummy(new Vector2(0, padY * 0.5f));
        }

        ImGui.EndGroup();
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
        bool showHeaders = false)
    {
        int columns = headers?.Length ?? 1;

        if (ImGui.BeginTable($"##table_{id}", columns, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp))
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
            ImGui.EndTable();
        }
    }

    public void DrawTable<T>(string id, IEnumerable<T> collection, Action<T, int> drawRow, int columns)
    {
        if (ImGui.BeginTable($"##table_{id}", columns, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp))
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
            ImGui.EndTable();
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
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 80f * ImGuiHelpers.GlobalScale);
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
                ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                if (Button($"{FontAwesomeIcon.Check.ToIconString()}##save-{id}-{idx}"))
                {
                    list[idx] = _stringEditStates[editKey].Value;
                    _stringEditStates.Remove(editKey);
                    onListModified();
                }
                ImGui.PopFont();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Save");

                ImGui.SameLine();
                ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                if (Button($"{FontAwesomeIcon.Times.ToIconString()}##cancel-{id}-{idx}"))
                {
                    // If canceling a new empty item, remove it
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
                ImGui.PopFont();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Cancel");
            }
            else
            {
                ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
                if (Button($"{FontAwesomeIcon.Pen.ToIconString()}##edit-{id}-{idx}"))
                {
                    _stringEditStates[editKey] = (idx, item);
                }
                ImGui.PopStyleColor();
                ImGui.PopFont();

                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Edit");

                ImGui.SameLine();
                ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.56f, 0f, 0f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.1f, 0.1f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0f, 0f, 1f));
                if (Button($"{FontAwesomeIcon.Trash.ToIconString()}##del-{id}-{idx}"))
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
                ImGui.PopStyleColor(3);
                ImGui.PopFont();

                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Delete");
            }
        };

        Action drawFooter = () =>
        {
            if (allowAdd)
            {
                float avail = ImGui.GetContentRegionAvail().X;
                float width = avail * 0.95f;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((avail - width) * 0.5f));
                if (Button($"Add new {itemName}##{id}", new Vector2(width, 0)))
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
            drawFooter: allowAdd ? drawFooter : null
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
        Action? drawFooter = null,
        bool allowAdd = false)
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
                ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                if (Button($"{FontAwesomeIcon.Check.ToIconString()}##save-{id}-{idx}"))
                {
                    dictionary[key] = state.Value;
                    _dictEditStates.Remove(editKey);
                    onModified();
                }
                ImGui.PopFont();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Save");

                ImGui.SameLine();
                ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                if (Button($"{FontAwesomeIcon.Times.ToIconString()}##cancel-{id}-{idx}"))
                {
                    _dictEditStates.Remove(editKey);
                }
                ImGui.PopFont();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Cancel");
            }
            else
            {
                ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
                if (Button($"{FontAwesomeIcon.Pen.ToIconString()}##edit-{id}-{idx}"))
                {
                    _dictEditStates[editKey] = (key, value);
                    _dictAddStates.Remove(id);
                }
                ImGui.PopStyleColor();
                ImGui.PopFont();

                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Edit");

                ImGui.SameLine();
                ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.56f, 0f, 0f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.1f, 0.1f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0f, 0f, 1f));

                if (Button($"{FontAwesomeIcon.Trash.ToIconString()}##del-{id}-{idx}"))
                {
                    postDrawActions.Add(() =>
                    {
                        dictionary.Remove(key);
                        onModified();
                        if (_dictEditStates.TryGetValue(editKey, out var s) && s.Key == key)
                            _dictEditStates.Remove(editKey);
                    });
                }
                ImGui.PopStyleColor(3);
                ImGui.PopFont();

                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Delete");
            }
        };

        Action? internalFooter = drawFooter;

        if (allowAdd)
        {
            internalFooter = () =>
            {
                if (drawFooter != null) drawFooter();
                float avail = ImGui.GetContentRegionAvail().X;

                if (_dictAddStates.TryGetValue(id, out var addState))
                {
                    float pad = PadX(0.6f);
                    float actionsWidth = 80f * ImGuiHelpers.GlobalScale; // Reserve space for buttons aligned with table actions
                    float inputsTotalWidth = avail - actionsWidth - pad;
                    float keyWidth = inputsTotalWidth * 0.35f;
                    float valWidth = inputsTotalWidth - keyWidth - Gap();

                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + pad);

                    string nKey = addState.NewKey;
                    string nVal = addState.NewValue;

                    ImGui.PushItemWidth(keyWidth);
                    ImGui.InputTextWithHint($"##add-key-{id}", headers.Length > 0 ? headers[0] : "Key", ref nKey, 128);
                    ImGui.PopItemWidth();

                    ImGui.SameLine();
                    ImGui.PushItemWidth(valWidth);
                    ImGui.InputTextWithHint($"##add-val-{id}", headers.Length > 1 ? headers[1] : "Value", ref nVal, 512);
                    ImGui.PopItemWidth();

                    _dictAddStates[id] = (nKey, nVal);

                    ImGui.SameLine();
                    ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                    var saved = Button($"{FontAwesomeIcon.Check.ToIconString()}##mod-save-{id}");
                    ImGui.PopFont();
                    if (saved)
                    {
                        if (!string.IsNullOrWhiteSpace(nKey) && !dictionary.ContainsKey(nKey))
                        {
                            dictionary[nKey] = nVal;
                            _dictAddStates.Remove(id);
                            onModified();
                        }
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(dictionary.ContainsKey(nKey) ? "Key already exists" : "Add");

                    ImGui.SameLine();
                    ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                    var canceled = Button($"{FontAwesomeIcon.Times.ToIconString()}##cancel-{id}");
                    ImGui.PopFont();
                    if (canceled)
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
            drawFooter: internalFooter
        );

        foreach (var action in postDrawActions) action();
    }
}
