using System;
using System.Numerics;
using System.Collections.Generic;
using Cordi.Configuration.QoLBar;
using Cordi.Core;
using Cordi.Services.QoLBar;
using Cordi.UI.QoLBar;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;

namespace Cordi.UI.Tabs;

public class QoLBarTab
{
    private readonly CordiPlugin plugin;
    private readonly UiTheme theme;
    private readonly BarImportExportService importExport;
    private readonly QoLBarOverlay overlay;
    private int selectedSubTab = 0;
    private bool _conditionSetsExpanded = true;
    private string _importBuffer = string.Empty;

    public QoLBarTab(CordiPlugin plugin, UiTheme theme, BarImportExportService importExport, QoLBarOverlay overlay)
    {
        this.plugin = plugin;
        this.theme = theme;
        this.importExport = importExport;
        this.overlay = overlay;
    }

    public void Draw()
    {
        var config = plugin.QoLBarConfig;

        theme.SpacerY(1f);

        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, theme.Radius());
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(theme.Gap(0.5f), theme.Gap(0.5f)));

        float btnW = ImGui.GetContentRegionAvail().X / 3 - theme.Gap(0.5f);
        float btnH = 32f * ImGuiHelpers.GlobalScale;

        void SubTabButton(string label, int idx)
        {
            if (selectedSubTab == idx)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, theme.Accent);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, theme.Accent);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, theme.Accent);
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, theme.FrameBg);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, theme.FrameBgHover);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, theme.FrameBgActive);
            }

            if (ImGui.Button(label, new Vector2(btnW, btnH)))
                selectedSubTab = idx;
            theme.HoverHandIfItem();

            ImGui.PopStyleColor(3);
        }

        SubTabButton("Bars", 0);
        ImGui.SameLine();
        SubTabButton("Conditions", 1);
        ImGui.SameLine();
        SubTabButton("Settings", 2);

        ImGui.PopStyleVar(2);

        theme.SpacerY(1f);
        ImGui.Separator();
        theme.SpacerY(1f);

        switch (selectedSubTab)
        {
            case 0: DrawBarManager(config); break;
            case 1: DrawConditionSets(config); break;
            case 2: DrawSettings(config); break;
        }
    }

    private void DrawBarManager(QoLBarConfig config)
    {
        float availW = ImGui.GetContentRegionAvail().X;
        float scale = ImGuiHelpers.GlobalScale;

        if (theme.PrimaryButton("+ New Bar", new Vector2(140 * scale, 0)))
        {
            overlay.AddBar(new BarCfg { Name = $"Bar {config.Bars.Count + 1}", Editing = true });
        }
        ImGui.SameLine();
        if (theme.SecondaryButton("Import", new Vector2(100 * scale, 0)))
        {
            ImGui.OpenPopup("##ImportBarPopup");
        }

        if (ImGui.BeginPopup("##ImportBarPopup"))
        {
            ImGui.TextUnformatted("Paste import string:");
            ImGui.SetNextItemWidth(400 * scale);
            ImGui.InputText("##importInput", ref _importBuffer, 100000);
            if (theme.PrimaryButton("Import##btn"))
            {
                var result = importExport.TryImport(_importBuffer);
                if (result.bar != null)
                {
                    overlay.AddBar(result.bar);
                    _importBuffer = string.Empty;
                    ImGui.CloseCurrentPopup();
                }
                else if (result.shortcut != null)
                {
                    var bar = new BarCfg { Name = "Imported", Editing = true };
                    bar.ShortcutList.Add(result.shortcut);
                    overlay.AddBar(bar);
                    _importBuffer = string.Empty;
                    ImGui.CloseCurrentPopup();
                }
            }
            ImGui.EndPopup();
        }

        theme.SpacerY(1f);

        for (int i = 0; i < config.Bars.Count; i++)
        {
            ImGui.PushID(i);
            DrawBarCard(config, i);
            ImGui.PopID();
            theme.SpacerY(0.5f);
        }

        if (config.Bars.Count == 0)
        {
            theme.SpacerY(2f);
            var text = "No bars configured. Click '+ New Bar' to create one.";
            var textSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorPosX((availW - textSize.X) * 0.5f);
            ImGui.TextColored(theme.MutedText, text);
        }
    }

    private void DrawBarCard(QoLBarConfig config, int i)
    {
        var bar = config.Bars[i];
        float scale = ImGuiHelpers.GlobalScale;
        float padX = theme.PadX(0.9f);
        float padY = theme.PadY(0.9f);
        float radius = theme.Radius(1.0f);

        var draw = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var availW = ImGui.GetContentRegionAvail().X;

        draw.ChannelsSplit(2);
        draw.ChannelsSetCurrent(1);

        ImGui.BeginGroup();
        ImGui.Dummy(new Vector2(0, padY));

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padX);

        ImGui.PushStyleColor(ImGuiCol.Text, theme.Text);
        ImGui.PushFont(Dalamud.Interface.UiBuilder.DefaultFont);

        var nameWidth = availW * 0.3f;
        ImGui.SetNextItemWidth(nameWidth);
        theme.PushInputScope();
        var name = bar.Name;
        if (ImGui.InputText("##barName", ref name, 64))
        {
            bar.Name = name;
            plugin.QoLBarConfig.Save();
        }
        theme.PopInputScope();

        ImGui.PopFont();
        ImGui.PopStyleColor();

        ImGui.SameLine();

        var dockLabel = bar.DockSide.ToString();
        ImGui.PushStyleColor(ImGuiCol.Text, theme.MutedText);
        ImGui.TextUnformatted($"Dock: {dockLabel}");
        ImGui.PopStyleColor();

        ImGui.SameLine();

        float rightEdge = startPos.X + availW - padX;
        float btnWidth = 28 * scale;
        float btnGap = theme.Gap(0.3f);
        float totalBtns = btnWidth * 5 + btnGap * 4;

        ImGui.SetCursorScreenPos(new Vector2(rightEdge - totalBtns, ImGui.GetCursorScreenPos().Y));

        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, theme.Radius(0.6f));

        if (ImGui.Button(bar.Hidden ? FontAwesomeIcon.Eye.ToIconString() : FontAwesomeIcon.EyeSlash.ToIconString(), new Vector2(btnWidth, 0)))
        {
            overlay.SetBarHidden(i, true);
        }
        theme.HoverHandIfItem();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(bar.Hidden ? "Show" : "Hide");

        ImGui.SameLine(0, btnGap);
        if (ImGui.Button(FontAwesomeIcon.ArrowUp.ToIconString(), new Vector2(btnWidth, 0)))
            overlay.ShiftBar(i, false);
        theme.HoverHandIfItem();

        ImGui.SameLine(0, btnGap);
        if (ImGui.Button(FontAwesomeIcon.ArrowDown.ToIconString(), new Vector2(btnWidth, 0)))
            overlay.ShiftBar(i, true);
        theme.HoverHandIfItem();

        ImGui.SameLine(0, btnGap);
        if (ImGui.Button(FontAwesomeIcon.FileExport.ToIconString(), new Vector2(btnWidth, 0)))
        {
            ImGui.SetClipboardText(importExport.ExportBar(bar, false));
        }
        theme.HoverHandIfItem();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Export to clipboard");

        ImGui.SameLine(0, btnGap);
        ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.ColorDanger);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.1f, 0.1f, 1f));
        if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString(), new Vector2(btnWidth, 0)))
        {
            if (config.ExportOnDelete)
                ImGui.SetClipboardText(importExport.ExportBar(bar, false));
            overlay.RemoveBar(i);
        }
        ImGui.PopStyleColor(2);
        theme.HoverHandIfItem();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Delete bar");

        ImGui.PopStyleVar();

        theme.SpacerY(0.3f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padX);

        DrawBarInlineSettings(bar, i, config);

        ImGui.Dummy(new Vector2(0, padY));
        ImGui.EndGroup();

        var itemMax = ImGui.GetItemRectMax();
        var endPos = new Vector2(startPos.X + availW, itemMax.Y);

        draw.ChannelsSetCurrent(0);
        draw.AddRectFilled(startPos, endPos, ImGui.GetColorU32(theme.CardBg), radius);
        draw.AddRect(startPos, endPos, ImGui.GetColorU32(theme.WindowBorder), radius);

        if (ImGui.IsMouseHoveringRect(startPos, endPos))
            draw.AddRectFilled(startPos, endPos, ImGui.GetColorU32(new Vector4(1, 1, 1, 0.02f)), radius);

        draw.ChannelsMerge();
        ImGui.SetCursorScreenPos(new Vector2(startPos.X, endPos.Y));
    }

    private void DrawBarInlineSettings(BarCfg bar, int barIdx, QoLBarConfig config)
    {
        float scale = ImGuiHelpers.GlobalScale;
        float itemW = 100 * scale;
        float smallW = 55 * scale;

        theme.PushInputScope();

        // Row 1: Dock, Align, Visibility, Condition
        var dockIdx = (int)bar.DockSide;
        ImGui.SetNextItemWidth(itemW);
        if (ImGui.Combo("##dock", ref dockIdx, "Top\0Right\0Bottom\0Left\0Undocked\0"))
        {
            bar.DockSide = (BarDock)dockIdx;
            if (barIdx < overlay.Bars.Count) overlay.Bars[barIdx].SetupPivot();
            plugin.QoLBarConfig.Save();
        }

        ImGui.SameLine();

        var alignIdx = (int)bar.Alignment;
        ImGui.SetNextItemWidth(itemW);
        if (ImGui.Combo("##align", ref alignIdx, "Left/Top\0Center\0Right/Bottom\0"))
        {
            bar.Alignment = (BarAlign)alignIdx;
            if (barIdx < overlay.Bars.Count) overlay.Bars[barIdx].SetupPivot();
            plugin.QoLBarConfig.Save();
        }

        ImGui.SameLine();

        var visIdx = (int)bar.Visibility;
        ImGui.SetNextItemWidth(itemW);
        if (ImGui.Combo("##vis", ref visIdx, "Slide\0Immediate\0Always\0"))
        {
            bar.Visibility = (BarVisibility)visIdx;
            plugin.QoLBarConfig.Save();
        }

        ImGui.SameLine();

        if (bar.ConditionSet >= 0 && bar.ConditionSet < config.ConditionSets.Count)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, theme.MutedText);
            ImGui.TextUnformatted($"Cnd: {config.ConditionSets[bar.ConditionSet].Name}");
            ImGui.PopStyleColor();
        }

        // Row 2: Width, Height, Cols, Scale, Edit, Lock, No BG, Opacity
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + theme.PadX(0.9f));

        ImGui.SetNextItemWidth(smallW);
        var btnW = bar.ButtonWidth;
        if (ImGui.DragFloat("W", ref btnW, 1f, 20f, 500f, "%.0f"))
        {
            bar.ButtonWidth = btnW;
            plugin.QoLBarConfig.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Button Width");

        ImGui.SameLine();

        ImGui.SetNextItemWidth(smallW);
        var btnH = bar.ButtonHeight;
        if (ImGui.DragFloat("H", ref btnH, 1f, 0f, 500f, btnH == 0 ? "Auto" : "%.0f"))
        {
            bar.ButtonHeight = Math.Max(0, btnH);
            plugin.QoLBarConfig.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Button Height (0 = auto from font)");

        ImGui.SameLine();

        ImGui.SetNextItemWidth(smallW);
        var cols = bar.Columns;
        if (ImGui.DragInt("Col", ref cols, 0.1f, 0, 20))
        {
            bar.Columns = cols;
            plugin.QoLBarConfig.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Columns (0 = single row)");

        ImGui.SameLine();

        ImGui.SetNextItemWidth(smallW);
        var barScale = bar.Scale;
        if (ImGui.DragFloat("Scl", ref barScale, 0.01f, 0.1f, 5f, "%.2f"))
        {
            bar.Scale = barScale;
            plugin.QoLBarConfig.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Scale (multiplies width and height)");

        // Row 3: Checkboxes
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + theme.PadX(0.9f));

        var editing = bar.Editing;
        if (ImGui.Checkbox("Edit", ref editing))
        {
            bar.Editing = editing;
            plugin.QoLBarConfig.Save();
        }

        ImGui.SameLine();

        var locked = bar.LockedPosition;
        if (ImGui.Checkbox("Lock", ref locked))
        {
            bar.LockedPosition = locked;
            plugin.QoLBarConfig.Save();
        }

        ImGui.SameLine();

        var noBg = bar.NoBackground;
        if (ImGui.Checkbox("No BG", ref noBg))
        {
            bar.NoBackground = noBg;
            plugin.QoLBarConfig.Save();
        }

        if (!bar.NoBackground)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(smallW);
            var opacity = bar.Opacity;
            if (ImGui.DragFloat("##opacity", ref opacity, 0.01f, 0f, 1f, "%.2f"))
            {
                bar.Opacity = Math.Clamp(opacity, 0f, 1f);
                plugin.QoLBarConfig.Save();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Background Opacity");
        }

        theme.PopInputScope();
    }

    private void DrawConditionSets(QoLBarConfig config)
    {
        float scale = ImGuiHelpers.GlobalScale;

        ImGui.TextDisabled("Variable Conditions (New)");

        if (theme.PrimaryButton("+ New Variable Condition", new Vector2(200 * scale, 0)))
        {
            config.ConditionDefinitions.Add(new ShConditionDefinition { Name = $"New Condition {config.ConditionDefinitions.Count + 1}" });
            plugin.QoLBarConfig.Save();
        }

        theme.SpacerY(0.5f);

        if (theme.BeginTable("##varCondTable", 3))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Variable", ImGuiTableColumnFlags.WidthFixed, 180 * scale);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 180 * scale);
            ImGui.TableHeadersRow();

            for (int i = 0; i < config.ConditionDefinitions.Count; i++)
            {
                var def = config.ConditionDefinitions[i];
                ImGui.PushID($"def{i}");

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                var name = def.Name;
                if (ImGui.InputText("##name", ref name, 64))
                {
                    def.Name = name;
                    plugin.QoLBarConfig.Save();
                }

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                var variable = def.Variable;
                if (ImGui.InputText("##var", ref variable, 64))
                {
                    def.Variable = variable;
                    plugin.QoLBarConfig.Save();
                }

                ImGui.TableNextColumn();
                if (theme.SecondaryButton($"Cases ({def.Cases.Count})"))
                {
                    ImGui.OpenPopup($"EditCasesPopup");
                }

                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                {
                    config.ConditionDefinitions.RemoveAt(i);
                    plugin.QoLBarConfig.Save();
                    i--;
                }

                if (ImGui.BeginPopup($"EditCasesPopup"))
                {
                    ImGui.TextColored(theme.Accent, $"Cases for '{def.Name}'");
                    theme.SpacerY(0.5f);

                    if (theme.BeginTable("##casesTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
                    {
                        ImGui.TableSetupColumn("Operator", ImGuiTableColumnFlags.WidthFixed, 100 * scale);
                        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Override", ImGuiTableColumnFlags.WidthFixed, 100 * scale);
                        ImGui.TableSetupColumn("##del", ImGuiTableColumnFlags.WidthFixed, 30 * scale);
                        ImGui.TableHeadersRow();

                        for (int k = 0; k < def.Cases.Count; k++)
                        {
                            var c = def.Cases[k];
                            ImGui.PushID($"case{k}");
                            ImGui.TableNextRow();

                            ImGui.TableNextColumn();
                            ImGui.SetNextItemWidth(-1);
                            var op = (int)c.Operator;
                            if (ImGui.Combo("##op", ref op, "==\0!=\0>\0<\0Contains\0"))
                            {
                                c.Operator = (ConditionOperator)op;
                                plugin.QoLBarConfig.Save();
                            }

                            ImGui.TableNextColumn();
                            ImGui.SetNextItemWidth(-1);
                            var val = c.Value;
                            if (ImGui.InputText("##val", ref val, 128))
                            {
                                c.Value = val;
                                plugin.QoLBarConfig.Save();
                            }

                            ImGui.TableNextColumn();
                            if (theme.SecondaryButton("Settings##ovr"))
                            {
                                ImGui.OpenPopup("EditCaseOverridePopup");
                            }

                            if (ImGui.BeginPopup("EditCaseOverridePopup"))
                            {
                                // We need to access DrawConfigEditor from QoLBarTab? 
                                // DrawConfigEditor is in ShortcutRenderer. 
                                // We might need to duplicate it or move it to a shared place?
                                // QoLBarTab doesn't have access to ShortcutRenderer instance methods easily.
                                // But Wait, QoLBarTab manages the high level config.
                                // Inspecting QoLBarTab.cs might reveal if it has similar methods.
                                // If not, we definitely need to move DrawConfigEditor to a shared static helper or service.

                                ImGui.TextColored(theme.Accent, "Override Settings");
                                ImGui.Separator();

                                ShortcutRenderer.DrawConfigEditor(c.Override, true);

                                theme.SpacerY(0.5f);
                                if (theme.Button("Close")) ImGui.CloseCurrentPopup();

                                ImGui.EndPopup();
                            }

                            ImGui.TableNextColumn();
                            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                            {
                                def.Cases.RemoveAt(k);
                                plugin.QoLBarConfig.Save();
                                k--;
                            }

                            ImGui.PopID();
                        }
                        theme.EndTable();
                    }

                    theme.SpacerY(0.5f);
                    if (theme.PrimaryButton("+ Add Case"))
                    {
                        def.Cases.Add(new ShConditionCaseDef());
                        plugin.QoLBarConfig.Save();
                    }

                    ImGui.EndPopup();
                }



                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        theme.SpacerY(1f);
        ImGui.Separator();
        theme.SpacerY(1f);

        ImGui.TextDisabled("Game Condition Sets (Legacy)");

        if (theme.SecondaryButton("+ New Game Condition Set", new Vector2(250 * scale, 0)))
        {
            config.ConditionSets.Add(new CndSetCfg { Name = $"Set {config.ConditionSets.Count + 1}" });
            plugin.QoLBarConfig.Save();
        }

        theme.SpacerY(1f);

        for (int i = 0; i < config.ConditionSets.Count; i++)
        {
            ImGui.PushID(i);
            DrawConditionSetCard(config, i);
            ImGui.PopID();
            theme.SpacerY(0.5f);
        }

        if (config.ConditionSets.Count == 0 && config.ConditionDefinitions.Count == 0)
        {
            theme.SpacerY(2f);
            var text = "No conditions defined.";
            var textSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - textSize.X) * 0.5f);
            ImGui.TextColored(theme.MutedText, text);
        }
    }

    private void DrawConditionSetCard(QoLBarConfig config, int idx)
    {
        var set = config.ConditionSets[idx];
        bool expanded = true;

        theme.DrawPluginCardAuto(
            id: $"cndSet{idx}",
            title: $"[{idx + 1}] {set.Name}",
            drawContent: (availW) =>
            {
                float scale = ImGuiHelpers.GlobalScale;

                theme.PushInputScope();
                ImGui.SetNextItemWidth(200 * scale);
                var name = set.Name;
                if (ImGui.InputText("Name", ref name, 64))
                {
                    set.Name = name;
                    plugin.QoLBarConfig.Save();
                }
                theme.PopInputScope();

                theme.SpacerY(0.5f);

                for (int j = 0; j < set.Conditions.Count; j++)
                {
                    ImGui.PushID(j);
                    DrawConditionRow(set, j);
                    ImGui.PopID();
                }

                theme.SpacerY(0.5f);

                if (theme.SecondaryButton("+ Add Condition", new Vector2(160 * scale, 0)))
                {
                    set.Conditions.Add(new CndCfg());
                    plugin.QoLBarConfig.Save();
                }

                ImGui.SameLine();

                ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.ColorDanger);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.1f, 0.1f, 1f));
                if (ImGui.Button("Delete Set"))
                {
                    config.ConditionSets.RemoveAt(idx);
                    plugin.QoLBarConfig.Save();
                }
                ImGui.PopStyleColor(2);
                theme.HoverHandIfItem();
            },
            enabled: ref expanded,
            showCheckbox: false
        );
    }

    private void DrawConditionRow(CndSetCfg set, int j)
    {
        var cnd = set.Conditions[j];
        float scale = ImGuiHelpers.GlobalScale;
        float itemW = 100 * scale;

        theme.PushInputScope();

        if (j > 0)
        {
            var opIdx = (int)cnd.Operator;
            ImGui.SetNextItemWidth(70 * scale);
            if (ImGui.Combo("##op", ref opIdx, "AND\0OR\0EQUALS\0XOR\0"))
            {
                cnd.Operator = (BinaryOperator)opIdx;
                plugin.QoLBarConfig.Save();
            }
            ImGui.SameLine();
        }

        var negate = cnd.Negate;
        if (ImGui.Checkbox("NOT", ref negate))
        {
            cnd.Negate = negate;
            plugin.QoLBarConfig.Save();
        }
        ImGui.SameLine();

        var conditionService = plugin.ConditionService;
        var allConditions = conditionService?.GetAllConditions();
        if (allConditions != null)
        {
            var condNames = new string[allConditions.Count];
            var condIdx = 0;
            for (int k = 0; k < allConditions.Count; k++)
            {
                condNames[k] = allConditions[k].Name;
                if (allConditions[k].ID == cnd.ID)
                    condIdx = k;
            }

            ImGui.SetNextItemWidth(itemW * 1.5f);
            if (ImGui.Combo("##cond", ref condIdx, condNames, condNames.Length))
            {
                cnd.ID = allConditions[condIdx].ID;
                plugin.QoLBarConfig.Save();
            }
        }

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.ColorDanger);
        if (ImGui.SmallButton("×"))
        {
            set.Conditions.RemoveAt(j);
            plugin.QoLBarConfig.Save();
        }
        ImGui.PopStyleColor();
        theme.HoverHandIfItem();

        theme.PopInputScope();
    }

    private void DrawSettings(QoLBarConfig config)
    {
        float scale = ImGuiHelpers.GlobalScale;

        bool enabled = true;

        theme.DrawPluginCardAuto(
            id: "qolbar-general",
            title: "General Settings",
            drawContent: (availW) =>
            {
                theme.PushInputScope();

                var exportOnDelete = config.ExportOnDelete;
                if (ImGui.Checkbox("Export on Delete", ref exportOnDelete))
                {
                    config.ExportOnDelete = exportOnDelete;
                    plugin.QoLBarConfig.Save();
                }
                theme.HoverHandIfItem();

                ImGui.SameLine();

                var alwaysDisplay = config.AlwaysDisplayBars;
                if (ImGui.Checkbox("Always Display Bars", ref alwaysDisplay))
                {
                    config.AlwaysDisplayBars = alwaysDisplay;
                    plugin.QoLBarConfig.Save();
                }
                theme.HoverHandIfItem();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Bars will remain visible even when logged out.");

                var useIconFrame = config.UseIconFrame;
                if (ImGui.Checkbox("Use Hotbar Frames on Icons", ref useIconFrame))
                {
                    config.UseIconFrame = useIconFrame;
                    plugin.QoLBarConfig.Save();
                }
                theme.HoverHandIfItem();

                ImGui.SameLine();

                var useHR = config.UseHRIcons;
                if (ImGui.Checkbox("High-Resolution Icons", ref useHR))
                {
                    config.UseHRIcons = useHR;
                    plugin.QoLBarConfig.Save();
                }
                theme.HoverHandIfItem();

                var noCache = config.NoConditionCache;
                if (ImGui.Checkbox("Disable Condition Caching", ref noCache))
                {
                    config.NoConditionCache = noCache;
                    plugin.QoLBarConfig.Save();
                    if (plugin.ConditionService != null) plugin.ConditionService.NoCacheMode = noCache;
                }
                theme.HoverHandIfItem();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Disables the 100ms delay between checking conditions, increasing CPU load.");

                ImGui.SetNextItemWidth(100 * scale);
                var fontSize = config.FontSize;
                if (ImGui.DragFloat("Font Size", ref fontSize, 0.5f, 8f, 64f, "%.0f"))
                {
                    config.FontSize = fontSize;
                    plugin.QoLBarConfig.Save();
                }

                ImGui.SetNextItemWidth(100 * scale);
                var backupTimer = config.BackupTimer;
                if (ImGui.InputInt("Backup Timer (min)", ref backupTimer))
                {
                    config.BackupTimer = backupTimer;
                    plugin.QoLBarConfig.Save();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Minutes between automatic backups. 0 = disabled.");

                theme.PopInputScope();
            },
            enabled: ref enabled,
            showCheckbox: false
        );

        theme.SpacerY(1f);

        theme.DrawPluginCardAuto(
            id: "qolbar-pie",
            title: "Pie Menu Settings",
            drawContent: (availW) =>
            {
                theme.PushInputScope();

                ImGui.SetNextItemWidth(100 * scale);
                var pieOp = config.PieOpacity;
                if (ImGui.DragInt("Opacity", ref pieOp, 0.5f, 0, 255))
                {
                    config.PieOpacity = pieOp;
                    plugin.QoLBarConfig.Save();
                }

                var altAngle = config.PieAlternateAngle;
                if (ImGui.Checkbox("Alternate Angle", ref altAngle))
                {
                    config.PieAlternateAngle = altAngle;
                    plugin.QoLBarConfig.Save();
                }
                theme.HoverHandIfItem();

                var center = config.PiesAlwaysCenter;
                if (ImGui.Checkbox("Appear in Center", ref center))
                {
                    config.PiesAlwaysCenter = center;
                    if (!center)
                    {
                        config.PiesMoveMouse = false;
                        config.PiesReturnMouse = false;
                        config.PiesReadjustMouse = false;
                    }
                    plugin.QoLBarConfig.Save();
                }
                theme.HoverHandIfItem();

                if (config.PiesAlwaysCenter)
                {
                    ImGui.SameLine();
                    var moveMouse = config.PiesMoveMouse;
                    if (ImGui.Checkbox("Center Mouse on Open", ref moveMouse))
                    {
                        config.PiesMoveMouse = moveMouse;
                        if (!moveMouse)
                        {
                            config.PiesReturnMouse = false;
                            config.PiesReadjustMouse = false;
                        }
                        plugin.QoLBarConfig.Save();
                    }
                    theme.HoverHandIfItem();
                }

                if (config.PiesMoveMouse)
                {
                    var returnMouse = config.PiesReturnMouse;
                    if (ImGui.Checkbox("Return Mouse on Close", ref returnMouse))
                    {
                        config.PiesReturnMouse = returnMouse;
                        if (!returnMouse)
                            config.PiesReadjustMouse = false;
                        plugin.QoLBarConfig.Save();
                    }
                    theme.HoverHandIfItem();
                }

                theme.PopInputScope();
            },
            enabled: ref enabled,
            showCheckbox: false
        );

        theme.SpacerY(1f);

        theme.DrawPluginCardAuto(
            id: "qolbar-optout",
            title: "Hide UI Opt-Outs",
            drawContent: (availW) =>
            {
                var optGameUI = config.OptOutGameUIOffHide;
                if (ImGui.Checkbox("Game UI Toggled", ref optGameUI))
                {
                    config.OptOutGameUIOffHide = optGameUI;
                    plugin.QoLBarConfig.Save();
                }
                theme.HoverHandIfItem();

                ImGui.SameLine();

                var optCut = config.OptOutCutsceneHide;
                if (ImGui.Checkbox("In Cutscene", ref optCut))
                {
                    config.OptOutCutsceneHide = optCut;
                    plugin.QoLBarConfig.Save();
                }
                theme.HoverHandIfItem();

                ImGui.SameLine();

                var optGpose = config.OptOutGPoseHide;
                if (ImGui.Checkbox("In /gpose", ref optGpose))
                {
                    config.OptOutGPoseHide = optGpose;
                    plugin.QoLBarConfig.Save();
                }
                theme.HoverHandIfItem();
            },
            enabled: ref enabled,
            showCheckbox: false
        );
    }
}
