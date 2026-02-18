using System;
using System.Numerics;
using System.Collections.Generic;
using Cordi.Configuration.QoLBar;
using Cordi.Core;
using Cordi.Services.QoLBar;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using System.Linq;

namespace Cordi.UI.QoLBar;

public class ShortcutRenderer : IDisposable
{
    private int _id;
    public int ID
    {
        get => _id;
        set
        {
            _id = value;
            Config = (Parent != null) ? Parent.Config.SubList[value] : ParentBar.Config.ShortcutList[value];
        }
    }

    public ShCfg Config { get; private set; } = null!;
    public readonly BarRenderer ParentBar;
    public readonly ShortcutRenderer? Parent;
    public readonly List<ShortcutRenderer> Children = new();
    private readonly CommandExecutor commandExecutor;

    // Unique ID for ImGui popups to prevent collisions in nested menus
    private readonly string _guid = Guid.NewGuid().ToString();

    public bool Activated = false;
    private bool isHovered;
    private float animTime = -1;
    private Vector2 _categoryPos = Vector2.Zero;
    private Vector2 _categoryPivot = Vector2.Zero;
    private bool _wasOpen = false;
    private float _closeTimer = 0;

    public bool IsOpen => _wasOpen;

    public bool HasOpenSubPopup()
    {
        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            ImGui.PushID(i);

            bool open = child.IsOpen;
            if (!open) open = ImGui.IsPopupOpen($"ShortcutConfig##{child._guid}");
            if (!open) open = child.HasOpenSubPopup();

            ImGui.PopID();

            if (open) return true;
        }
        return false;
    }

    public ShortcutRenderer(BarRenderer bar, CommandExecutor executor)
    {
        ParentBar = bar;
        commandExecutor = executor;
        ID = bar.Children.Count;
        Initialize();
    }

    public ShortcutRenderer(ShortcutRenderer parent, CommandExecutor executor)
    {
        ParentBar = parent.ParentBar;
        Parent = parent;
        commandExecutor = executor;
        ID = parent.Children.Count;
        Initialize();
    }

    private void Initialize()
    {
        if (Config.SubList == null) return;
        for (int i = 0; i < Config.SubList.Count; i++)
            Children.Add(new ShortcutRenderer(this, commandExecutor));
    }

    public void ClearActivated()
    {
        if (animTime == 0) animTime = -1;
        Activated = false;
        if (Config.Type == ShortcutType.Category)
        {
            foreach (var ui in Children)
                ui.ClearActivated();
        }
    }

    public void OnClick(bool v, bool mouse, bool wasHovered, bool outsideDraw = false, ShCfg? effectiveConfig = null)
    {
        animTime = mouse ? -1 : 0;

        var cfg = effectiveConfig ?? GetEffectiveConfig();
        var command = cfg.Command;
        // Service.Log.Info($"[Cordi] OnClick: {cfg.Name} (Type: {cfg.Type}, Cmd: '{command}')");

        switch (cfg.Type)
        {
            case ShortcutType.Command:
                commandExecutor.QueueCommand(command);
                break;

            case ShortcutType.Category:
                if (!wasHovered)
                    commandExecutor.QueueCommand(command);

                if (!outsideDraw)
                {
                    // Toggle logic:
                    if (!_wasOpen)
                    {
                        var (pos, piv) = ParentBar.CalculateCategoryPosition(v, Parent != null, cfg.CategoryAnchor);
                        _categoryPos = pos;
                        _categoryPivot = piv;
                        ImGui.OpenPopup($"ShortcutCategory##{_guid}");
                    }
                    // Else: It was open, and ImGui closed it. We leave it closed.
                }
                break;
        }

        if (cfg.CloseOnAction && !outsideDraw && cfg.Type != ShortcutType.Category)
        {
            ImGui.CloseCurrentPopup();
            var p = Parent;
            while (p != null)
            {
                ImGui.CloseCurrentPopup();
                p = p.Parent;
            }
        }
    }

    public void DrawShortcut(float width, float height)
    {
        if (animTime >= 0.35f) animTime = -1;
        else if (animTime >= 0) animTime += ImGui.GetIO().DeltaTime;

        var inCategory = Parent != null;
        var sh = GetEffectiveConfig();
        var name = sh.Name;
        var spacer = sh.Type == ShortcutType.Spacer;

        if (!spacer && (sh.Type == ShortcutType.Command || sh.Type == ShortcutType.Category))
        {
            // Add hand cursor for interactive elements
            // ImGui.Button handles simple hover, but we might want custom cursor
        }

        var scale = ParentBar.Config.Scale;
        var effectiveWidth = sh.ButtonSize > 0 ? sh.ButtonSize * ImGuiHelpers.GlobalScale * scale : width;
        var effectiveHeight = sh.ButtonHeight > 0 ? sh.ButtonHeight * ImGuiHelpers.GlobalScale * scale : height;

        if (sh.IconOnly)
        {
            var zoom = sh.IconZoom > 0 ? sh.IconZoom : 1.0f;
            effectiveWidth *= zoom;
            effectiveHeight *= zoom;
        }

        // Rounding logic
        float rounding = sh.CornerRadius > 0 ? sh.CornerRadius : ParentBar.Config.CornerRadius;
        rounding *= ImGuiHelpers.GlobalScale * scale;

        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, rounding);

        var btnAlpha = sh.Opacity;

        // Base Button Color: #2e2e38 -> 46, 46, 56 -> 0.180f, 0.180f, 0.220f
        var baseCol = new Vector4(0.180f, 0.180f, 0.220f, 1.0f);

        if (sh.IconOnly)
        {
            baseCol = Vector4.Zero;
            ImGui.PushStyleColor(ImGuiCol.Button, baseCol);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 1f, 1f, 0.1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1f, 1f, 1f, 0.2f));
        }
        else if (btnAlpha < 1.0f && !spacer)
        {
            baseCol.W *= btnAlpha;
            var btnHov = ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered];
            btnHov.W *= btnAlpha;
            var btnAct = ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive];
            btnAct.W *= btnAlpha;

            ImGui.PushStyleColor(ImGuiCol.Button, baseCol);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, btnHov);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, btnAct);
        }
        else if (!spacer)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, baseCol);
        }

        var clicked = false;
        var c = ImGui.ColorConvertU32ToFloat4(sh.Color);

        if (spacer)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, 0);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0);
        }

        var hasIcon = sh.IconId > 0 || !string.IsNullOrEmpty(sh.CustomIconPath);

        if (hasIcon)
        {
            clicked = DrawIconButton(sh, effectiveWidth, effectiveHeight, c);
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Text, c);
            clicked = ImGui.Button(name, new Vector2(effectiveWidth, effectiveHeight));
            ImGui.PopStyleColor();
        }

        if (spacer)
        {
            ImGui.PopStyleColor(3);
            clicked = false;
        }

        if (sh.IconOnly)
        {
            ImGui.PopStyleColor(3);
        }
        else if ((btnAlpha < 1.0f && !spacer) || (!spacer))
        {
            ImGui.PopStyleColor(btnAlpha < 1.0f ? 3 : 1);
        }

        if (!inCategory)
        {
            var size = ImGui.GetItemRectMax() - ImGui.GetWindowPos();
            ParentBar.MaxWidth = size.X;
            ParentBar.MaxHeight = size.Y;
        }

        isHovered = ImGui.IsItemHovered(ImGuiHoveredFlags.RectOnly);
        var wasHovered = false;

        // Right-click to open config popup
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup) && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
        {
            ImGui.OpenPopup($"ShortcutConfig##{_guid}");
        }

        var configPopupOpen = ImGui.IsPopupOpen($"ShortcutConfig##{_guid}");
        if (configPopupOpen)
            ParentBar.HasPopupOpen = true;

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup) && !configPopupOpen)
        {
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                clicked = true;
                wasHovered = true;
            }

            if (sh.Type == ShortcutType.Category && (sh.HoverOpen))
            {
                if (sh.HoverOpen && !ImGui.IsPopupOpen($"ShortcutCategory##{_guid}"))
                {
                    var (pos, piv) = ParentBar.CalculateCategoryPosition(
                        ParentBar.Config.Columns > 0 && ParentBar.Children.Count >= (ParentBar.Config.Columns * (ParentBar.Config.Columns - 1) + 1),
                        Parent != null, sh.CategoryAnchor);
                    _categoryPos = pos;
                    _categoryPivot = piv;
                    ImGui.OpenPopup($"ShortcutCategory##{_guid}");
                }
            }
        }

        bool v = ParentBar.Config.Columns > 0 && ParentBar.Children.Count >= (ParentBar.Config.Columns * (ParentBar.Config.Columns - 1) + 1);

        if (clicked && sh.Type != ShortcutType.Spacer)
        {
            OnClick(v, true, wasHovered, false, sh);
        }
        else if (!spacer && sh.Hotkey > 0 && CordiPlugin.Plugin.KeybindService.IsKeyDown(sh.Hotkey))
        {
            OnClick(v, false, false, false, sh);
        }

        DrawShortcutConfig();
        DrawCategory(sh);
        ImGui.PopStyleVar(); // FrameRounding

        if (!spacer && isHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }





    private void ApplyOverride(ShCfg target, ShCfg ovr)
    {
        if (!string.IsNullOrEmpty(ovr.Name)) target.Name = ovr.Name;
        if (!string.IsNullOrEmpty(ovr.Command)) target.Command = ovr.Command;
        if (ovr.Color != 0xFFFFFFFF) target.Color = ovr.Color; // Default white
        if (ovr.IconId > 0) target.IconId = ovr.IconId;
        if (!string.IsNullOrEmpty(ovr.CustomIconPath)) target.CustomIconPath = ovr.CustomIconPath;
        if (ovr.Hotkey != 0) target.Hotkey = ovr.Hotkey;

        // Booleans are tricky, we assume if Override has a different value than default we take it? 
        // Or we should initialize override with defaults that indicate "no change".
        // For simplicity in this version, we will blindly copy non-default looking values.
        if (ovr.IconOnly) target.IconOnly = true;
        if (ovr.ButtonSize > 0) target.ButtonSize = ovr.ButtonSize;
        if (ovr.ButtonHeight > 0) target.ButtonHeight = ovr.ButtonHeight;
        if (ovr.Opacity != 1.0f) target.Opacity = ovr.Opacity;
        if (ovr.IconZoom != 1.0f) target.IconZoom = ovr.IconZoom;
        if (ovr.IconRotation != 0) target.IconRotation = ovr.IconRotation;
        if (ovr.IconOffset[0] != 0 || ovr.IconOffset[1] != 0) target.IconOffset = (float[])ovr.IconOffset.Clone();

        // Category Overrides
        if (ovr.CategoryColumns > 0) target.CategoryColumns = ovr.CategoryColumns;
        if (ovr.HoverOpen) target.HoverOpen = true;
        if (ovr.CategoryAnchor != CategoryAnchor.Auto) target.CategoryAnchor = ovr.CategoryAnchor;
        if (ovr.CategoryNoBackground) target.CategoryNoBackground = true;
    }

    private bool DrawIconButton(ShCfg sh, float width, float height, Vector4 textColor)
    {
        ISharedImmediateTexture? tex = null;

        if (sh.IconId > 0)
        {
            try { tex = Service.TextureProvider.GetFromGameIcon(new GameIconLookup((uint)sh.IconId)); }
            catch { }
        }
        else if (!string.IsNullOrEmpty(sh.CustomIconPath))
        {
            try { tex = Service.TextureProvider.GetFromFile(sh.CustomIconPath); }
            catch { }
        }

        var wrap = tex?.GetWrapOrEmpty();

        if (wrap != null && wrap.Handle != IntPtr.Zero)
        {
            var iconSize = height - ImGui.GetStyle().FramePadding.Y;

            ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
            var clicked = ImGui.Button($"##btn{_guid}", new Vector2(width, height));
            ImGui.PopStyleColor();

            var rectMin = ImGui.GetItemRectMin();
            var rectMax = ImGui.GetItemRectMax();
            var rectSize = rectMax - rectMin;
            var center = rectMin + rectSize * 0.5f;

            if (!sh.IconOnly)
            {
                var textSize = ImGui.CalcTextSize(sh.Name);
                var textPos = new Vector2(rectMin.X + iconSize + ImGui.GetStyle().FramePadding.X * 2, center.Y - textSize.Y * 0.5f);
                ImGui.GetWindowDrawList().AddText(textPos, ImGui.GetColorU32(textColor), sh.Name);

                if (!sh.IconOnly)
                {
                    center = new Vector2(rectMin.X + ImGui.GetStyle().FramePadding.X + iconSize * 0.5f, center.Y);
                }
            }

            float rotation = sh.IconRotation * (MathF.PI / 180f);
            float zoom = sh.IconZoom > 0 ? sh.IconZoom : 1.0f;
            if (sh.IconOnly) zoom = 1.0f; // Prevent double scaling since frame is already scaled

            var offset = new Vector2(sh.IconOffset[0], sh.IconOffset[1]) * ImGuiHelpers.GlobalScale;

            var size = new Vector2(iconSize) * zoom;
            var cos = MathF.Cos(rotation);
            var sin = MathF.Sin(rotation);

            Vector2 Rotate(Vector2 v) => new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);

            var p1 = Rotate(new Vector2(-size.X, -size.Y) * 0.5f) + center + offset;
            var p2 = Rotate(new Vector2(size.X, -size.Y) * 0.5f) + center + offset;
            var p3 = Rotate(new Vector2(size.X, size.Y) * 0.5f) + center + offset;
            var p4 = Rotate(new Vector2(-size.X, size.Y) * 0.5f) + center + offset;

            ImGui.GetWindowDrawList().AddImageQuad(wrap.Handle, p1, p2, p3, p4);

            return clicked;
        }

        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
        var fallback = ImGui.Button(sh.Name, new Vector2(width, height));
        ImGui.PopStyleColor();
        return fallback;
    }

    private void DrawShortcutConfig()
    {
        if (ImGui.BeginPopup($"ShortcutConfig##{_guid}"))
        {
            DrawConfigEditor(Config, false);

            if (Parent == null)
            {
                ImGui.Separator();
                var barChildren = ParentBar.Children;
                var idx = barChildren.IndexOf(this);
                if (idx > 0 && ImGui.MenuItem("Move Up"))
                    ParentBar.ShiftShortcut(idx, false);
                if (idx < barChildren.Count - 1 && ImGui.MenuItem("Move Down"))
                    ParentBar.ShiftShortcut(idx, true);
            }

            if (ImGui.MenuItem("Delete"))
            {
                if (Parent == null)
                {
                    var idx = ParentBar.Children.IndexOf(this);
                    if (idx >= 0)
                        ParentBar.RemoveShortcut(idx);
                }
            }

            ImGui.EndPopup();
        }
    }

    public static void DrawConfigEditor(ShCfg cfg, bool isOverride)
    {
        ImGui.SetNextItemWidth(200);
        var name = cfg.Name;
        if (ImGui.InputText("Name", ref name, 128))
        {
            cfg.Name = name;
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }

        if (!isOverride)
        {
            var typeIdx = (int)cfg.Type;
            if (ImGui.Combo("Type", ref typeIdx, "Command\0Category\0Spacer\0"))
            {
                cfg.Type = (ShortcutType)typeIdx;
                CordiPlugin.Plugin.QoLBarConfig.Save();
            }
        }

        if (cfg.Type == ShortcutType.Command || isOverride)
        {
            ImGui.SetNextItemWidth(300);
            var cmd = cfg.Command;
            if (ImGui.InputTextMultiline("Command", ref cmd, 4096, new Vector2(300, 80)))
            {
                cfg.Command = cmd;
                CordiPlugin.Plugin.QoLBarConfig.Save();
            }

            if (!isOverride)
            {
                var close = cfg.CloseOnAction;
                if (ImGui.Checkbox("Close Menu on Click", ref close))
                {
                    cfg.CloseOnAction = close;
                    CordiPlugin.Plugin.QoLBarConfig.Save();
                }
            }
        }

        if (cfg.Type == ShortcutType.Category || isOverride)
        {
            if (isOverride)
            {
                ImGui.Separator();
                ImGui.TextDisabled("Category Overrides (If Applicable)");
            }

            var hover = cfg.HoverOpen;
            if (ImGui.Checkbox("Open on Hover", ref hover))
            {
                cfg.HoverOpen = hover;
                CordiPlugin.Plugin.QoLBarConfig.Save();
            }

            var catCols = cfg.CategoryColumns;
            ImGui.SetNextItemWidth(100);
            if (ImGui.DragInt("Category Columns", ref catCols, 0.1f, 0, 20))
            {
                cfg.CategoryColumns = catCols;
                CordiPlugin.Plugin.QoLBarConfig.Save();
            }

            var anchor = (int)cfg.CategoryAnchor;
            if (ImGui.Combo("Open Direction", ref anchor, "Auto\0Top Right\0Top Left\0Top Center\0Bottom Right\0Bottom Left\0Bottom Center\0Right Top\0Right Bottom\0Right Center\0Left Top\0Left Bottom\0Left Center\0"))
            {
                cfg.CategoryAnchor = (CategoryAnchor)anchor;
                CordiPlugin.Plugin.QoLBarConfig.Save();
            }
        }

        ImGui.Separator();

        var colorVec = ImGui.ColorConvertU32ToFloat4(cfg.Color);
        if (ImGui.ColorEdit4("Color", ref colorVec, ImGuiColorEditFlags.NoInputs))
        {
            cfg.Color = ImGui.ColorConvertFloat4ToU32(colorVec);
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }

        ImGui.Separator();

        var iconId = cfg.IconId;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Icon ID", ref iconId))
        {
            cfg.IconId = Math.Max(0, iconId);
            cfg.CustomIconPath = string.Empty;
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }

        ImGui.SameLine();
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
        {
            CordiPlugin.Plugin.QoLBarOverlay.IconPicker.Open((id, path) =>
            {
                cfg.IconId = id;
                cfg.CustomIconPath = path;
                CordiPlugin.Plugin.QoLBarConfig.Save();
            });
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Browse Icons");

        if (cfg.IconId > 0)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.8f, 0.5f, 1f));
            ImGui.TextUnformatted($"Icon #{cfg.IconId}");
            ImGui.PopStyleColor();
        }
        else if (!string.IsNullOrEmpty(cfg.CustomIconPath))
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.7f, 0.9f, 1f));
            ImGui.TextUnformatted(System.IO.Path.GetFileName(cfg.CustomIconPath));
            ImGui.PopStyleColor();
        }

        var iconOnly = cfg.IconOnly;
        if (ImGui.Checkbox("Icon Only", ref iconOnly))
        {
            cfg.IconOnly = iconOnly;
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }

        ImGui.Separator();

        var btnSize = cfg.ButtonSize;
        ImGui.SetNextItemWidth(120);
        if (ImGui.DragFloat("Button Width", ref btnSize, 1f, 0f, 500f, btnSize == 0 ? "Inherit" : "%.0f"))
        {
            cfg.ButtonSize = Math.Max(0, btnSize);
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }

        var btnHeight = cfg.ButtonHeight;
        ImGui.SetNextItemWidth(120);
        if (ImGui.DragFloat("Button Height", ref btnHeight, 1f, 0f, 500f, btnHeight == 0 ? "Inherit" : "%.0f"))
        {
            cfg.ButtonHeight = Math.Max(0, btnHeight);
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }

        var opacity = cfg.Opacity;
        ImGui.SetNextItemWidth(120);
        if (ImGui.DragFloat("Opacity", ref opacity, 0.01f, 0f, 1f, "%.2f"))
        {
            cfg.Opacity = Math.Clamp(opacity, 0f, 1f);
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }

        var cornerRadius = cfg.CornerRadius;
        ImGui.SetNextItemWidth(120);
        if (ImGui.DragFloat("Corner Radius", ref cornerRadius, 0.5f, 0f, 50f, "%.0f"))
        {
            cfg.CornerRadius = Math.Max(0, cornerRadius);
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }

        ImGui.Separator();

        if (ImGui.TreeNode("Icon Transforms"))
        {
            var zoom = cfg.IconZoom;
            if (ImGui.DragFloat("Zoom", ref zoom, 0.01f, 0.1f, 5.0f)) cfg.IconZoom = zoom;

            var rot = cfg.IconRotation;
            if (ImGui.DragFloat("Rotation", ref rot, 1f, -360f, 360f)) cfg.IconRotation = rot;

            var off = new Vector2(cfg.IconOffset[0], cfg.IconOffset[1]);
            if (ImGui.DragFloat2("Offset", ref off, 0.5f))
            {
                cfg.IconOffset[0] = off.X;
                cfg.IconOffset[1] = off.Y;
            }

            if (ImGui.Button("Save Transforms")) CordiPlugin.Plugin.QoLBarConfig.Save();

            var hotkey = cfg.Hotkey;
            if (CordiPlugin.Plugin.KeybindService.InputHotkey("Hotkey", ref hotkey))
            {
                cfg.Hotkey = hotkey;
                CordiPlugin.Plugin.QoLBarConfig.Save();
            }

            ImGui.TreePop();
        }

        if (!isOverride)
        {
            ImGui.Separator();
            if (ImGui.TreeNode("Conditions"))
            {
                var defs = CordiPlugin.Plugin.QoLBarConfig.ConditionDefinitions;
                if (defs.Count == 0)
                {
                    ImGui.TextColored(new Vector4(1, 0.5f, 0.5f, 1), "No Conditions defined in QoL Bar tab.");
                }
                else
                {
                    var defNames = defs.Select(d => d.Name).ToArray();

                    for (int i = 0; i < cfg.Conditions.Count; i++)
                    {
                        var cond = cfg.Conditions[i];
                        ImGui.PushID(i);

                        var currentIdx = Array.IndexOf(defNames, cond.ConditionName);
                        if (currentIdx == -1) currentIdx = 0;

                        ImGui.SetNextItemWidth(150);
                        if (ImGui.Combo("##condSelect", ref currentIdx, defNames, defNames.Length))
                        {
                            cond.ConditionName = defNames[currentIdx];
                            CordiPlugin.Plugin.QoLBarConfig.Save();
                        }

                        ImGui.SameLine();
                        if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                        {
                            cfg.Conditions.RemoveAt(i);
                            CordiPlugin.Plugin.QoLBarConfig.Save();
                            i--;
                        }

                        ImGui.PopID();
                    }

                    if (ImGui.Button("Add Condition Assignment"))
                    {
                        if (defs.Count > 0)
                        {
                            cfg.Conditions.Add(new ShCondition { ConditionName = defs[0].Name });
                            CordiPlugin.Plugin.QoLBarConfig.Save();
                        }
                    }
                }

                ImGui.TreePop();
            }
        }
    }

    private ShCfg GetEffectiveConfig()
    {
        var cfg = Config.Clone();
        if (Config.Conditions.Count == 0) return cfg;

        var variableService = CordiPlugin.Plugin.VariableService;
        var defs = CordiPlugin.Plugin.QoLBarConfig.ConditionDefinitions;

        foreach (var condRef in Config.Conditions)
        {
            var def = defs.FirstOrDefault(d => d.Name == condRef.ConditionName);
            if (def == null) continue;

            var val = variableService.GetVariable(def.Variable);
            if (val == null) val = string.Empty;

            foreach (var c in def.Cases)
            {
                if (CheckCondition(val, c.Operator, c.Value))
                {
                    ApplyOverride(cfg, c.Override);
                }
            }
        }

        return cfg;
    }

    private bool CheckCondition(string val, ConditionOperator op, string target)
    {
        if (target == null) target = string.Empty;

        switch (op)
        {
            case ConditionOperator.Equals: return val.Equals(target, StringComparison.OrdinalIgnoreCase);
            case ConditionOperator.NotEquals: return !val.Equals(target, StringComparison.OrdinalIgnoreCase);
            case ConditionOperator.Contains: return val.Contains(target, StringComparison.OrdinalIgnoreCase);
            case ConditionOperator.GreaterThan:
                if (float.TryParse(val, out var v1) && float.TryParse(target, out var t1)) return v1 > t1;
                return string.Compare(val, target, StringComparison.OrdinalIgnoreCase) > 0;
            case ConditionOperator.LessThan:
                if (float.TryParse(val, out var v2) && float.TryParse(target, out var t2)) return v2 < t2;
                return string.Compare(val, target, StringComparison.OrdinalIgnoreCase) < 0;
            default: return false;
        }
    }

    private void DrawCategory(ShCfg sh)
    {
        if (sh.Type != ShortcutType.Category) return;

        _wasOpen = ImGui.IsPopupOpen($"ShortcutCategory##{_guid}");

        if (_wasOpen)
        {
            ImGui.SetNextWindowPos(_categoryPos, ImGuiCond.Appearing, _categoryPivot);
        }

        var pushBorder = false;
        if (!ParentBar.Config.NoBackground)
        {
            var winBg = new Vector4(0.090f, 0.090f, 0.110f, ParentBar.Config.Opacity);
            ImGui.PushStyleColor(ImGuiCol.PopupBg, winBg);
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.PopupBg, Vector4.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 0.0f);
            pushBorder = true;
        }

        if (ImGui.BeginPopup($"ShortcutCategory##{_guid}"))
        {
            var catCols = sh.CategoryColumns;
            var scale = ParentBar.Config.Scale;
            var catWidth = sh.CategoryNoBackground ? -1 : ParentBar.Config.ButtonWidth * ImGuiHelpers.GlobalScale * scale;
            var catHeight = ParentBar.Config.ButtonHeight > 0
                ? (float)Math.Round(ParentBar.Config.ButtonHeight * ImGuiHelpers.GlobalScale * scale)
                : (float)Math.Round((ImGui.GetFontSize() + ImGui.GetStyle().FramePadding.Y * 2) * scale);
            for (int i = 0; i < Children.Count; i++)
            {
                ImGui.PushID(i);
                Children[i].DrawShortcut(catWidth, catHeight);
                if (catCols > 0 && i % catCols != catCols - 1)
                    ImGui.SameLine();
                ImGui.PopID();
            }

            // Use Config for structural changes (SubList) to be safe, though sh.SubList is same ref.
            if (Config.SubList != null && Children.Count < Config.SubList.Count)
            {
                for (int i = Children.Count; i < Config.SubList.Count; i++)
                    Children.Add(new ShortcutRenderer(this, commandExecutor));
            }

            if (ParentBar.Config.Editing)
            {
                var height = ImGui.GetFontSize() + ImGui.GetStyle().FramePadding.Y * 2;
                if (ImGui.Button("+##addCat", new Vector2(catWidth > 0 ? catWidth : 100, height)))
                    ImGui.OpenPopup("addCatShortcut");

                if (ImGui.BeginPopup("addCatShortcut"))
                {
                    if (ImGui.MenuItem("Command"))
                    {
                        Config.SubList ??= new();
                        Config.SubList.Add(new ShCfg { Name = "New", Type = ShortcutType.Command });
                        Children.Add(new ShortcutRenderer(this, commandExecutor));
                        CordiPlugin.Plugin.QoLBarConfig.Save();
                    }
                    if (ImGui.MenuItem("Category"))
                    {
                        Config.SubList ??= new();
                        Config.SubList.Add(new ShCfg { Name = "Menu", Type = ShortcutType.Category });
                        Children.Add(new ShortcutRenderer(this, commandExecutor));
                        CordiPlugin.Plugin.QoLBarConfig.Save();
                    }
                    if (ImGui.MenuItem("Spacer"))
                    {
                        Config.SubList ??= new();
                        Config.SubList.Add(new ShCfg { Name = string.Empty, Type = ShortcutType.Spacer });
                        Children.Add(new ShortcutRenderer(this, commandExecutor));
                        CordiPlugin.Plugin.QoLBarConfig.Save();
                    }
                    ImGui.EndPopup();
                }
            }

            if (sh.HoverOpen)
            {
                if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup | ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)
                    && !isHovered
                    && !HasOpenSubPopup())
                {
                    _closeTimer += ImGui.GetIO().DeltaTime;
                    if (_closeTimer > 0.1f)
                    {
                        ImGui.CloseCurrentPopup();
                        _closeTimer = 0;
                    }
                }
                else
                {
                    _closeTimer = 0;
                }
            }

            ImGui.EndPopup();
        }
        ImGui.PopStyleColor(); // Pop PopupBg
        if (pushBorder) ImGui.PopStyleVar(); // Pop PopupBorderSize
    }

    public void Dispose()
    {
        foreach (var child in Children)
            child.Dispose();
    }
}
