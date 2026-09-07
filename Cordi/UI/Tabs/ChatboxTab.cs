using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab : ConfigTabBase
{
    private ListPanel? listRenderer;
    private ChipSelector? chipRenderer;

    private ListPanel List => listRenderer ??= new ListPanel(theme);
    private ChipSelector Chips => chipRenderer ??= new ChipSelector(theme);

    private bool pendingScrollTop;

    public ChatboxTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
    }

    public override string Label => "Chatbox";

    private ChatboxConfig Cfg => plugin.Config.Chatbox;

    private void ConsumeScroll()
    {
        if (!pendingScrollTop)
            return;

        pendingScrollTop = false;
        ImGui.SetScrollY(0f);
    }

    private void DrawToggleRow(
        string id,
        FontAwesomeIcon icon,
        string title,
        string subtitle,
        float rowWidth,
        Func<bool> get,
        Action<bool> set,
        Vector4? activeColor = null) =>
        DrawToggleRow(
            id,
            icon,
            get() ? activeColor ?? theme.Accent : theme.MutedText,
            title,
            subtitle,
            rowWidth,
            get,
            set);

    private void DrawSliderRow(
        string id,
        FontAwesomeIcon icon,
        string title,
        string subtitle,
        float rowWidth,
        float min,
        float max,
        float step,
        Func<float> get,
        Action<float> set,
        int decimals = 0,
        string suffix = "") =>
        DrawSliderRow(
            id, icon, theme.Accent, title, subtitle, rowWidth,
            min, max, step, get, set, decimals, suffix);

    private void DrawIntSliderRow(
        string id,
        FontAwesomeIcon icon,
        string title,
        string subtitle,
        float rowWidth,
        int min,
        int max,
        Func<int> get,
        Action<int> set,
        string suffix = "")
    {
        Row.Draw(
            id: id,
            icon: icon,
            iconColor: theme.Accent,
            title: title,
            subtitle: subtitle,
            controlWidth: 260f,
            drawControl: (pos, width) =>
            {
                int value = get();

                if (!theme.SliderControl($"##{id}-slider", pos, width, ref value, min, max, suffix))
                    return;

                set(value);
                Save();
            },
            rowWidth: rowWidth);
    }

    private void DrawTextRow(
        string id,
        FontAwesomeIcon icon,
        string title,
        string subtitle,
        float rowWidth,
        Func<string> get,
        Action<string> set,
        int maxLength = 128,
        string hint = "",
        float controlWidth = 280f)
    {
        string current = get();

        Row.Draw(
            id: id,
            icon: icon,
            iconColor: string.IsNullOrWhiteSpace(current) ? theme.MutedText : theme.Accent,
            title: title,
            subtitle: subtitle,
            controlWidth: controlWidth,
            drawControl: (pos, width) =>
            {
                string value = current;

                theme.PushInputScope();
                if (theme.TextInput($"##{id}-input", pos, width, ref value, maxLength, hint))
                {
                    set(value);
                    Save();
                }
                theme.PopInputScope();
            },
            rowWidth: rowWidth);
    }

    private void DrawOptionRow<T>(
        string id,
        FontAwesomeIcon icon,
        string title,
        string subtitle,
        float rowWidth,
        Func<T> get,
        Action<T> set,
        IReadOnlyList<DropdownItem> options,
        float controlWidth = 220f) where T : struct, Enum
    {
        Row.Draw(
            id: id,
            icon: icon,
            iconColor: theme.Accent,
            title: title,
            subtitle: subtitle,
            controlWidth: controlWidth,
            drawControl: (pos, width) =>
            {
                ImGui.SetCursorScreenPos(pos);
                theme.OptionPicker(
                    $"{id}-picker",
                    get().ToString(),
                    options,
                    value =>
                    {
                        if (!Enum.TryParse<T>(value, out var parsed))
                            return;

                        set(parsed);
                        Save();
                    },
                    width);
            },
            rowWidth: rowWidth);
    }

    private static IReadOnlyList<DropdownItem> Options<T>(params (T Value, string Label)[] entries) where T : struct, Enum
    {
        var list = new List<DropdownItem>(entries.Length);

        foreach (var (value, label) in entries)
            list.Add(new DropdownItem { Key = value.ToString(), Label = label });

        return list;
    }


    private void DrawActionRow(
        string id,
        FontAwesomeIcon icon,
        Vector4 iconColor,
        string title,
        string subtitle,
        string buttonLabel,
        float rowWidth,
        Action onClick,
        float controlWidth = 160f)
    {
        Row.Draw(
            id: id,
            icon: icon,
            iconColor: iconColor,
            title: title,
            subtitle: subtitle,
            controlWidth: controlWidth,
            drawControl: (pos, width) =>
            {
                ImGui.SetCursorScreenPos(pos);

                if (theme.SecondaryButton($"{buttonLabel}##{id}", new Vector2(width, theme.Scaled(UiTheme.ControlHeight))))
                    onClick();
            },
            rowWidth: rowWidth);
    }

    private void DrawInfoRow(
        string id,
        FontAwesomeIcon icon,
        string title,
        string subtitle,
        float rowWidth) =>
        Row.Draw(
            id: id,
            icon: icon,
            iconColor: theme.MutedText,
            title: title,
            subtitle: subtitle,
            rowWidth: rowWidth);

    private void DrawCountChip(Vector2 rightAnchor, string text, Vector4? color = null)
    {
        var size = theme.ChipSize(text);
        theme.ChipAt(new Vector2(rightAnchor.X - size.X, rightAnchor.Y), text, color);
    }
}
