using System;
using System.Collections.Generic;
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

public class PeeperPanel
{
    private readonly Configuration.Configuration _config;
    private readonly PeeperService _peeper;

    private ulong? _hoveredPeeperId;
    private string? _hoveredPeeperName;
    private string? _hoveredPeeperWorld;

    private ulong? _hoveredTargetId;
    private string? _hoveredTargetName;

    private ulong? _lastHoveredPeeperId;
    private ulong? _lastHoveredTargetId;

    private readonly UiTheme _theme;

    public PeeperPanel(Configuration.Configuration config, PeeperService peeper, UiTheme theme)
    {
        _config = config;
        _peeper = peeper;
        _theme = theme;
    }

    public void Draw(bool textShadow = false)
    {
        _hoveredPeeperId = null;
        _hoveredPeeperName = null;
        _hoveredPeeperWorld = null;
        _hoveredTargetId = null;
        _hoveredTargetName = null;

        var snapshot = _peeper.Snapshot;

        if (snapshot.IsEmpty)
        {
            _theme.ShadowedText("No detected peeps.", textShadow, true);
        }
        else
        {
            float rightEdge = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;

            foreach (var peeper in snapshot.Active)
            {
                DrawEntry(peeper, true, rightEdge, textShadow);
            }

            foreach (var peeper in snapshot.History)
            {
                DrawEntry(peeper, false, rightEdge, textShadow);
            }
        }

        if (_config.Peeper.FocusOnHover)
        {
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
                            _peeper.Visibility.Unhide(pc);
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

    private void DrawEntry(PeeperState peeper, bool isActive, float rightEdge, bool textShadow)
    {
        var style = ImGui.GetStyle();
        var config = _config.Peeper;

        var left = new List<RowSegment>();
        var right = new List<RowSegment>();
        var second = new List<RowSegment>();

        bool showTarget = BuildSegments(peeper, isActive, textShadow, left, right, second);

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

        using (var popup = ImRaii.ContextPopupItem($"ctx_{peeper.GameObjectId}_{peeper.StartTime.Ticks}"))
        {
            if (popup)
            {
                if (MenuItem("Target"))
                {
                    var obj = FindPeeper(peeper);
                    if (obj is IPlayerCharacter pc)
                    {
                        _peeper.Visibility.Unhide(pc);
                    }
                    if (obj != null) Service.TargetManager.Target = obj;
                }
                if (MenuItem("Focus Target"))
                {
                    var obj = FindPeeper(peeper);
                    if (obj is IPlayerCharacter pc)
                    {
                        _peeper.Visibility.Unhide(pc);
                    }
                    if (obj != null) Service.TargetManager.FocusTarget = obj;
                }
                if (MenuItem("Examine"))
                {
                    var obj = FindPeeper(peeper);
                    if (obj is IPlayerCharacter pc)
                    {
                        _peeper.Visibility.Unhide(pc);
                    }
                    if (obj != null) Examine(obj.GameObjectId);
                }
                if (MenuItem("Adventure Plate"))
                {
                    var obj = FindPeeper(peeper);
                    if (obj is IPlayerCharacter pc)
                    {
                        _peeper.Visibility.Unhide(pc);
                    }
                    if (obj != null) OpenAdventurePlate(obj.GameObjectId);
                }

                ImGui.Separator();
                var isBlacklisted = config.Blacklist.Exists(x => x.Name == peeper.Name && x.World == peeper.World);
                if (!isBlacklisted && MenuItem("Blacklist"))
                {
                    config.Blacklist.Add(new Configuration.PeeperBlacklistEntry
                    {
                        Name = peeper.Name,
                        World = peeper.World,
                        DisableSound = true,
                        DisableDiscord = true,
                    });
                    _config.Save();
                }
            }
        }

        bool targetHovered = false;
        bool secondLineDrawn = second.Count > 0;
        Vector2 targetItemMin = Vector2.Zero;

        if (secondLineDrawn)
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

            targetHovered = showTarget && ImGui.IsItemHovered();
            if (targetHovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            targetItemMin = ImGui.GetItemRectMin();

            if (showTarget)
            {
                using var popup = ImRaii.ContextPopupItem($"ctx_target_{peeper.GameObjectId}_{peeper.StartTime.Ticks}");

                if (popup)
                {
                    var targetName = peeper.CurrentTargetName ?? "Target";
                    if (MenuItem($"Target: {targetName}"))
                    {
                        var tObj = Service.ObjectTable.SearchById(peeper.CurrentTargetId);
                        if (tObj is IPlayerCharacter pc)
                        {
                            _peeper.Visibility.Unhide(pc);
                        }
                        if (tObj != null) Service.TargetManager.Target = tObj;
                    }
                    if (MenuItem($"Focus: {targetName}"))
                    {
                        var tObj = Service.ObjectTable.SearchById(peeper.CurrentTargetId);
                        if (tObj is IPlayerCharacter pc)
                        {
                            _peeper.Visibility.Unhide(pc);
                        }
                        if (tObj != null) Service.TargetManager.FocusTarget = tObj;
                    }
                    if (MenuItem($"Examine: {targetName}"))
                    {
                        var tObj = Service.ObjectTable.SearchById(peeper.CurrentTargetId);
                        if (tObj is IPlayerCharacter pc)
                        {
                            _peeper.Visibility.Unhide(pc);
                        }
                        if (tObj != null) Examine(tObj.GameObjectId);
                    }
                    if (MenuItem($"Plate: {targetName}"))
                    {
                        var tObj = Service.ObjectTable.SearchById(peeper.CurrentTargetId);
                        if (tObj is IPlayerCharacter pc)
                        {
                            _peeper.Visibility.Unhide(pc);
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

        var drawList = ImGui.GetWindowDrawList();
        float gap = style.ItemSpacing.X;
        float margin = MathF.Max(gap, _theme.Gap(0.5f));
        float textY = peeperItemMin.Y + style.FramePadding.Y;

        RowRenderer.DrawLeft(drawList, new Vector2(peeperItemMin.X + gap, textY), left, gap, textShadow, _theme);
        RowRenderer.DrawRight(drawList, new Vector2(rightEdge - margin, textY), right, gap, textShadow, _theme);

        if (second.Count > 0)
        {
            var secondOrigin = secondLineDrawn
                ? new Vector2(targetItemMin.X + gap, targetItemMin.Y)
                : new Vector2(peeperItemMin.X + gap, textY + ImGui.GetTextLineHeight());

            RowRenderer.DrawLeft(drawList, secondOrigin, second, gap, textShadow, _theme);
        }
    }

    private bool BuildSegments(
        PeeperState peeper,
        bool isActive,
        bool textShadow,
        List<RowSegment> left,
        List<RowSegment> right,
        List<RowSegment> second)
    {
        var config = _config.Peeper;
        var localPlayer = Service.ObjectTable.LocalPlayer;

        bool isTargetLocalPlayer = peeper.CurrentTargetId != 0 && localPlayer != null && peeper.CurrentTargetId == localPlayer.GameObjectId;
        bool hasTarget = !string.IsNullOrEmpty(peeper.CurrentTargetName) && !isTargetLocalPlayer;
        bool showTarget = false;

        uint normal = isActive
            ? ImGui.GetColorU32(config.TargetingHighlightColor)
            : ImGui.GetColorU32(ImGuiCol.TextDisabled);
        uint muted = ImGui.GetColorU32(ImGuiCol.TextDisabled);

        float lineHeight = ImGui.GetTextLineHeight();

        foreach (var field in config.RowLayout.Fields)
        {
            if (!field.Enabled) continue;
            if (!isActive && (!field.InHistory || !peeper.IsPresent) && IsPresenceField(field.Key)) continue;

            var bucket = field.Side switch
            {
                RowSide.Right => right,
                RowSide.Second => second,
                _ => left,
            };

            uint color = field.Style switch
            {
                RowFieldStyle.Muted => muted,
                RowFieldStyle.Accent => ImGui.GetColorU32(config.TargetingHighlightColor),
                RowFieldStyle.Custom => ImGui.GetColorU32(field.Color),
                _ => normal,
            };

            if (field.Key == RowFieldCatalog.Direction)
            {
                float arrowSize = lineHeight * 0.45f;
                float angle = peeper.DirectionAngle;

                bucket.Add(new RowSegment
                {
                    Width = arrowSize * 2f,
                    Draw = pos =>
                    {
                        var center = new Vector2(pos.X + arrowSize, pos.Y + lineHeight * 0.5f);
                        var list = ImGui.GetWindowDrawList();

                        if (textShadow)
                            _theme.DirectionArrow(list, center + UiTheme.TextShadowOffset, arrowSize, angle, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.8f)));

                        _theme.DirectionArrow(list, center, arrowSize, angle, color);
                    },
                });

                continue;
            }

            var text = FieldText(peeper, field, isActive, hasTarget);
            if (string.IsNullOrEmpty(text)) continue;

            if (field.Key == RowFieldCatalog.Target) showTarget = true;

            if (field.Key == RowFieldCatalog.Name && isActive && config.TargetingGlowEnabled && config.TargetingGlowThickness > 0f)
            {
                var label = text;

                bucket.Add(new RowSegment
                {
                    Width = ImGui.CalcTextSize(label).X,
                    Draw = pos =>
                    {
                        var list = ImGui.GetWindowDrawList();

                        if (textShadow) _theme.TextShadowAt(list, pos, label);

                        _theme.TextGlow(list, pos, label, config.TargetingGlowColor, config.TargetingGlowThickness);
                        _theme.ShadowedTextAt(list, pos, label, color, false);
                    },
                });

                continue;
            }

            bucket.Add(new RowSegment
            {
                Text = text,
                Color = color,
                Width = ImGui.CalcTextSize(text).X,
            });
        }

        return showTarget;
    }

    private static bool IsPresenceField(string key) =>
        key is RowFieldCatalog.Direction or RowFieldCatalog.Distance or RowFieldCatalog.Target;

    private static string? FieldText(PeeperState peeper, RowFieldConfig field, bool isActive, bool hasTarget)
    {
        switch (field.Key)
        {
            case RowFieldCatalog.Name:
                return RowFieldCatalog.Format(field.Format, token => RowFieldCatalog.NamePart(peeper.Name, token));

            case RowFieldCatalog.World:
                return RowFieldCatalog.Format(field.Format, token => token == "world" ? peeper.World : null);

            case RowFieldCatalog.Distance:
                return RowFieldCatalog.Format(field.Format, token => token switch
                {
                    "y" => peeper.Distance.ToString("F1"),
                    "yint" => ((int)MathF.Round(peeper.Distance)).ToString(),
                    _ => null,
                });

            case RowFieldCatalog.Time:
            {
                DateTime? when = isActive ? peeper.LastSeen : peeper.EndTime;
                return when == null
                    ? "Unknown"
                    : RowFieldCatalog.Format(field.Format, token => RowFieldCatalog.TimePart(when.Value, token));
            }

            case RowFieldCatalog.Duration:
            {
                var span = (isActive ? peeper.LastSeen : peeper.EndTime ?? peeper.LastSeen) - peeper.StartTime;
                return RowFieldCatalog.Format(field.Format, token => RowFieldCatalog.DurationPart(span, token));
            }

            case RowFieldCatalog.Target:
                return hasTarget
                    ? RowFieldCatalog.Format(field.Format, token => token == "world"
                        ? peeper.CurrentTargetWorld
                        : RowFieldCatalog.NamePart(peeper.CurrentTargetName, token))
                    : null;

            default:
                return null;
        }
    }

    private Dalamud.Game.ClientState.Objects.Types.IGameObject? FindPeeper(PeeperState peeper)
    {
        var obj = peeper.Live;
        obj ??= Service.ObjectTable.SearchById(peeper.GameObjectId);
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
