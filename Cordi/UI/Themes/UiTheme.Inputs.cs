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
