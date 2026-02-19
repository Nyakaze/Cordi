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
        ISharedImmediateTexture? tex = null;

        if (sh.IconId > 0)
        {
            try { tex = Service.TextureProvider.GetFromGameIcon(new GameIconLookup((uint)sh.IconId)); }
            catch { }
        }

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
        var theme = new UiTheme();
        ImGui.PushStyleColor(ImGuiCol.PopupBg, theme.WindowBg);
        try
        {
            if (ImGui.BeginPopup($"ShortcutConfig##{_guid}"))
            {
                var effective = GetEffectiveConfig();
                DrawConfigEditor(Config, false, theme, effective);

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
    {
        theme ??= new UiTheme();

        ImGui.SetNextItemWidth(200);
        theme.PushInputScope();
        var name = cfg.Name ?? string.Empty;
        if (ImGui.InputText("Name", ref name, 128))
        {
            cfg.Name = (isOverride && name.Length == 0) ? null : name;
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }
        DrawOverrideIndicator(cfg.Name, effectiveCfg?.Name);
        theme.PopInputScope();

        theme.SpacerY(0.5f);

        if (theme.BeginTabBar("##shConfigTabs"))
        {
            if (theme.BeginTabItem("General"))
            {
                theme.PushInputScope();
                DrawGeneralTab(cfg, isOverride, theme, effectiveCfg);
                theme.PopInputScope();
                theme.EndTabItem();
            }

            if (theme.BeginTabItem("Style"))
            {
                theme.PushInputScope();
                DrawStyleTab(cfg, isOverride, theme, effectiveCfg);
                theme.PopInputScope();
                theme.EndTabItem();
            }

            if (theme.BeginTabItem("Transforms"))
            {
                theme.PushInputScope();
                DrawTransformsTab(cfg, theme, effectiveCfg);
                theme.PopInputScope();
                theme.EndTabItem();
            }

            if (!isOverride)
            {
                if (theme.BeginTabItem("Overrides"))
                {
                    DrawConditionsTab(cfg, theme);
                    theme.EndTabItem();
                }
            }
            theme.EndTabBar();
        }
    }

    private static void DrawGeneralTab(ShCfg cfg, bool isOverride, UiTheme theme, ShCfg? effectiveCfg)
    {

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
            theme.SpacerY(0.5f);
            ImGui.TextDisabled("Command Settings");
            ImGui.SetNextItemWidth(300);
            var cmd = cfg.Command ?? string.Empty;
            if (ImGui.InputTextMultiline("Command", ref cmd, 4096, new Vector2(300, 80)))
            {
                cfg.Command = (isOverride && cmd.Length == 0) ? null : cmd;
                CordiPlugin.Plugin.QoLBarConfig.Save();
            }
            DrawOverrideIndicator(cfg.Command, effectiveCfg?.Command);

            if (!isOverride)
            {
                var close = cfg.CloseOnAction;
                if (theme.Checkbox("Close Menu on Click", ref close))
                {
                    cfg.CloseOnAction = close;
                    CordiPlugin.Plugin.QoLBarConfig.Save();
                }
            }
        }

        if (cfg.Type == ShortcutType.Category || isOverride)
        {
            theme.SpacerY(0.5f);
            if (isOverride)
            {
                ImGui.Separator();
                ImGui.TextDisabled("Category Overrides");
            }
            else
            {
                ImGui.TextDisabled("Category Settings");
            }

            var hover = cfg.HoverOpen;
            if (theme.Checkbox("Open on Hover", ref hover))
            {
                cfg.HoverOpen = hover;
                CordiPlugin.Plugin.QoLBarConfig.Save();
            }
            DrawOverrideIndicator(cfg.HoverOpen, effectiveCfg?.HoverOpen);

            var catCols = cfg.CategoryColumns;
            ImGui.SetNextItemWidth(100);
            if (ImGui.DragInt("Category Columns", ref catCols, 0.1f, 0, 20))
            {
                cfg.CategoryColumns = catCols;
                CordiPlugin.Plugin.QoLBarConfig.Save();
            }
            DrawOverrideIndicator(cfg.CategoryColumns, effectiveCfg?.CategoryColumns);

            var anchor = (int)cfg.CategoryAnchor;
            if (ImGui.Combo("Open Direction", ref anchor, "Auto\0Top Right\0Top Left\0Top Center\0Bottom Right\0Bottom Left\0Bottom Center\0Right Top\0Right Bottom\0Right Center\0Left Top\0Left Bottom\0Left Center\0"))
            {
                cfg.CategoryAnchor = (CategoryAnchor)anchor;
                CordiPlugin.Plugin.QoLBarConfig.Save();
            }
            DrawOverrideIndicator(cfg.CategoryAnchor, effectiveCfg?.CategoryAnchor);
        }

        theme.SpacerY(0.5f);
        ImGui.Separator();

        var tooltip = cfg.Tooltip ?? string.Empty;
        if (ImGui.InputText("Tooltip", ref tooltip, 256))
        {
            cfg.Tooltip = (isOverride && tooltip.Length == 0) ? null : tooltip;
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }
        DrawOverrideIndicator(cfg.Tooltip, effectiveCfg?.Tooltip);

        theme.SpacerY(0.5f);
        ImGui.Separator();
        ImGui.TextDisabled("Hotkey");
        theme.SpacerY(0.3f);

        var hotkey = cfg.Hotkey;
        ImGui.Text("Bind");
        ImGui.SameLine(60);
        if (CordiPlugin.Plugin.KeybindService.InputHotkey("##hotkey", ref hotkey))
        {
            cfg.Hotkey = hotkey;
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }

        if (cfg.Hotkey != 0)
        {
            var passToGame = cfg.HotkeyPassToGame;
            if (ImGui.Checkbox("Pass Input to Game", ref passToGame))
            {
                cfg.HotkeyPassToGame = passToGame;
                CordiPlugin.Plugin.QoLBarConfig.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("When disabled, the hotkey triggers this button\nbut is not forwarded to the game.");
        }

    }

    private static void DrawStyleTab(ShCfg cfg, bool isOverride, UiTheme theme, ShCfg? effectiveCfg)
    {
        var colorVec = ImGui.ColorConvertU32ToFloat4(cfg.Color);
        if (ImGui.ColorEdit4("Color", ref colorVec, ImGuiColorEditFlags.NoInputs))
        {
            cfg.Color = ImGui.ColorConvertFloat4ToU32(colorVec);
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }
        DrawOverrideIndicator(cfg.Color, effectiveCfg?.Color);

        theme.SpacerY(0.5f);

        var iconId = cfg.IconId;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Icon ID", ref iconId))
        {
            cfg.IconId = Math.Max(0, iconId);
            cfg.CustomIconPath = null;
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }
        DrawOverrideIndicator(cfg.IconId, effectiveCfg?.IconId);

        ImGui.SameLine();
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
        {
            CordiPlugin.Plugin.QoLBarOverlay.IconPicker.Open((id, path) =>
            {
                cfg.IconId = id;
                cfg.CustomIconPath = string.IsNullOrEmpty(path) ? null : path;
                CordiPlugin.Plugin.QoLBarConfig.Save();
            });
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Browse Icons");

        if (cfg.IconId > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 0.5f, 1f), $"Icon #{cfg.IconId}");
        }
        else if (!string.IsNullOrEmpty(cfg.CustomIconPath))
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.5f, 0.7f, 0.9f, 1f), System.IO.Path.GetFileName(cfg.CustomIconPath));
        }

        var iconOnly = cfg.IconOnly;
        if (theme.Checkbox("Icon Only", ref iconOnly))
        {
            cfg.IconOnly = iconOnly;
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }
        DrawOverrideIndicator(cfg.IconOnly, effectiveCfg?.IconOnly);

        theme.SpacerY(0.5f);
        ImGui.Separator();

        var btnSize = cfg.ButtonSize;
        ImGui.SetNextItemWidth(120);
        if (ImGui.DragFloat("Button Width", ref btnSize, 1f, 0f, 500f, btnSize == 0 ? "Inherit" : "%.0f"))
        {
            cfg.ButtonSize = Math.Max(0, btnSize);
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }
        DrawOverrideIndicator(cfg.ButtonSize, effectiveCfg?.ButtonSize);

        var btnHeight = cfg.ButtonHeight;
        ImGui.SetNextItemWidth(120);
        if (ImGui.DragFloat("Button Height", ref btnHeight, 1f, 0f, 500f, btnHeight == 0 ? "Inherit" : "%.0f"))
        {
            cfg.ButtonHeight = Math.Max(0, btnHeight);
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }
        DrawOverrideIndicator(cfg.ButtonHeight, effectiveCfg?.ButtonHeight);

        var opacity = cfg.Opacity;
        ImGui.SetNextItemWidth(120);
        if (ImGui.DragFloat("Opacity", ref opacity, 0.01f, 0f, 1f, "%.2f"))
        {
            cfg.Opacity = Math.Clamp(opacity, 0f, 1f);
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }
        DrawOverrideIndicator(cfg.Opacity, effectiveCfg?.Opacity);

        var cornerRadius = cfg.CornerRadius;
        ImGui.SetNextItemWidth(120);
        if (ImGui.DragFloat("Corner Radius", ref cornerRadius, 0.5f, 0f, 50f, "%.0f"))
        {
            cfg.CornerRadius = Math.Max(0, cornerRadius);
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }
        DrawOverrideIndicator(cfg.CornerRadius, effectiveCfg?.CornerRadius);

        theme.SpacerY(0.5f);
        ImGui.Separator();
        ImGui.TextDisabled("Interaction & Layout");

        var useFrame = cfg.UseFrame;
        if (theme.Checkbox("Hotbar Frame on Hover", ref useFrame))
        {
            cfg.UseFrame = useFrame;
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }
        DrawOverrideIndicator(cfg.UseFrame, effectiveCfg?.UseFrame);

        var clickThrough = cfg.ClickThrough;
        if (theme.Checkbox("Click-Through", ref clickThrough))
        {
            cfg.ClickThrough = clickThrough;
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }
        DrawOverrideIndicator(cfg.ClickThrough, effectiveCfg?.ClickThrough);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Ignore mouse input for this button");

        ImGui.Text("Spacing (Padding)");
        var spX = cfg.Spacing[0];
        ImGui.SetNextItemWidth(100);
        if (ImGui.DragFloat("X##pad", ref spX, 1f, 0f, 100f, "%.0f"))
        {
            cfg.Spacing[0] = spX;
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }
        DrawOverrideIndicator(cfg.Spacing[0], effectiveCfg?.Spacing?[0]);

        ImGui.SameLine();
        var spY = cfg.Spacing[1];
        ImGui.SetNextItemWidth(100);
        if (ImGui.DragFloat("Y##pad", ref spY, 1f, 0f, 100f, "%.0f"))
        {
            cfg.Spacing[1] = spY;
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }
        DrawOverrideIndicator(cfg.Spacing[1], effectiveCfg?.Spacing?[1]);
    }

    private static void DrawTransformsTab(ShCfg cfg, UiTheme theme, ShCfg? effectiveCfg)
    {
        var zoom = cfg.IconZoom;
        if (ImGui.DragFloat("Zoom", ref zoom, 0.01f, 0.1f, 5.0f)) cfg.IconZoom = zoom;
        DrawOverrideIndicator(cfg.IconZoom, effectiveCfg?.IconZoom);

        var rot = cfg.IconRotation;
        if (ImGui.DragFloat("Rotation", ref rot, 1f, -360f, 360f)) cfg.IconRotation = rot;
        DrawOverrideIndicator(cfg.IconRotation, effectiveCfg?.IconRotation);

        var off = new Vector2(cfg.IconOffset[0], cfg.IconOffset[1]);
        if (ImGui.DragFloat2("Offset", ref off, 0.5f))
        {
            cfg.IconOffset[0] = off.X;
            cfg.IconOffset[1] = off.Y;
        }
        DrawOverrideIndicator(cfg.IconOffset, effectiveCfg?.IconOffset);

        theme.SpacerY(0.5f);
        if (theme.Button("Save Transforms")) CordiPlugin.Plugin.QoLBarConfig.Save();
    } // End of Transforms (replacing old tree node)

    /* 
       Remaining parts to check: 
       I am consuming up to line 615 (TreePop).
       The previous code had Conditions logic AFTER Transforms.
       I need to make sure I process Conditions correctly.
    */
    private static void DrawConditionsTab(ShCfg cfg, UiTheme theme)
    {
        try
        {
            var defs = CordiPlugin.Plugin.QoLBarConfig.ConditionDefinitions;
            if (defs.Count == 0)
            {
                ImGui.TextColored(new Vector4(1, 0.5f, 0.5f, 1), "No Variable Contexts defined in QoL Bar tab.");
                return;
            }

            var defNames = defs.Select(d => d.Name ?? "Unnamed").ToArray();
            var defIds = defs.Select(d => d.ID ?? string.Empty).ToArray();
            float scale = ImGuiHelpers.GlobalScale;

            if (cfg.Conditions == null) cfg.Conditions = new();

            for (int i = 0; i < cfg.Conditions.Count; i++)
            {
                var cond = cfg.Conditions[i];
                if (cond.Cases == null) cond.Cases = new();
                ImGui.PushID(i);

                var currentIdx = Array.IndexOf(defIds, cond.ConditionID);
                // Fallback for transition
                if (currentIdx == -1 && !string.IsNullOrEmpty(cond.ConditionID))
                {
                    currentIdx = Array.IndexOf(defNames, cond.ConditionID);
                    if (currentIdx != -1)
                    {
                        cond.ConditionID = defIds[currentIdx];
                        CordiPlugin.Plugin.QoLBarConfig.Save();
                    }
                }
                if (currentIdx == -1) currentIdx = 0;

                ImGui.SetNextItemWidth(150 * scale);
                theme.PushInputScope();
                if (ImGui.Combo("##condSelect", ref currentIdx, defNames, defNames.Length))
                {
                    cond.ConditionID = defIds[currentIdx];
                    CordiPlugin.Plugin.QoLBarConfig.Save();
                }
                theme.PopInputScope();

                ImGui.SameLine();
                if (theme.SecondaryButton($"Cases ({cond.Cases.Count})"))
                {
                    ImGui.OpenPopup("EditConditionCasesPopup");
                }

                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                {
                    cfg.Conditions.RemoveAt(i);
                    CordiPlugin.Plugin.QoLBarConfig.Save();
                    i--;
                }

                ImGui.SetNextWindowSizeConstraints(new Vector2(600, 400) * scale, new Vector2(float.MaxValue, float.MaxValue));
                if (ImGui.BeginPopup("EditConditionCasesPopup", ImGuiWindowFlags.None))
                {
                    ImGui.TextColored(theme.Accent, $"Cases for '{defNames[currentIdx]}'");
                    theme.SpacerY(0.5f);

                    var tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY;
                    var availY = ImGui.GetContentRegionAvail().Y - 40 * scale;
                    if (ImGui.BeginTable("##casesTable", 4, tableFlags, new Vector2(0, availY)))
                    {
                        ImGui.TableSetupColumn("Operator", ImGuiTableColumnFlags.WidthFixed, 100 * scale);
                        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Override", ImGuiTableColumnFlags.WidthFixed, 60 * scale);
                        ImGui.TableSetupColumn("##del", ImGuiTableColumnFlags.WidthFixed, 40 * scale);
                        ImGui.TableHeadersRow();

                        for (int k = 0; k < cond.Cases.Count; k++)
                        {
                            var c = cond.Cases[k];
                            if (c.Override == null) c.Override = new();
                            ImGui.PushID($"case{k}");
                            ImGui.TableNextRow();

                            ImGui.TableNextColumn();
                            ImGui.SetNextItemWidth(-1);
                            var op = (int)c.Operator;
                            if (ImGui.Combo("##op", ref op, "==\0!=\0>\0<\0Contains\0"))
                            {
                                c.Operator = (ConditionOperator)op;
                                CordiPlugin.Plugin.QoLBarConfig.Save();
                            }

                            ImGui.TableNextColumn();
                            ImGui.SetNextItemWidth(-1);
                            var val = c.Value;
                            if (ImGui.InputText("##val", ref val, 128))
                            {
                                c.Value = val;
                                CordiPlugin.Plugin.QoLBarConfig.Save();
                            }

                            ImGui.TableNextColumn();
                            if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
                            {
                                ImGui.OpenPopup("EditCaseOverridePopup");
                            }
                            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Configure visual override");

                            ImGui.PushStyleColor(ImGuiCol.PopupBg, theme.WindowBg);
                            if (ImGui.BeginPopup("EditCaseOverridePopup"))
                            {
                                ImGui.TextColored(theme.Accent, "Override Settings");
                                ImGui.Separator();

                                DrawConfigEditor(c.Override, true, theme);

                                theme.SpacerY(0.5f);
                                if (theme.Button("Close")) ImGui.CloseCurrentPopup();

                                ImGui.EndPopup();
                            }
                            ImGui.PopStyleColor();

                            ImGui.TableNextColumn();
                            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                            {
                                cond.Cases.RemoveAt(k);
                                CordiPlugin.Plugin.QoLBarConfig.Save();
                                k--;
                            }

                            ImGui.PopID();
                        }
                        ImGui.EndTable();
                    }

                    theme.SpacerY(0.5f);
                    if (theme.PrimaryButton("+ Add Case", new Vector2(-1, 0)))
                    {
                        cond.Cases.Add(new ShConditionCase());
                        CordiPlugin.Plugin.QoLBarConfig.Save();
                    }

                    ImGui.EndPopup();
                }

                ImGui.PopID();
            }

            theme.SpacerY(0.5f);
            if (theme.SecondaryButton("Add Override Group"))
            {
                if (defs.Count > 0)
                {
                    cfg.Conditions.Add(new ShCondition { ConditionID = defs[0].ID });
                    CordiPlugin.Plugin.QoLBarConfig.Save();
                }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "[Cordi] Exception in DrawConditionsTab");
        }
    }

    private ShCfg GetEffectiveConfig()
    {
        try
        {
            var cfg = Config.Clone();
            if (Config.Conditions == null || Config.Conditions.Count == 0) return cfg;

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
                        ApplyOverride(cfg, c.Override);
                    }
                }
            }

            return cfg;
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

    private static void DrawOverrideIndicator(object? baseVal, object? effVal)
    {
        if (effVal == null) return;

        bool diff = false;
        if (baseVal == null && effVal == null) diff = false;
        else if (baseVal == null || effVal == null) diff = true;
        else if (baseVal is float[] f1 && effVal is float[] f2)
        {
            if (f1.Length != f2.Length) diff = true;
            else if (!f1.SequenceEqual(f2)) diff = true;
        }
        else if (!baseVal.Equals(effVal)) diff = true;

        if (diff)
        {
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.TextColored(new Vector4(1f, 0.7f, 0.2f, 1f), FontAwesomeIcon.Bolt.ToIconString());
            ImGui.PopFont();
            if (ImGui.IsItemHovered())
            {
                var valStr = effVal?.ToString() ?? "null";
                if (effVal is float[] fArr) valStr = string.Join(", ", fArr);
                ImGui.SetTooltip($"Active Override: {valStr}");
            }
        }
    }
}
