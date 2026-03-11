using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.Core;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Cordi.UI.Tabs;

public abstract class ConfigTabBase
{
    protected readonly CordiPlugin plugin;
    protected readonly UiTheme theme;

    private int selectedSubTab;

    protected ConfigTabBase(CordiPlugin plugin, UiTheme theme)
    {
        this.plugin = plugin;
        this.theme = theme;
    }

    public abstract string Label { get; }

    protected virtual IReadOnlyList<(string Label, Action Draw)>? GetSubTabs() => null;

    public virtual void Draw()
    {
        var subTabs = GetSubTabs();
        if (subTabs == null || subTabs.Count == 0)
            return;

        if (selectedSubTab >= subTabs.Count)
            selectedSubTab = 0;

        theme.SpacerY(1f);

        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, theme.Radius());
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(theme.Gap(0.5f), theme.Gap(0.5f)));

        float btnW = ImGui.GetContentRegionAvail().X / subTabs.Count - theme.Gap(0.5f);
        float btnH = 32f * ImGuiHelpers.GlobalScale * UiTheme.GlobalFontScale;

        for (int i = 0; i < subTabs.Count; i++)
        {
            if (i > 0)
                ImGui.SameLine();

            bool isActive = selectedSubTab == i;
            ImGui.PushStyleColor(ImGuiCol.Button, isActive ? theme.Accent : theme.FrameBg);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, isActive ? theme.Accent : theme.FrameBgHover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, isActive ? theme.Accent : theme.FrameBgActive);

            if (ImGui.Button(subTabs[i].Label, new Vector2(btnW, btnH)))
                selectedSubTab = i;
            theme.HoverHandIfItem();

            ImGui.PopStyleColor(3);
        }

        ImGui.PopStyleVar(2);

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

        ImGui.Indent(theme.Gap());
        subTabs[selectedSubTab].Draw();
        ImGui.Unindent(theme.Gap());
    }
}
