using System;
using System.Numerics;
using Cordi.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Cordi.Services.Features;

using Cordi.Configuration;
using Cordi.Core;
using Cordi.Extensions;
using Cordi.UI.Themes;

namespace Cordi.UI.Panels;

public class CordiPeepPanel
{
    private readonly CordiPlugin _plugin;

    private ulong? _hoveredPeeperId;
    private string? _hoveredPeeperName;
    private string? _hoveredPeeperWorld;

    private ulong? _hoveredTargetId;
    private string? _hoveredTargetName;

    private ulong? _lastHoveredPeeperId;
    private ulong? _lastHoveredTargetId;

    private readonly UiTheme _theme;

    public CordiPeepPanel(CordiPlugin plugin, UiTheme theme)
    {
        _plugin = plugin;
        _theme = theme;
    }

    public void Draw(bool textShadow = false)
    {
        _hoveredPeeperId = null;
        _hoveredPeeperName = null;
        _hoveredPeeperWorld = null;
        _hoveredTargetId = null;
        _hoveredTargetName = null;

        var cordiPeep = _plugin.CordiPeep;
        if (cordiPeep == null) return;

        if (cordiPeep.ActivePeepers.IsEmpty && cordiPeep.History.Count == 0)
        {
            _theme.ShadowedText("No detected peeps.", textShadow, true);
        }
        else
        {
            foreach (var peeper in cordiPeep.ActivePeepers.Values)
            {
                string label = $"{peeper.Name}";
                string time = peeper.LastSeen.ToString("HH:mm");

                DrawEntry(peeper, label, time, true, textShadow);
            }

            lock (cordiPeep.History)
            {
                foreach (var peeper in cordiPeep.History)
                {
                    string label = $"{peeper.Name}";
                    string time = peeper.EndTime.HasValue ? peeper.EndTime.Value.ToString("HH:mm") : "Unknown";

                    DrawEntry(peeper, label, time, false, textShadow);
                }
            }
        }

        if (_plugin.Config.CordiPeep.FocusOnHover)
        {
            // Determine which ID and name to focus: prefer hoveredTarget, fallback to peeper itself
            var focusId = _hoveredTargetId ?? _hoveredPeeperId;
            var focusName = _hoveredTargetId.HasValue ? _hoveredTargetName : _hoveredPeeperName;
            var focusWorld = _hoveredTargetId.HasValue ? null : _hoveredPeeperWorld;

            var lastFocusId = _lastHoveredTargetId ?? _lastHoveredPeeperId;

            if (focusId != lastFocusId)
            {
                bool focusSet = false;

                if (focusId.HasValue)
                {
                    var obj = FindGameObject(focusId.Value, focusName, focusWorld);
                    if (obj != null)
                    {
                        if (obj is IPlayerCharacter pc)
                        {
                            VisibilityBridge.UnhidePlayer(pc, allowVoided: true, isEmote: false);
                        }
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

            _lastHoveredPeeperId = _hoveredPeeperId;
            _lastHoveredTargetId = _hoveredTargetId;
        }
    }

    private void DrawEntry(CordiPeepService.PeeperState peeper, string label, string rightText, bool isActive, bool textShadow)
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

        using var headerHoveredRaii = ImRaii.PushColor(ImGuiCol.HeaderHovered, new Vector4(0f, 0f, 0f, 0f));
        using var headerActiveRaii = ImRaii.PushColor(ImGuiCol.HeaderActive, new Vector4(0f, 0f, 0f, 0f));

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

        // Context menu
        using (var popup = ImRaii.ContextPopupItem($"ctx_{peeper.GameObjectId}_{peeper.StartTime.Ticks}"))
        {
            if (popup)
            {
                if (MenuItem("Target"))
                {
                    var obj = FindPeeper(peeper);
                    if (obj is IPlayerCharacter pc)
                    {
                        VisibilityBridge.UnhidePlayer(pc, allowVoided: true, isEmote: false);
                    }
                    if (obj != null) Service.TargetManager.Target = obj;
                }
                if (MenuItem("Focus Target"))
                {
                    var obj = FindPeeper(peeper);
                    if (obj is IPlayerCharacter pc)
                    {
                        VisibilityBridge.UnhidePlayer(pc, allowVoided: true, isEmote: false);
                    }
                    if (obj != null) Service.TargetManager.FocusTarget = obj;
                }
                if (MenuItem("Examine"))
                {
                    var obj = FindPeeper(peeper);
                    if (obj is IPlayerCharacter pc)
                    {
                        VisibilityBridge.UnhidePlayer(pc, allowVoided: true, isEmote: false);
                    }
                    if (obj != null) Examine(obj.GameObjectId);
                }
                if (MenuItem("Adventure Plate"))
                {
                    var obj = FindPeeper(peeper);
                    if (obj is IPlayerCharacter pc)
                    {
                        VisibilityBridge.UnhidePlayer(pc, allowVoided: true, isEmote: false);
                    }
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
            }
        }

        bool targetHovered = false;
        Vector2 targetItemMin = Vector2.Zero;

        if (showTarget)
        {
            float targetRowHeight = ImGui.GetTextLineHeight();

            using var targetHeaderHoveredRaii = ImRaii.PushColor(ImGuiCol.HeaderHovered, new Vector4(0f, 0f, 0f, 0f));
            using var targetHeaderActiveRaii = ImRaii.PushColor(ImGuiCol.HeaderActive, new Vector4(0f, 0f, 0f, 0f));

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

            using (var popup = ImRaii.ContextPopupItem($"ctx_target_{peeper.GameObjectId}_{peeper.StartTime.Ticks}"))
            {
                if (popup)
                {
                    var targetName = peeper.CurrentTargetName ?? "Target";
                    if (MenuItem($"Target: {targetName}"))
                    {
                        var tObj = Service.ObjectTable.SearchById(peeper.CurrentTargetId);
                        if (tObj is IPlayerCharacter pc)
                        {
                            VisibilityBridge.UnhidePlayer(pc, allowVoided: true, isEmote: false);
                        }
                        if (tObj != null) Service.TargetManager.Target = tObj;
                    }
                    if (MenuItem($"Focus: {targetName}"))
                    {
                        var tObj = Service.ObjectTable.SearchById(peeper.CurrentTargetId);
                        if (tObj is IPlayerCharacter pc)
                        {
                            VisibilityBridge.UnhidePlayer(pc, allowVoided: true, isEmote: false);
                        }
                        if (tObj != null) Service.TargetManager.FocusTarget = tObj;
                    }
                    if (MenuItem($"Examine: {targetName}"))
                    {
                        var tObj = Service.ObjectTable.SearchById(peeper.CurrentTargetId);
                        if (tObj is IPlayerCharacter pc)
                        {
                            VisibilityBridge.UnhidePlayer(pc, allowVoided: true, isEmote: false);
                        }
                        if (tObj != null) Examine(tObj.GameObjectId);
                    }
                    if (MenuItem($"Plate: {targetName}"))
                    {
                        var tObj = Service.ObjectTable.SearchById(peeper.CurrentTargetId);
                        if (tObj is IPlayerCharacter pc)
                        {
                            VisibilityBridge.UnhidePlayer(pc, allowVoided: true, isEmote: false);
                        }
                        if (tObj != null) OpenAdventurePlate(tObj.GameObjectId);
                    }
                }
            }
        }

        if (config.FocusOnHover)
        {
            if (peeperHovered)
            {
                _hoveredPeeperId = peeper.GameObjectId;
                _hoveredPeeperName = peeper.Name;
                _hoveredPeeperWorld = peeper.World;
            }
            if (targetHovered && peeper.CurrentTargetId != 0)
            {
                _hoveredTargetId = peeper.CurrentTargetId;
                _hoveredTargetName = peeper.CurrentTargetName;
            }
        }

        var pMin = peeperItemMin;
        var pMax = peeperItemMax;
        var drawList = ImGui.GetWindowDrawList();

        uint textColor;
        if (isActive)
        {
            textColor = ImGui.GetColorU32(_plugin.Config.CordiPeep.TargetingHighlightColor);
        }
        else
        {
            textColor = ImGui.GetColorU32(ImGuiCol.TextDisabled);
        }

        float textX = pMin.X + style.ItemSpacing.X;
        float textY = pMin.Y + style.FramePadding.Y;
        float lineH = ImGui.GetTextLineHeight();

        var mutedColor = ImGui.GetColorU32(ImGuiCol.TextDisabled);

        if (showArrow)
        {
            float arrowSize = lineH * 0.45f;
            var arrowCenter = new Vector2(textX + arrowSize + 1f, textY + lineH * 0.5f);

            if (textShadow)
                _theme.DirectionArrow(
                    drawList,
                    arrowCenter + UiTheme.TextShadowOffset,
                    arrowSize,
                    peeper.DirectionAngle,
                    ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.8f)));

            _theme.DirectionArrow(drawList, arrowCenter, arrowSize, peeper.DirectionAngle, mutedColor);
            textX += arrowSize * 2f + 6f;
        }

        if (distText.Length > 0)
        {
            _theme.ShadowedTextAt(drawList, new Vector2(textX, textY), distText, mutedColor, textShadow);
            textX += ImGui.CalcTextSize(distText).X;
        }

        var labelPos = new Vector2(textX, textY);

        if (textShadow) _theme.TextShadowAt(drawList, labelPos, label);

        if (isActive && config.TargetingGlowEnabled && config.TargetingGlowThickness > 0f)
            _theme.TextGlow(drawList, labelPos, label, config.TargetingGlowColor, config.TargetingGlowThickness);

        _theme.ShadowedTextAt(drawList, labelPos, label, textColor, false);

        var timeSize = ImGui.CalcTextSize(rightText);
        var timePos = new Vector2(pMax.X - timeSize.X - style.ItemSpacing.X, textY);
        _theme.ShadowedTextAt(drawList, timePos, rightText, mutedColor, textShadow);

        if (showTarget)
        {
            var targetText = $"  \u2192 {peeper.CurrentTargetName}";
            var targetPos = new Vector2(targetItemMin.X + style.ItemSpacing.X, targetItemMin.Y);
            _theme.ShadowedTextAt(drawList, targetPos, targetText, mutedColor, textShadow);
        }
    }

    private Dalamud.Game.ClientState.Objects.Types.IGameObject? FindPeeper(CordiPeepService.PeeperState peeper)
    {
        var obj = Service.ObjectTable.SearchById(peeper.GameObjectId);
        obj ??= Service.ObjectTable.FindPlayerByName(peeper.Name, peeper.World);
        return obj;
    }

    private Dalamud.Game.ClientState.Objects.Types.IGameObject? FindGameObject(ulong id, string? name, string? world)
    {
        var obj = Service.ObjectTable.SearchById(id);
        if (obj == null && !string.IsNullOrEmpty(name))
        {
            obj = Service.ObjectTable.FindPlayerByName(name, world);
        }
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
