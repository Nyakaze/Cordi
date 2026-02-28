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
using Cordi.UI.Themes;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Textures.TextureWraps;

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
    private readonly ShCfg _effectiveConfigCache = new();
    private static readonly UiTheme _configTheme = new UiTheme();

    public bool Activated = false;
    /// <summary>Set by KeybindService during framework update (before game reads input). Consumed once in Draw.</summary>
    internal bool HotkeyActivatedThisFrame = false;
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
        var command = cfg.Command ?? string.Empty;
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
        var name = sh.Name ?? string.Empty;
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

        // Element-level spacing: add margin on all 4 sides using BeginGroup + Dummy spacers.
        // This correctly expands the outer bounding box so ImGui layout (SameLine, MaxWidth) works.
        var spX = sh.Spacing != null && sh.Spacing.Length > 0 ? sh.Spacing[0] : 0;
        var spY = sh.Spacing != null && sh.Spacing.Length > 1 ? sh.Spacing[1] : 0;
        var hasElementSpacing = spX > 0 || spY > 0;

        if (hasElementSpacing)
        {
            ImGui.BeginGroup();

            // Top spacing row
            if (spY > 0)
                ImGui.Dummy(new Vector2(effectiveWidth + spX * 2, spY));

            // Left spacer
            if (spX > 0)
            {
                ImGui.Dummy(new Vector2(spX, effectiveHeight));
                ImGui.SameLine(0, 0);
            }
        }

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

        if (hasElementSpacing)
        {
            // Right spacer
            if (spX > 0)
            {
                ImGui.SameLine(0, 0);
                ImGui.Dummy(new Vector2(spX, effectiveHeight));
            }

            // Bottom spacing row
            if (spY > 0)
                ImGui.Dummy(new Vector2(effectiveWidth + spX * 2, spY));

            ImGui.EndGroup();
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
        else if (!spacer && HotkeyActivatedThisFrame)
        {
            HotkeyActivatedThisFrame = false; // consume the flag
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
        if (ovr.Name != null) target.Name = ovr.Name;
        if (ovr.Command != null) target.Command = ovr.Command;
        if (ovr.Color != 0xFFFFFFFF) target.Color = ovr.Color; // Default white
        if (ovr.IconId > 0) target.IconId = ovr.IconId;
        if (ovr.CustomIconPath != null) target.CustomIconPath = ovr.CustomIconPath;
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
        if (ovr.IconOffset != null && (ovr.IconOffset[0] != 0 || ovr.IconOffset[1] != 0)) target.IconOffset = (float[])ovr.IconOffset.Clone();

        if (ovr.Tooltip != null) target.Tooltip = ovr.Tooltip;
        if (ovr.UseFrame) target.UseFrame = true;
        if (ovr.ClickThrough) target.ClickThrough = true;
        if (ovr.Spacing != null && (ovr.Spacing[0] != 0 || ovr.Spacing[1] != 0)) target.Spacing = (float[])ovr.Spacing.Clone();
        if (ovr.CornerRadius > 0) target.CornerRadius = ovr.CornerRadius;
        if (ovr.HotkeyPassToGame) target.HotkeyPassToGame = true;
        if (ovr.ColorAnimation != 0) target.ColorAnimation = ovr.ColorAnimation;
        if (ovr.CloseOnAction) target.CloseOnAction = true;
        if (ovr.CooldownAction != 0) target.CooldownAction = ovr.CooldownAction;
        if (ovr.CooldownStyle != 0) target.CooldownStyle = ovr.CooldownStyle;


        // Category Overrides
        if (ovr.CategoryColumns > 0) target.CategoryColumns = ovr.CategoryColumns;
        if (ovr.HoverOpen) target.HoverOpen = true;
        if (ovr.CategoryAnchor != CategoryAnchor.Auto) target.CategoryAnchor = ovr.CategoryAnchor;
        if (ovr.CategoryNoBackground) target.CategoryNoBackground = true;
        if (ovr.CategoryFontScale != 1.0f) target.CategoryFontScale = ovr.CategoryFontScale;
    }

    private bool DrawIconButton(ShCfg sh, float width, float height, Vector4 textColor)
    {
        if (sh.UseFrame)
        {
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        }

        if (Config.Type == ShortcutType.Spacer)
        {
            ImGui.Dummy(new Vector2(width, height));

            if (sh.UseFrame)
            {
                ImGui.PopStyleColor(2);
                if (ImGui.IsItemHovered())
                {
                    var min = ImGui.GetItemRectMin();
                    var max = ImGui.GetItemRectMax();
                    ImGui.GetWindowDrawList().AddRect(min, max, 0xFFFFFFFF, 4f, ImDrawFlags.None, 2f);
                }
            }

            if (!string.IsNullOrEmpty(sh.Tooltip) && ImGui.IsItemHovered())
                ImGui.SetTooltip(sh.Tooltip);

            DrawShortcutConfig();
            return false;
        }

        IDalamudTextureWrap? imgTex = null;
        if (sh.IconId > 0)
        {
            var icon = Service.TextureProvider.GetFromGameIcon(new GameIconLookup((uint)sh.IconId));
            if (icon != null) imgTex = icon.GetWrapOrEmpty();
        }
        else if (sh.CustomIconPath != null)
        {
            try { imgTex = Service.TextureProvider.GetFromFile(sh.CustomIconPath).GetWrapOrEmpty(); }
            catch { }
        }

        bool clicked = false;

        if (imgTex != null && imgTex.Handle != IntPtr.Zero)
        {
            var iconSize = height - ImGui.GetStyle().FramePadding.Y;

            ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
            clicked = ImGui.Button($"##btn{_guid}", new Vector2(width, height));
            ImGui.PopStyleColor();

            var rectMin = ImGui.GetItemRectMin();
            var rectMax = ImGui.GetItemRectMax();
            var rectSize = rectMax - rectMin;
            var center = rectMin + rectSize * 0.5f;

            if (!sh.IconOnly)
            {
                var textSize = ImGui.CalcTextSize(sh.Name ?? "");
                var textPos = new Vector2(rectMin.X + iconSize + ImGui.GetStyle().FramePadding.X * 2, center.Y - textSize.Y * 0.5f);
                ImGui.GetWindowDrawList().AddText(textPos, ImGui.GetColorU32(textColor), sh.Name ?? "");

                if (!sh.IconOnly)
                {
                    center = new Vector2(rectMin.X + ImGui.GetStyle().FramePadding.X + iconSize * 0.5f, center.Y);
                }
            }

            float rotation = sh.IconRotation * (MathF.PI / 180f);
            float zoom = sh.IconZoom > 0 ? sh.IconZoom : 1.0f;
            if (sh.IconOnly) zoom = 1.0f;

            var offset = new Vector2(
                sh.IconOffset != null && sh.IconOffset.Length > 0 ? sh.IconOffset[0] : 0,
                sh.IconOffset != null && sh.IconOffset.Length > 1 ? sh.IconOffset[1] : 0) * ImGuiHelpers.GlobalScale;

            var size = new Vector2(iconSize) * zoom;
            var cos = MathF.Cos(rotation);
            var sin = MathF.Sin(rotation);

            Vector2 Rotate(Vector2 v) => new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);

            var p1 = Rotate(new Vector2(-size.X, -size.Y) * 0.5f) + center + offset;
            var p2 = Rotate(new Vector2(size.X, -size.Y) * 0.5f) + center + offset;
            var p3 = Rotate(new Vector2(size.X, size.Y) * 0.5f) + center + offset;
            var p4 = Rotate(new Vector2(-size.X, size.Y) * 0.5f) + center + offset;

            ImGui.GetWindowDrawList().AddImageQuad(imgTex.Handle, p1, p2, p3, p4);
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            clicked = ImGui.Button(sh.Name ?? string.Empty, new Vector2(width, height));
            ImGui.PopStyleColor();
        }

        if (sh.UseFrame)
        {
            ImGui.PopStyleColor(2);
            if (ImGui.IsItemHovered())
            {
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                ImGui.GetWindowDrawList().AddRect(min, max, 0xFFFFFFFF, 4f, ImDrawFlags.None, 2f);
            }
        }

        if (!string.IsNullOrEmpty(sh.Tooltip) && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(sh.Tooltip);
        }

        // If click-through is enabled, swallow the click
        if (sh.ClickThrough) clicked = false;

        return clicked;
    }

    private void DrawShortcutConfig()
    {
        ImGui.PushStyleColor(ImGuiCol.PopupBg, _configTheme.WindowBg);
        try
        {
            if (ImGui.BeginPopup($"ShortcutConfig##{_guid}"))
            {
                var effective = GetEffectiveConfig();
                DrawConfigEditor(Config, false, _configTheme, effective);

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
        catch (Exception ex)
        {
            Service.Log.Error(ex, "[Cordi] Exception in DrawShortcutConfig — caught to protect ImGui style stack");
        }
        ImGui.PopStyleColor();
    }

    public static void DrawConfigEditor(ShCfg cfg, bool isOverride, UiTheme? theme = null, ShCfg? effectiveCfg = null)
        => ShortcutConfigEditor.DrawConfigEditor(cfg, isOverride, theme, effectiveCfg);


    private ShCfg GetEffectiveConfig()
    {
        try
        {
            _effectiveConfigCache.CopyFrom(Config);
            if (Config.Conditions == null || Config.Conditions.Count == 0) return _effectiveConfigCache;

            var variableService = CordiPlugin.Plugin.VariableService;
            var defs = CordiPlugin.Plugin.QoLBarConfig.ConditionDefinitions;

            foreach (var condRef in Config.Conditions)
            {
                var def = defs.FirstOrDefault(d => d != null && d.ID == condRef.ConditionID);
                if (def == null && !string.IsNullOrEmpty(condRef.ConditionID))
                    def = defs.FirstOrDefault(d => d != null && d.Name == condRef.ConditionID);

                if (def == null || condRef.Cases == null) continue;

                var varName = def.Variable ?? string.Empty;
                var val = variableService.GetVariable(varName) ?? string.Empty;

                foreach (var c in condRef.Cases)
                {
                    if (c == null) continue;
                    if (c.Override == null) c.Override = new();
                    if (CheckCondition(val, c.Operator, c.Value))
                    {
                        ApplyOverride(_effectiveConfigCache, c.Override);
                    }
                }
            }

            return _effectiveConfigCache;
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "[Cordi] Exception in GetEffectiveConfig — returning base config");
            return Config.Clone();
        }
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
