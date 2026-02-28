using System;
using System.Numerics;
using Cordi.Services;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

using Cordi.Configuration;
using Cordi.Core;
using Cordi.Extensions;

namespace Cordi.UI.Panels;

public class CordiPeepPanel
{
    private readonly CordiPlugin _plugin;
    private ulong? _lastHoveredPeeper;
    private ulong? _hoveredPeeperThisFrame;
    private ulong? _lastHoveredTarget;
    private ulong? _hoveredTargetThisFrame;

    public CordiPeepPanel(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw()
    {
        _hoveredPeeperThisFrame = null;
        _hoveredTargetThisFrame = null;

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
            // Determine which ID to focus: prefer hoveredTarget (peeper's target), fallback to peeper itself
            var focusId = _hoveredTargetThisFrame ?? _hoveredPeeperThisFrame;
            var lastFocusId = _lastHoveredTarget ?? _lastHoveredPeeper;

            if (focusId != lastFocusId)
            {
                bool focusSet = false;

                if (focusId.HasValue)
                {
                    var obj = Service.ObjectTable.SearchById(focusId.Value);
                    if (obj != null)
                    {
                        Service.TargetManager.FocusTarget = obj;
                        focusSet = true;
                    }
                }

                if (!focusSet)
                {
                    var currentFocus = Service.TargetManager.FocusTarget;
                    if (currentFocus != null && lastFocusId.HasValue && currentFocus.GameObjectId == lastFocusId.Value)
                    {
                        Service.TargetManager.FocusTarget = null;
                    }
                }
            }

            _lastHoveredPeeper = _hoveredPeeperThisFrame;
            _lastHoveredTarget = _hoveredTargetThisFrame;
        }
    }

    private void DrawEntry(CordiPeepService.PeeperState peeper, string label, string rightText, bool isActive)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        var style = ImGui.GetStyle();
        var config = _plugin.Config.CordiPeep;

        // Build distance text and direction
        bool showArrow = (isActive && config.ShowDirection) || (!isActive && config.ShowDirectionInHistory && peeper.IsPresent);

        bool showDist = (isActive && config.ShowDistance) || (!isActive && config.ShowDistanceInHistory && peeper.IsPresent);
        string distText = showDist ? $"{peeper.Distance:F1}y  " : "";

        var localPlayer = Service.ObjectTable.LocalPlayer;
        bool isTargetLocalPlayer = peeper.CurrentTargetId != 0 && localPlayer != null && peeper.CurrentTargetId == localPlayer.GameObjectId;

        // Show current target for both active and history peepers
        bool showTarget = config.ShowCurrentTarget && !string.IsNullOrEmpty(peeper.CurrentTargetName) && !isTargetLocalPlayer;
        float rowHeight = ImGui.GetTextLineHeight() + style.FramePadding.Y * 2;

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

        if (ImGui.Selectable($"##{peeper.GameObjectId}_{peeper.StartTime.Ticks}", false, ImGuiSelectableFlags.None, new Vector2(0, rowHeight)))
        {
            if (ImGui.GetIO().KeyAlt && config.AltClickExamine)
            {
                var obj = FindPeeper(peeper);
                if (obj != null)
                {
                    Examine(obj.GameObjectId);
                }
            }
        }

        var peeperHovered = ImGui.IsItemHovered();
        if (peeperHovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var peeperItemMin = ImGui.GetItemRectMin();
        var peeperItemMax = ImGui.GetItemRectMax();

        ImGui.PopStyleColor(2);
        ImGui.PopStyleColor();

        // Context menu
        if (ImGui.BeginPopupContextItem($"ctx_{peeper.GameObjectId}_{peeper.StartTime.Ticks}"))
        {
            if (MenuItem("Target"))
            {
                var obj = FindPeeper(peeper);
                if (obj != null) Service.TargetManager.Target = obj;
            }
            if (MenuItem("Focus Target"))
            {
                var obj = FindPeeper(peeper);
                if (obj != null) Service.TargetManager.FocusTarget = obj;
            }
            if (MenuItem("Examine"))
            {
                var obj = FindPeeper(peeper);
                if (obj != null) Examine(obj.GameObjectId);
            }
            if (MenuItem("Adventure Plate"))
            {
                var obj = FindPeeper(peeper);
                if (obj != null) OpenAdventurePlate(obj.GameObjectId);
            }

            ImGui.Separator();
            var isBlacklisted = config.Blacklist.Exists(x => x.Name == peeper.Name && x.World == peeper.World);
            if (!isBlacklisted && MenuItem("Blacklist"))
            {
                config.Blacklist.Add(new Configuration.CordiPeepBlacklistEntry
                {
                    Name = peeper.Name,
                    World = peeper.World,
                    DisableSound = true,
                    DisableDiscord = true,
                });
                _plugin.Config.Save();
            }
            ImGui.EndPopup();
        }

        bool targetHovered = false;
        Vector2 targetItemMin = Vector2.Zero;

        if (showTarget)
        {
            float targetRowHeight = ImGui.GetTextLineHeight();

            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0f, 0f, 0f, 0f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0f, 0f, 0f, 0f));

            if (ImGui.Selectable($"##target_{peeper.GameObjectId}_{peeper.StartTime.Ticks}", false, ImGuiSelectableFlags.None, new Vector2(0, targetRowHeight)))
            {
                if (ImGui.GetIO().KeyAlt && config.AltClickExamine)
                {
                    var tObj = Service.ObjectTable.SearchById(peeper.CurrentTargetId);
                    if (tObj != null) Examine(tObj.GameObjectId);
                }
            }

            targetHovered = ImGui.IsItemHovered();
            if (targetHovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            targetItemMin = ImGui.GetItemRectMin();

            ImGui.PopStyleColor(2);

            if (ImGui.BeginPopupContextItem($"ctx_target_{peeper.GameObjectId}_{peeper.StartTime.Ticks}"))
            {
                var targetName = peeper.CurrentTargetName ?? "Target";
                if (MenuItem($"Target: {targetName}"))
                {
                    var tObj = Service.ObjectTable.SearchById(peeper.CurrentTargetId);
                    if (tObj != null) Service.TargetManager.Target = tObj;
                }
                if (MenuItem($"Focus: {targetName}"))
                {
                    var tObj = Service.ObjectTable.SearchById(peeper.CurrentTargetId);
                    if (tObj != null) Service.TargetManager.FocusTarget = tObj;
                }
                if (MenuItem($"Examine: {targetName}"))
                {
                    var tObj = Service.ObjectTable.SearchById(peeper.CurrentTargetId);
                    if (tObj != null) Examine(tObj.GameObjectId);
                }
                if (MenuItem($"Plate: {targetName}"))
                {
                    var tObj = Service.ObjectTable.SearchById(peeper.CurrentTargetId);
                    if (tObj != null) OpenAdventurePlate(tObj.GameObjectId);
                }
                ImGui.EndPopup();
            }
        }

        if (config.FocusOnHover)
        {
            if (peeperHovered)
            {
                _hoveredPeeperThisFrame = peeper.GameObjectId;
            }
            if (targetHovered && peeper.CurrentTargetId != 0)
            {
                _hoveredTargetThisFrame = peeper.CurrentTargetId;
            }
        }

        var pMin = peeperItemMin;
        var pMax = peeperItemMax;
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

        // Draw direction arrow + distance + label
        float textX = pMin.X + style.ItemSpacing.X;
        float textY = pMin.Y + style.FramePadding.Y;
        float lineH = ImGui.GetTextLineHeight();

        if (showArrow)
        {
            float arrowSize = lineH * 0.45f;
            var arrowCenter = new Vector2(textX + arrowSize + 1f, textY + lineH * 0.5f);
            DrawDirectionTriangle(drawList, arrowCenter, arrowSize, peeper.DirectionAngle, ImGui.GetColorU32(ImGuiCol.TextDisabled));
            textX += arrowSize * 2f + 6f;
        }

        if (distText.Length > 0)
        {
            drawList.AddText(new Vector2(textX, textY), ImGui.GetColorU32(ImGuiCol.TextDisabled), distText);
            textX += ImGui.CalcTextSize(distText).X;
        }

        drawList.AddText(new Vector2(textX, textY), textColor, label);

        var timeSize = ImGui.CalcTextSize(rightText);
        drawList.AddText(new Vector2(pMax.X - timeSize.X - style.ItemSpacing.X, textY), ImGui.GetColorU32(ImGuiCol.TextDisabled), rightText);

        // Draw current target on second line
        if (showTarget)
        {
            var targetText = $"  \u2192 {peeper.CurrentTargetName}";
            drawList.AddText(new Vector2(targetItemMin.X + style.ItemSpacing.X, targetItemMin.Y), ImGui.GetColorU32(ImGuiCol.TextDisabled), targetText);
        }
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

    private unsafe void OpenAdventurePlate(ulong objectId)
    {
        var agent = AgentCharaCard.Instance();
        if (agent != null)
        {
            var obj = Service.ObjectTable.SearchById(objectId);
            if (obj != null)
            {
                agent->OpenCharaCard((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)obj.Address);
            }
        }
    }

    private static void DrawDirectionTriangle(ImDrawListPtr drawList, Vector2 center, float size, float angle, uint color)
    {
        // angle: 0 = front, PI/2 = right, PI = behind, -PI/2 = left
        // Screen coords: Y increases downward, so up = -Y
        // angle 0 (front/up) → screen direction (0, -1) → screenAngle = -PI/2
        float screenAngle = angle - MathF.PI / 2f;

        // Tip points AWAY from player center, toward the peeper's direction
        var dir = new Vector2(MathF.Cos(screenAngle), MathF.Sin(screenAngle));
        var tip = center + dir * size;
        var baseCenter = center - dir * size * 0.4f;
        var perp = new Vector2(-dir.Y, dir.X);
        var left = baseCenter + perp * size * 0.5f;
        var right = baseCenter - perp * size * 0.5f;

        drawList.AddTriangleFilled(tip, left, right, color);
    }

    private static bool MenuItem(string label)
    {
        bool clicked = ImGui.MenuItem(label);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        return clicked;
    }
}
