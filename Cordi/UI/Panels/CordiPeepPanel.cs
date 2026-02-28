using System;
using System.Numerics;
using Cordi.Services;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

using Cordi.Core;
using Cordi.Extensions;

namespace Cordi.UI.Panels;

public class CordiPeepPanel
{
    private readonly CordiPlugin _plugin;
    private ulong? _lastHoveredPeeper;
    private ulong? _hoveredPeeperThisFrame;

    public CordiPeepPanel(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw()
    {
        _hoveredPeeperThisFrame = null;

        var cordiPeep = _plugin.CordiPeep;
        if (cordiPeep == null) return;

        if (!cordiPeep.ActivePeepers.IsEmpty)
        {
            ImGui.TextColored(new Vector4(1f, 0.5f, 0.5f, 1f), "Targeting you");
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"Players actively targeting you right now.\nRight-Click to target.\n{(_plugin.Config.CordiPeep.AltClickExamine ? "Alt-Click to examine." : "")}");
            }

            foreach (var peeper in cordiPeep.ActivePeepers.Values)
            {
                string label = $"{peeper.Name}";
                string time = peeper.LastSeen.ToString("HH:mm");

                DrawEntry(peeper, label, time, true);
            }
            ImGui.Separator();
        }

        if (cordiPeep.History.Count > 0)
        {
            ImGui.TextDisabled("History");
        }

        lock (cordiPeep.History)
        {
            if (cordiPeep.History.Count == 0 && cordiPeep.ActivePeepers.IsEmpty)
            {
                ImGui.TextDisabled("No detected peeps.");
            }
            else
            {
                foreach (var peeper in cordiPeep.History)
                {
                    string label = $"{peeper.Name}";
                    string time = peeper.EndTime.HasValue ? peeper.EndTime.Value.ToString("HH:mm") : "Unknown";

                    DrawEntry(peeper, label, time, false);
                }
            }
        }

        if (_plugin.Config.CordiPeep.FocusOnHover)
        {
            if (_hoveredPeeperThisFrame != _lastHoveredPeeper)
            {
                if (_hoveredPeeperThisFrame.HasValue)
                {
                    var obj = Service.ObjectTable.SearchById(_hoveredPeeperThisFrame.Value);
                    if (obj != null)
                    {
                        Service.TargetManager.FocusTarget = obj;
                    }
                }
                else
                {
                    var currentFocus = Service.TargetManager.FocusTarget;
                    if (currentFocus != null && currentFocus.GameObjectId == _lastHoveredPeeper)
                    {
                        Service.TargetManager.FocusTarget = null;
                    }
                }
                _lastHoveredPeeper = _hoveredPeeperThisFrame;
            }
        }
    }

    private void DrawEntry(CordiPeepService.PeeperState peeper, string label, string rightText, bool isActive)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        var style = ImGui.GetStyle();

        if (isActive)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.5f, 0.5f, 1f));
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        }

        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0f, 0f, 0f, 0f));

        if (ImGui.Selectable($"##{peeper.GameObjectId}_{peeper.StartTime.Ticks}", false, ImGuiSelectableFlags.None))
        {
            if (ImGui.GetIO().KeyAlt && _plugin.Config.CordiPeep.AltClickExamine)
            {
                var obj = FindPeeper(peeper);
                if (obj != null)
                {
                    Examine(obj.GameObjectId);
                }
            }
        }

        ImGui.PopStyleColor(2);
        ImGui.PopStyleColor();

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            var obj = FindPeeper(peeper);
            if (obj != null)
            {
                Service.TargetManager.Target = obj;
            }
        }

        if (ImGui.IsItemHovered())
        {
            if (_plugin.Config.CordiPeep.FocusOnHover)
            {
                _hoveredPeeperThisFrame = peeper.GameObjectId;
            }
        }

        var pMin = ImGui.GetItemRectMin();
        var pMax = ImGui.GetItemRectMax();
        var drawList = ImGui.GetWindowDrawList();

        uint textColor;
        if (isActive)
        {
            textColor = ImGui.GetColorU32(ImGuiCol.Text);
        }
        else
        {
            textColor = ImGui.GetColorU32(ImGuiCol.TextDisabled);
        }

        drawList.AddText(new Vector2(pMin.X + style.ItemSpacing.X, pMin.Y + style.FramePadding.Y), textColor, label);

        var timeSize = ImGui.CalcTextSize(rightText);
        drawList.AddText(new Vector2(pMax.X - timeSize.X - style.ItemSpacing.X, pMin.Y + style.FramePadding.Y), ImGui.GetColorU32(ImGuiCol.TextDisabled), rightText);
    }

    private Dalamud.Game.ClientState.Objects.Types.IGameObject? FindPeeper(CordiPeepService.PeeperState peeper)
    {
        var obj = Service.ObjectTable.SearchById(peeper.GameObjectId);
        obj ??= Service.ObjectTable.FindPlayerByName(peeper.Name, peeper.World);
        return obj;
    }

    private unsafe void Examine(ulong objectId)
    {
        var agent = AgentInspect.Instance();
        if (agent != null) agent->ExamineCharacter((uint)objectId);
    }
}
