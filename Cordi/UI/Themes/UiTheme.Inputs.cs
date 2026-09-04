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
    private IDisposable? _activeInputColorScope;
    private IDisposable? _activeInputStyleScope;
    private Cordi.UI.Components.Dropdown? _dropdown;

    private Cordi.UI.Components.Dropdown Dropdown => _dropdown ??= new Cordi.UI.Components.Dropdown(this);

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


    public bool TextInput(string id, Vector2 pos, float width, ref string value, int maxLength, string hint = "", ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
    {
        float height = Scaled(ControlHeight);
        float padY = MathF.Max(0f, (height - ImGui.GetTextLineHeight()) * 0.5f);

        ImGui.SetCursorScreenPos(pos);
        ImGui.SetNextItemWidth(width);

        using var style = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(PadX(0.8f), padY))
            .Push(ImGuiStyleVar.FrameRounding, Radius())
            .Push(ImGuiStyleVar.FrameBorderSize, 1f);
        using var color = ImRaii.PushColor(ImGuiCol.FrameBg, FrameBg)
            .Push(ImGuiCol.FrameBgHovered, FrameBgHover)
            .Push(ImGuiCol.FrameBgActive, FrameBgActive)
            .Push(ImGuiCol.Border, Border);

        return string.IsNullOrEmpty(hint)
            ? ImGui.InputText(id, ref value, maxLength, flags)
            : ImGui.InputTextWithHint(id, hint, ref value, maxLength, flags);
    }

    public bool NumberInput(string id, Vector2 pos, float width, ref int value, int min = int.MinValue, int max = int.MaxValue)
    {
        float height = Scaled(ControlHeight);
        float padY = MathF.Max(0f, (height - ImGui.GetTextLineHeight()) * 0.5f);

        ImGui.SetCursorScreenPos(pos);
        ImGui.SetNextItemWidth(width);

        using var style = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(PadX(0.8f), padY))
            .Push(ImGuiStyleVar.FrameRounding, Radius())
            .Push(ImGuiStyleVar.FrameBorderSize, 1f);
        using var color = ImRaii.PushColor(ImGuiCol.FrameBg, FrameBg)
            .Push(ImGuiCol.FrameBgHovered, FrameBgHover)
            .Push(ImGuiCol.FrameBgActive, FrameBgActive)
            .Push(ImGuiCol.Border, Border);

        if (!ImGui.InputInt(id, ref value, 0, 0))
            return false;

        value = Math.Clamp(value, min, max);
        return true;
    }

    public void HelpPill(string id, Vector2 rightAnchor, string tooltip, string label = "Quick Help")
    {
        var draw = ImGui.GetWindowDrawList();

        ApplyFontScale(0.86f);
        var textSize = ImGui.CalcTextSize(label);
        ApplyFontScale();

        float iconSize = Scaled(14f);
        float height = Scaled(28f);
        float width = textSize.X + iconSize + PadX(1.4f);
        var min = new Vector2(rightAnchor.X - width, rightAnchor.Y - Scaled(5f));

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton($"##help-pill-{id}", new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        draw.AddRectFilled(min, min + new Vector2(width, height), ImGui.GetColorU32(hovered ? RowHover : CardBg), height * 0.5f);
        draw.AddRect(min, min + new Vector2(width, height), ImGui.GetColorU32(Border), height * 0.5f);

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, hovered ? Text : FaintText))
        {
            var glyph = FontAwesomeIcon.QuestionCircle.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(new Vector2(min.X + PadX(0.5f), min.Y + (height - size.Y) * 0.5f));
            ImGui.TextUnformatted(glyph);
        }

        ApplyFontScale(0.86f);
        using (ImRaii.PushColor(ImGuiCol.Text, hovered ? Text : MutedText))
        {
            ImGui.SetCursorScreenPos(new Vector2(min.X + PadX(0.5f) + iconSize + Gap(0.5f), min.Y + (height - textSize.Y) * 0.5f));
            ImGui.TextUnformatted(label);
        }
        ApplyFontScale();

        if (hovered)
            Tooltip(tooltip);

        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(new Vector2(width, height));
    }

    public Vector2 ChipSize(ReadOnlySpan<char> text, float fontScale = 0.78f)
    {
        ApplyFontScale(fontScale);
        var textSize = ImGui.CalcTextSize(text.ToString());
        ApplyFontScale();

        return new Vector2(textSize.X + PadX(0.7f) * 2f, textSize.Y + PadY(0.35f) * 2f);
    }

    public void ChipAt(Vector2 pos, ReadOnlySpan<char> text, Vector4? color = null, float fontScale = 0.78f)
    {
        var tint = color ?? Accent;
        var size = ChipSize(text, fontScale);
        var draw = ImGui.GetWindowDrawList();

        draw.AddRectFilled(pos, pos + size, ImGui.GetColorU32(new Vector4(tint.X, tint.Y, tint.Z, 0.16f)), Radius(0.6f));

        ApplyFontScale(fontScale);
        using (ImRaii.PushColor(ImGuiCol.Text, tint))
        {
            ImGui.SetCursorScreenPos(pos + new Vector2(PadX(0.7f), PadY(0.35f)));
            ImGui.TextUnformatted(text.ToString());
        }
        ApplyFontScale();
    }

    public bool ColorSwatch(string id, Vector2 pos, float width, ref Vector4 color)
    {
        var draw = ImGui.GetWindowDrawList();
        float height = Scaled(ControlHeight);
        float inset = Scaled(4f);

        ImGui.SetCursorScreenPos(pos);
        bool clicked = ImGui.InvisibleButton(id, new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        var max = pos + new Vector2(width, height);
        draw.AddRectFilled(pos, max, ImGui.GetColorU32(hovered ? FrameBgHover : FrameBg), Radius());
        draw.AddRect(pos, max, ImGui.GetColorU32(Border), Radius());

        var fillMin = pos + new Vector2(inset, inset);
        var fillMax = max - new Vector2(inset, inset);
        draw.AddRectFilled(fillMin, fillMax, ImGui.GetColorU32(color), Radius(0.7f));
        draw.AddRect(fillMin, fillMax, ImGui.GetColorU32(Border), Radius(0.7f));

        if (clicked)
            ImGui.OpenPopup($"{id}-picker");

        bool changed = false;

        using (var popup = ImRaii.Popup($"{id}-picker"))
        {
            if (popup)
            {
                changed = ImGui.ColorPicker4(
                    $"{id}-picker4",
                    ref color,
                    ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.NoSidePreview | ImGuiColorEditFlags.DisplayHex);
            }
        }

        return changed;
    }

    public bool PathInput(string id, Vector2 pos, float width, ref string value, Action onBrowse, string hint = "")
    {
        float height = Scaled(ControlHeight);
        float browseWidth = height;
        float fieldWidth = MathF.Max(Scaled(40f), width - browseWidth - Gap(0.5f));

        bool changed = TextInput(id, pos, fieldWidth, ref value, 512, hint);

        var browsePos = new Vector2(pos.X + fieldWidth + Gap(0.5f), pos.Y);
        ImGui.SetCursorScreenPos(browsePos);

        using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, Radius()))
        using (ImRaii.PushColor(ImGuiCol.Button, FrameBg))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, FrameBgHover))
        using (ImRaii.PushColor(ImGuiCol.ButtonActive, FrameBgActive))
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            if (ImGui.Button($"{FontAwesomeIcon.FolderOpen.ToIconString()}##{id}-browse", new Vector2(browseWidth, height)))
                onBrowse();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        return changed;
    }

    public Vector2 ToggleSize() => new(Scaled(ToggleWidth), Scaled(ToggleHeight));

    public bool ToggleSwitch(string id, ref bool value, bool enabled = true) =>
        ToggleSwitch(id, ImGui.GetCursorScreenPos(), ref value, enabled);

    public bool ToggleSwitch(string id, Vector2 pos, ref bool value, bool enabled = true)
    {
        var draw = ImGui.GetWindowDrawList();
        var size = ToggleSize();

        ImGui.SetCursorScreenPos(pos);
        bool clicked = ImGui.InvisibleButton(id, size) && enabled;
        bool hovered = ImGui.IsItemHovered() && enabled;
        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        if (clicked)
            value = !value;

        float alpha = enabled ? 1f : 0.4f;
        float rounding = Radius(0.7f);
        var max = pos + size;

        var track = value
            ? hovered ? AccentHover : Accent
            : hovered ? FrameBgHover : FrameBg;

        var border = value ? Accent : Border;
        var knob = value ? AccentText : MutedText;

        draw.AddRectFilled(pos, max, Faded(track, alpha), rounding);
        draw.AddRect(pos, max, Faded(border, alpha), rounding);

        float inset = Scaled(3f);
        float knobSize = size.Y - inset * 2f;
        float knobX = value ? max.X - inset - knobSize : pos.X + inset;
        var knobMin = new Vector2(knobX, pos.Y + inset);

        draw.AddRectFilled(knobMin, knobMin + new Vector2(knobSize, knobSize), Faded(knob, alpha), Radius(0.42f));

        return clicked;
    }

    private static uint Faded(Vector4 color, float alpha) =>
        ImGui.GetColorU32(new Vector4(color.X, color.Y, color.Z, color.W * alpha));

    private Vector4 CardBgOr(Vector4? fallback = null) => CardBg != default ? CardBg : (fallback ?? new Vector4(0.11f, 0.12f, 0.14f, 1f));

    private Vector4 WindowBorderOr() => WindowBorder.W > 0 ? WindowBorder : new Vector4(0.23f, 0.25f, 0.30f, 1f);
    private Vector4 TextOr() => Text.W > 0 ? Text : new Vector4(0.92f, 0.92f, 0.96f, 1f);
    private Vector4 MutedOr() => MutedText.W > 0 ? MutedText : new Vector4(0.70f, 0.72f, 0.78f, 1f);


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
        IReadOnlyList<Crovus.Models.DiscordChannel>? channels,
        Action<string> onWaitSelection,
        string defaultLabel = "None",
        bool showLabel = true,
        float? width = null)
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

        float pickerWidth = width ?? (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().FramePadding.X * 2);

        var items = new List<Cordi.UI.Components.DropdownItem>
        {
            new() { Key = string.Empty, Label = defaultLabel },
        };

        if (channels != null)
        {
            foreach (var channel in channels)
                items.Add(new Cordi.UI.Components.DropdownItem { Key = channel.Id.ToString(), Label = $"#{channel.Name}" });
        }

        Dropdown.Draw(id, pickerWidth, preview, !string.IsNullOrEmpty(currentId), items, currentId, onWaitSelection);
    }

    public void OptionPicker(
        string id,
        string currentKey,
        IReadOnlyList<Cordi.UI.Components.DropdownItem> items,
        Action<string> onSelect,
        float? width = null)
    {
        string preview = items.FirstOrDefault(item => item.Key == currentKey).Label ?? string.Empty;
        float pickerWidth = width ?? (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().FramePadding.X * 2);

        Dropdown.Draw(id, pickerWidth, preview, true, items, currentKey, onSelect);
    }

    public float PickerCaptionHeight() => Dropdown.CaptionHeight();

    public void IconPicker(
        string id,
        Vector2 size,
        FontAwesomeIcon icon,
        string caption,
        float captionWidth,
        IReadOnlyList<Cordi.UI.Components.DropdownItem> items,
        string currentKey,
        Action<string> onSelect,
        float popupWidth,
        bool enabled = true,
        string tooltip = "",
        Vector4? accent = null) =>
        Dropdown.DrawIconPicker(id, size, icon, caption, captionWidth, items, currentKey, onSelect, popupWidth, enabled, tooltip, accent);

    public void ThreadPicker(
        string id,
        string currentId,
        IReadOnlyDictionary<ulong, string> threads,
        Action<string> onSelect,
        string defaultLabel = "None",
        float? width = null)
    {
        bool known = ulong.TryParse(currentId, out var currentThreadId) && threads.ContainsKey(currentThreadId);

        string preview = defaultLabel;
        if (known) preview = $"#{threads[currentThreadId]}";
        else if (!string.IsNullOrEmpty(currentId)) preview = currentId;

        float pickerWidth = width ?? (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().FramePadding.X * 2);

        var items = new List<Cordi.UI.Components.DropdownItem>();

        if (!known && !string.IsNullOrEmpty(currentId))
            items.Add(new Cordi.UI.Components.DropdownItem { Key = currentId, Label = currentId });

        foreach (var thread in threads.OrderBy(t => t.Value, StringComparer.OrdinalIgnoreCase))
            items.Add(new Cordi.UI.Components.DropdownItem { Key = thread.Key.ToString(), Label = $"#{thread.Value}" });

        Dropdown.Draw(id, pickerWidth, preview, !string.IsNullOrEmpty(currentId), items, currentId, onSelect);
    }
}
