using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.Core;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Tabs;

public abstract class ConfigTabBase
{
    protected readonly CordiPlugin plugin;
    protected readonly UiTheme theme;

    private SettingsRow? rowRenderer;
    private PageHeader? layoutRenderer;
    private Panel? panelRenderer;

    protected SettingsRow Row => rowRenderer ??= new SettingsRow(theme);
    protected PageHeader Layout => layoutRenderer ??= new PageHeader(theme);
    protected Panel Card => panelRenderer ??= new Panel(theme);

    private int selectedSubTab;

    protected ConfigTabBase(CordiPlugin plugin, UiTheme theme)
    {
        this.plugin = plugin;
        this.theme = theme;
    }

    public abstract string Label { get; }

    protected virtual IReadOnlyList<(string Label, Action Draw)>? GetSubTabs() => null;

    public IReadOnlyList<(string Label, Action Draw)>? SubTabs => GetSubTabs();

    public void SelectSubTab(string? label)
    {
        if (string.IsNullOrEmpty(label))
            return;

        var subTabs = GetSubTabs();
        if (subTabs == null)
            return;

        for (int i = 0; i < subTabs.Count; i++)
        {
            if (string.Equals(subTabs[i].Label, label, StringComparison.OrdinalIgnoreCase))
            {
                selectedSubTab = i;
                return;
            }
        }
    }

    public virtual void Draw()
    {
        var subTabs = GetSubTabs();
        if (subTabs == null || subTabs.Count == 0)
            return;

        if (selectedSubTab >= subTabs.Count)
            selectedSubTab = 0;

        theme.SpacerY(1f);

        using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, theme.Radius()))
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(theme.Gap(0.5f), theme.Gap(0.5f))))
        {
            float btnW = ImGui.GetContentRegionAvail().X / subTabs.Count - theme.Gap(0.5f);
            float btnH = 32f * ImGuiHelpers.GlobalScale * UiTheme.GlobalFontScale;

            for (int i = 0; i < subTabs.Count; i++)
            {
                if (i > 0)
                    ImGui.SameLine();

                bool isActive = selectedSubTab == i;
                using (ImRaii.PushColor(ImGuiCol.Button, isActive ? theme.Accent : theme.FrameBg))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, isActive ? theme.Accent : theme.FrameBgHover))
                using (ImRaii.PushColor(ImGuiCol.ButtonActive, isActive ? theme.Accent : theme.FrameBgActive))
                {
                    if (ImGui.Button(subTabs[i].Label, new Vector2(btnW, btnH)))
                        selectedSubTab = i;
                    theme.HoverHandIfItem();
                }
            }
        }

        theme.SpacerY(1f);

        {
            var draw = ImGui.GetWindowDrawList();
            var cursor = ImGui.GetCursorScreenPos();
            float availW = ImGui.GetContentRegionAvail().X;
            float radius = theme.Radius();
            float thickness = 1f * ImGuiHelpers.GlobalScale;
            uint col = ImGui.GetColorU32(theme.WindowBorder);

            float lineY = cursor.Y;
            float leftX = cursor.X;
            float rightX = cursor.X + availW;
            float bottomY = cursor.Y + ImGui.GetContentRegionAvail().Y;

            draw.PathClear();
            draw.PathLineTo(new Vector2(leftX, bottomY));
            draw.PathLineTo(new Vector2(leftX, lineY + radius));
            draw.PathArcTo(new Vector2(leftX + radius, lineY + radius),
                           radius, MathF.PI, MathF.PI * 1.5f, 12);
            draw.PathLineTo(new Vector2(rightX, lineY));
            draw.PathStroke(col, ImDrawFlags.None, thickness);
        }

        theme.SpacerY(1f);

        using (ImRaii.PushIndent(theme.Gap()))
        {
            subTabs[selectedSubTab].Draw();
        }
    }

    protected void Save() => plugin.Config.Save();

    protected void DrawToggleRow(
        string id,
        FontAwesomeIcon icon,
        Vector4 iconColor,
        string title,
        string subtitle,
        float rowWidth,
        Func<bool> get,
        Action<bool> set)
    {
        bool value = get();

        var result = Row.Draw(
            id: id,
            icon: icon,
            iconColor: iconColor,
            title: title,
            subtitle: subtitle,
            toggleValue: value,
            onToggle: newValue =>
            {
                set(newValue);
                Save();
            },
            rowWidth: rowWidth);

        if (result.RowClicked && !result.ToggleChanged)
        {
            set(!value);
            Save();
        }
    }

    protected void DrawSliderRow(
        string id,
        FontAwesomeIcon icon,
        Vector4 iconColor,
        string title,
        string subtitle,
        float rowWidth,
        float min,
        float max,
        float step,
        Func<float> get,
        Action<float> set,
        int decimals = 0,
        string suffix = "")
    {
        Row.Draw(
            id: id,
            icon: icon,
            iconColor: iconColor,
            title: title,
            subtitle: subtitle,
            controlWidth: 260f,
            drawControl: (pos, width) =>
            {
                float value = get();

                if (!theme.SliderControl($"##{id}-slider", pos, width, ref value, min, max, step, decimals, suffix))
                    return;

                set(value);
                Save();
            },
            rowWidth: rowWidth);
    }

    protected void DrawPercentRow(
        string id,
        FontAwesomeIcon icon,
        Vector4 iconColor,
        string title,
        string subtitle,
        float rowWidth,
        Func<float> get,
        Action<float> set) =>
        DrawSliderRow(
            id, icon, iconColor, title, subtitle, rowWidth,
            0f, 100f, 1f,
            () => get() * 100f,
            value => set(value / 100f),
            suffix: "%");

    protected void DrawNumberRow(
        string id,
        FontAwesomeIcon icon,
        Vector4 iconColor,
        string title,
        string subtitle,
        float rowWidth,
        int min,
        int max,
        Func<int> get,
        Action<int> set)
    {
        Row.Draw(
            id: id,
            icon: icon,
            iconColor: iconColor,
            title: title,
            subtitle: subtitle,
            controlWidth: 90f,
            drawControl: (pos, width) =>
            {
                int value = get();
                if (theme.NumberInput($"##{id}-value", pos, width, ref value, min, max))
                {
                    set(value);
                    Save();
                }
            },
            rowWidth: rowWidth);
    }

    protected void DrawColorRow(
        string id,
        string title,
        string subtitle,
        float rowWidth,
        Func<Vector4> get,
        Action<Vector4> set)
    {
        var color = get();

        Row.Draw(
            id: id,
            icon: FontAwesomeIcon.Palette,
            iconColor: color,
            title: title,
            subtitle: subtitle,
            controlWidth: 90f,
            drawControl: (pos, width) =>
            {
                var edited = color;

                if (!theme.ColorSwatch($"##{id}-swatch", pos, width, ref edited))
                    return;

                set(edited);
                Save();
            },
            rowWidth: rowWidth);
    }

    protected static void BrowseForSound(Action<string> onPicked)
    {
        var thread = new System.Threading.Thread(() =>
        {
            using var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Audio Files|*.wav;*.mp3|All files|*.*",
                CheckFileExists = true,
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                onPicked(dialog.FileName);
        });

        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
    }

    protected void DrawButtonRow(
        string id,
        FontAwesomeIcon icon,
        Vector4 iconColor,
        string title,
        string subtitle,
        string buttonLabel,
        float rowWidth,
        Action onClick,
        float controlWidth = 150f)
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
                float buttonHeight = theme.Scaled(32f);
                ImGui.SetCursorScreenPos(
                    new Vector2(pos.X, pos.Y + (theme.Scaled(UiTheme.ControlHeight) - buttonHeight) * 0.5f));

                if (theme.SecondaryButton(buttonLabel, new Vector2(width, buttonHeight)))
                    onClick();
            },
            rowWidth: rowWidth);
    }
}
