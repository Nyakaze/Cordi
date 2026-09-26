using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Cordi.UI.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Themes;

public sealed partial class UiTheme
{
    public void StepStrip(IReadOnlyList<string> steps, float width)
    {
        var draw = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var height = Scaled(26f);
        var badge = Scaled(18f);
        var gap = Gap(0.4f);
        var rounding = Radius(0.6f);
        var chevron = FontAwesomeIcon.ChevronRight.ToIconString();
        var x = origin.X;
        var y = origin.Y;

        ApplyFontScale(0.86f);

        for (var i = 0; i < steps.Count; i++)
        {
            var label = steps[i];
            var textSize = ImGui.CalcTextSize(label);
            var itemWidth = badge + gap + textSize.X + PadX(0.9f);

            if (x > origin.X && x + itemWidth > origin.X + width)
            {
                x = origin.X;
                y += height + gap;
            }

            var min = new Vector2(x, y);
            var max = min + new Vector2(itemWidth, height);

            draw.AddRectFilled(min, max, ImGui.GetColorU32(CardBg), rounding);
            draw.AddRect(min, max, ImGui.GetColorU32(Border), rounding);

            var center = new Vector2(min.X + PadX(0.3f) + badge * 0.5f, min.Y + height * 0.5f);
            draw.AddCircleFilled(center, badge * 0.5f, ImGui.GetColorU32(new Vector4(Accent.X, Accent.Y, Accent.Z, 0.22f)));

            var number = (i + 1).ToString();
            var numberSize = ImGui.CalcTextSize(number);
            draw.AddText(center - numberSize * 0.5f, ImGui.GetColorU32(Accent), number);

            var textPos = new Vector2(center.X + badge * 0.5f + gap, min.Y + (height - textSize.Y) * 0.5f);
            draw.AddText(textPos, ImGui.GetColorU32(MutedText), label);

            x = max.X + gap;

            if (i == steps.Count - 1) continue;

            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                var size = ImGui.CalcTextSize(chevron) * 0.7f;
                if (x + size.X + gap <= origin.X + width)
                {
                    draw.AddText(
                        UiBuilder.IconFont,
                        ImGui.GetFontSize() * 0.7f,
                        new Vector2(x, y + (height - size.Y) * 0.5f),
                        ImGui.GetColorU32(FaintText),
                        chevron);
                    x += size.X + gap;
                }
            }
        }

        ApplyFontScale();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y + height - origin.Y));
    }

    public void MultiPicker(
        string id,
        string preview,
        bool hasValue,
        IReadOnlyList<DropdownItem> items,
        Func<string, bool> isSelected,
        Action<string> onToggle,
        float? width = null)
    {
        float pickerWidth = width ?? (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().FramePadding.X * 2);

        Dropdown.DrawMulti(id, pickerWidth, preview, hasValue, items, isSelected, onToggle);
    }

    private string? _sliderEditId;
    private string _sliderEditBuffer = string.Empty;
    private bool _sliderEditFocus;

    public bool SliderControl(
        string id,
        Vector2 pos,
        float width,
        ref int value,
        int min,
        int max,
        string suffix = "")
    {
        float current = value;

        if (!SliderControl(id, pos, width, ref current, min, max, 1f, 0, suffix))
            return false;

        value = (int)MathF.Round(current);
        return true;
    }

    public bool SliderControl(
        string id,
        Vector2 pos,
        float width,
        ref float value,
        float min,
        float max,
        float step = 0f,
        int decimals = 0,
        string suffix = "")
    {
        float height = Scaled(ControlHeight);
        float gap = Gap(0.6f);

        float valueWidth = MathF.Max(
            ImGui.CalcTextSize(FormatSliderValue(min, decimals, suffix)).X,
            ImGui.CalcTextSize(FormatSliderValue(max, decimals, suffix)).X) + PadX(1.6f);

        valueWidth = MathF.Max(valueWidth, Scaled(52f));

        float sliderWidth = MathF.Max(Scaled(60f), width - valueWidth - gap);
        valueWidth = MathF.Max(Scaled(40f), width - sliderWidth - gap);

        bool changed = RangeSlider($"{id}-track", pos, sliderWidth, ref value, min, max, step);
        var valuePos = new Vector2(pos.X + sliderWidth + gap, pos.Y);

        if (_sliderEditId == id)
        {
            if (_sliderEditFocus)
            {
                ImGui.SetKeyboardFocusHere();
                _sliderEditFocus = false;
            }

            string buffer = _sliderEditBuffer;

            bool submitted = TextInput(
                $"##{id}-value-edit",
                valuePos,
                valueWidth,
                ref buffer,
                16,
                string.Empty,
                ImGuiInputTextFlags.CharsDecimal
                    | ImGuiInputTextFlags.AutoSelectAll
                    | ImGuiInputTextFlags.EnterReturnsTrue);

            _sliderEditBuffer = buffer;

            if (!submitted && !ImGui.IsItemDeactivated())
                return changed;

            _sliderEditId = null;

            if (!float.TryParse(_sliderEditBuffer, NumberStyles.Float, CultureInfo.InvariantCulture, out float typed))
                return changed;

            typed = Math.Clamp(typed, min, max);

            if (step > 0f)
                typed = Math.Clamp(min + MathF.Round((typed - min) / step) * step, min, max);

            if (typed.Equals(value))
                return changed;

            value = typed;
            return true;
        }

        var draw = ImGui.GetWindowDrawList();
        var valueMax = valuePos + new Vector2(valueWidth, height);

        ImGui.SetCursorScreenPos(valuePos);
        bool clicked = ImGui.InvisibleButton($"##{id}-value", new Vector2(valueWidth, height));
        bool hovered = ImGui.IsItemHovered();

        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        draw.AddRectFilled(valuePos, valueMax, ImGui.GetColorU32(hovered ? FrameBgHover : FrameBg), Radius());
        draw.AddRect(valuePos, valueMax, ImGui.GetColorU32(hovered ? Accent : Border), Radius());

        string display = FormatSliderValue(value, decimals, suffix);

        using (ImRaii.PushColor(ImGuiCol.Text, hovered ? Text : MutedText))
        {
            var size = ImGui.CalcTextSize(display);
            ImGui.SetCursorScreenPos(new Vector2(
                valuePos.X + (valueWidth - size.X) * 0.5f,
                valuePos.Y + (height - size.Y) * 0.5f));
            ImGui.TextUnformatted(display);
        }

        if (hovered)
            Tooltip("Click to type an exact value");

        if (clicked)
        {
            _sliderEditId = id;
            _sliderEditBuffer = value.ToString($"F{decimals}", CultureInfo.InvariantCulture);
            _sliderEditFocus = true;
        }

        return changed;
    }

    private static string FormatSliderValue(float value, int decimals, string suffix) =>
        value.ToString($"F{decimals}", CultureInfo.InvariantCulture) + suffix;

    public bool RangeSlider(string id, Vector2 pos, float width, ref float value, float min, float max, float step = 0f)
    {
        var draw = ImGui.GetWindowDrawList();
        float height = Scaled(ControlHeight);
        float trackHeight = Scaled(8f);
        var knobSize = new Vector2(Scaled(12f), Scaled(20f));

        ImGui.SetCursorScreenPos(pos);
        ImGui.InvisibleButton(id, new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        bool active = ImGui.IsItemActive();
        if (hovered || active)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        float usable = width - knobSize.X;
        float previous = value;

        if (active && usable > 0f)
        {
            float t = Math.Clamp((ImGui.GetIO().MousePos.X - pos.X - knobSize.X * 0.5f) / usable, 0f, 1f);
            value = min + t * (max - min);

            if (step > 0f)
                value = min + MathF.Round((value - min) / step) * step;

            value = Math.Clamp(value, min, max);
        }

        float progress = max > min ? (value - min) / (max - min) : 0f;
        var trackMin = new Vector2(pos.X, pos.Y + (height - trackHeight) * 0.5f);
        var trackMax = new Vector2(pos.X + width, trackMin.Y + trackHeight);
        float fillEnd = pos.X + knobSize.X * 0.5f + usable * progress;

        draw.AddRectFilled(trackMin, trackMax, ImGui.GetColorU32(FrameBg), Radius(0.5f));
        draw.AddRectFilled(trackMin, new Vector2(fillEnd, trackMax.Y), ImGui.GetColorU32(Accent), Radius(0.5f));
        draw.AddRect(trackMin, trackMax, ImGui.GetColorU32(Border), Radius(0.5f));

        var knobMin = new Vector2(pos.X + usable * progress, pos.Y + (height - knobSize.Y) * 0.5f);
        draw.AddRectFilled(knobMin, knobMin + knobSize, ImGui.GetColorU32(hovered || active ? AccentHover : Accent), Radius(0.42f));
        draw.AddRect(knobMin, knobMin + knobSize, ImGui.GetColorU32(Border), Radius(0.42f));

        return !value.Equals(previous);
    }
}
