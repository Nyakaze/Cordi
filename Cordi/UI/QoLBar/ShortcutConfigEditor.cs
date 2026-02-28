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

/// <summary>
/// Static methods for drawing shortcut configuration UI (General, Style, Transforms, Conditions tabs).
/// Extracted from ShortcutRenderer to separate config editing concerns from rendering concerns.
/// </summary>
public static class ShortcutConfigEditor
{
    private static readonly UiTheme _defaultTheme = new UiTheme();

    private static string[] _defNamesCache = Array.Empty<string>();
    private static string[] _defIdsCache = Array.Empty<string>();
    private static int _lastDefHash = 0;

    public static void DrawConfigEditor(ShCfg cfg, bool isOverride, UiTheme? theme = null, ShCfg? effectiveCfg = null)
    {
        theme ??= _defaultTheme;

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
    }

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

            int currentHash = defs.Count;
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                currentHash = HashCode.Combine(currentHash, d.Name, d.ID);
            }

            if (currentHash != _lastDefHash || _defNamesCache.Length != defs.Count)
            {
                _defNamesCache = defs.Select(d => d.Name ?? "Unnamed").ToArray();
                _defIdsCache = defs.Select(d => d.ID ?? string.Empty).ToArray();
                _lastDefHash = currentHash;
            }

            var defNames = _defNamesCache;
            var defIds = _defIdsCache;
            float scale = ImGuiHelpers.GlobalScale;

            if (cfg.Conditions == null) cfg.Conditions = new();

            for (int i = 0; i < cfg.Conditions.Count; i++)
            {
                var cond = cfg.Conditions[i];
                if (cond.Cases == null) cond.Cases = new();
                ImGui.PushID(i);

                var currentIdx = Array.IndexOf(defIds, cond.ConditionID);
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

    public static void DrawOverrideIndicator<T>(T baseVal, T? effVal) where T : struct
    {
        if (!effVal.HasValue) return;
        if (EqualityComparer<T>.Default.Equals(baseVal, effVal.Value)) return;
        DrawThunderbolt(effVal.Value.ToString() ?? "null");
    }

    public static void DrawOverrideIndicator(string? baseVal, string? effVal)
    {
        if (effVal == null) return;
        if (baseVal == effVal) return;
        DrawThunderbolt(effVal);
    }

    public static void DrawOverrideIndicator(float[]? baseVal, float[]? effVal)
    {
        if (effVal == null) return;
        bool diff = false;
        if (baseVal == null) diff = true;
        else if (baseVal.Length != effVal.Length) diff = true;
        else if (!baseVal.SequenceEqual(effVal)) diff = true;

        if (diff) DrawThunderbolt(string.Join(", ", effVal));
    }

    private static void DrawThunderbolt(string tooltipVal)
    {
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(new Vector4(1f, 0.7f, 0.2f, 1f), FontAwesomeIcon.Bolt.ToIconString());
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip($"Active Override: {tooltipVal}");
        }
    }
}
