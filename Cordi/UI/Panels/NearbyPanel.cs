using System;
using System.Linq;
using System.Numerics;
using Cordi.Core;
using Cordi.Services.Features;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;

namespace Cordi.UI.Panels;

public class NearbyPanel
{
    private readonly CordiPlugin _plugin;
    private readonly UiTheme _theme = new UiTheme();

    public NearbyPanel(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw()
    {
        var config = _plugin.Config.Nearby;

        if (!config.Enabled)
        {
            ImGui.TextWrapped("Nearby tracking is currently disabled in /cordi configuration.");
            return;
        }

        var peepers = _plugin.NearbyService.NearbyPlayers.Values.ToList();
        if (peepers.Count == 0)
        {
            ImGui.TextDisabled("No nearby players found.");
            return;
        }

        if (ImGui.BeginTable("NearbyTable", 3, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.WidthStretch, 1.5f);
            ImGui.TableSetupColumn("Target", ImGuiTableColumnFlags.WidthStretch, 1.5f);
            ImGui.TableSetupColumn("##Dist", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableHeadersRow();

            var specs = ImGui.TableGetSortSpecs();
            if (specs.SpecsCount > 0)
            {
                var spec = specs.Specs;
                var dir = spec.SortDirection;
                var col = spec.ColumnIndex;

                var localPlayer = Service.ObjectTable.LocalPlayer;
                ulong localId = localPlayer != null ? localPlayer.GameObjectId : 0;

                peepers.Sort((a, b) =>
                {
                    if (config.PrioritizeTargetingMe && localId != 0)
                    {
                        bool aTargetsMe = a.CurrentTargetId == localId;
                        bool bTargetsMe = b.CurrentTargetId == localId;
                        if (aTargetsMe && !bTargetsMe) return -1;
                        if (!aTargetsMe && bTargetsMe) return 1;
                    }

                    int cmp = 0;
                    if (col == 0)
                    {
                        cmp = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                        if (cmp == 0) cmp = a.Distance.CompareTo(b.Distance);
                    }
                    else if (col == 1)
                    {
                        cmp = string.Compare(a.CurrentTargetName ?? "", b.CurrentTargetName ?? "", StringComparison.OrdinalIgnoreCase);
                        if (cmp == 0) cmp = a.Distance.CompareTo(b.Distance);
                    }
                    else
                    {
                        cmp = a.Distance.CompareTo(b.Distance);
                    }

                    if (dir == ImGuiSortDirection.Descending)
                        cmp = -cmp;

                    return cmp;
                });
            }

            foreach (var state in peepers)
            {
                ImGui.TableNextRow();
                DrawEntry(state);
            }
            ImGui.EndTable();
        }
    }

    private void DrawEntry(NearbyService.NearbyState state)
    {
        var style = ImGui.GetStyle();
        var config = _plugin.Config.Nearby;

        bool showArrow = config.ShowDirection;
        bool showDist = config.ShowDistance;
        string distText = showDist ? $"{state.Distance:F1}y  " : "";

        var localPlayer = Service.ObjectTable.LocalPlayer;
        bool isTargetLocalPlayer = state.CurrentTargetId != 0 && localPlayer != null && state.CurrentTargetId == localPlayer.GameObjectId;

        float rowHeight = ImGui.GetTextLineHeight() + style.FramePadding.Y * 2;

        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0f, 0f, 0f, 0f));

        // --- Column 0 : Name
        ImGui.TableNextColumn();

        if (ImGui.Selectable($"##{state.GameObjectId}", false, ImGuiSelectableFlags.SpanAllColumns, new Vector2(0, rowHeight))) { }

        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        if (ImGui.BeginPopupContextItem($"context_{state.GameObjectId}"))
        {
            MenuItem("Copy Name", () => ImGui.SetClipboardText(state.Name));
            MenuItem("Examine", () => Service.CommandManager.ProcessCommand($"/c \"{state.Name}\""));
            ImGui.EndPopup();
        }

        ImGui.SameLine(0, 0);

        var startPos = ImGui.GetCursorScreenPos();
        startPos.Y += style.FramePadding.Y;

        ImGui.SetCursorScreenPos(startPos);
        ImGui.TextUnformatted(state.Name);
        if (!string.IsNullOrEmpty(state.World))
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            ImGui.TextUnformatted($"@{state.World}");
            ImGui.PopStyleColor();
        }

        // --- Column 1: Target
        ImGui.TableNextColumn();
        startPos = ImGui.GetCursorScreenPos();
        startPos.Y += style.FramePadding.Y;

        if (config.ShowCurrentTarget && !string.IsNullOrEmpty(state.CurrentTargetName))
        {
            if (isTargetLocalPlayer)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
                ImGui.SetCursorScreenPos(startPos);
                ImGui.TextUnformatted($"You");
                ImGui.PopStyleColor();
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
                ImGui.SetCursorScreenPos(startPos);
                ImGui.TextUnformatted($"{state.CurrentTargetName}");
                ImGui.PopStyleColor();
            }

            if (ImGui.BeginPopupContextItem($"context_tgt_{state.GameObjectId}"))
            {
                MenuItem("Copy Name", () => ImGui.SetClipboardText(state.CurrentTargetName));
                MenuItem("Examine", () => Service.CommandManager.ProcessCommand($"/c \"{state.CurrentTargetName}\""));
                ImGui.EndPopup();
            }
        }

        // --- Column 2: Distance/Dir
        ImGui.TableNextColumn();
        startPos = ImGui.GetCursorScreenPos();
        startPos.Y += style.FramePadding.Y;

        var drawList = ImGui.GetWindowDrawList();

        float rightAlignedOffset = 0f;
        if (showArrow) rightAlignedOffset += 20f;
        if (showDist) rightAlignedOffset += ImGui.CalcTextSize(distText).X;

        float rightX = startPos.X + ImGui.GetContentRegionAvail().X - rightAlignedOffset;
        if (rightX < startPos.X) rightX = startPos.X;

        if (showDist) drawList.AddText(new Vector2(rightX, startPos.Y), ImGui.GetColorU32(ImGuiCol.Text), distText);

        if (showArrow)
        {
            float arrowSize = ImGui.GetTextLineHeight() * 0.45f;
            float arrowX = rightX + (showDist ? ImGui.CalcTextSize(distText).X : 0);
            var arrowCenter = new Vector2(arrowX + arrowSize + 1f, startPos.Y + ImGui.GetTextLineHeight() * 0.5f);
            DrawArrow(drawList, arrowCenter, arrowSize, state.DirectionAngle, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        }

        ImGui.PopStyleColor(2);
    }

    private void MenuItem(string label, Action onClick)
    {
        if (ImGui.MenuItem(label)) onClick?.Invoke();
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
    }

    private void DrawArrow(ImDrawListPtr drawList, Vector2 center, float size, float angle, uint color)
    {
        float screenAngle = angle - MathF.PI / 2f;
        var dir = new Vector2(MathF.Cos(screenAngle), MathF.Sin(screenAngle));
        var tip = center + dir * size;
        var baseCenter = center - dir * size * 0.4f;
        var perp = new Vector2(-dir.Y, dir.X);

        var left = baseCenter + perp * size * 0.6f;
        var right = baseCenter - perp * size * 0.6f;

        drawList.AddTriangleFilled(tip, left, right, color);
    }
}
