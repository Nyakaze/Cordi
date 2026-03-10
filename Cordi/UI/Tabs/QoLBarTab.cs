using System;
using System.Linq;
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

public class QoLBarTab : ConfigTabBase
{
    private readonly BarImportExportService importExport;
    private readonly QoLBarOverlay overlay;
    private bool _conditionSetsExpanded = true;
    private string _importBuffer = string.Empty;

    public override string Label => "QoL Bar";

    public QoLBarTab(CordiPlugin plugin, UiTheme theme, BarImportExportService importExport, QoLBarOverlay overlay)
        : base(plugin, theme)
    {
        this.importExport = importExport;
        this.overlay = overlay;
    }

    protected override IReadOnlyList<(string Label, Action Draw)> GetSubTabs()
    {
        var config = plugin.QoLBarConfig;
        return new (string, Action)[]
        {
            ("Bars", () => DrawBarManager(config)),
            ("Conditions", () =>
            {
                DrawDynamicVariables(config);
                DrawConditionSets(config);
            }),
            ("Settings", () => DrawSettings(config)),
        };
    }

    private void DrawBarManager(QoLBarConfig config)
    {
        float availW = ImGui.GetContentRegionAvail().X;
        float scale = ImGuiHelpers.GlobalScale;

        // ── Toolbar ──────────────────────────────────────────────────────────
        if (theme.PrimaryButton("+ New Bar", new Vector2(140 * scale, 0)))
            overlay.AddBar(new BarCfg { Name = $"Bar {config.Bars.Count + 1}", Editing = true });

        ImGui.SameLine();
        if (theme.SecondaryButton("+ Collection", new Vector2(130 * scale, 0)))
        {
            config.Collections.Add(new BarCollectionCfg { Name = $"Collection {config.Collections.Count + 1}" });
            config.Save();
        }

        ImGui.SameLine();
        if (theme.SecondaryButton("Import", new Vector2(100 * scale, 0)))
            ImGui.OpenPopup("##ImportBarPopup");

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

        // ── Collections ───────────────────────────────────────────────────────
        for (int ci = 0; ci < config.Collections.Count; ci++)
        {
            var col = config.Collections[ci];
            ImGui.PushID($"col_{ci}");

            DrawCollectionHeader(config, col, ci);
            theme.SpacerY(0.3f);

            if (!col.Collapsed)
            {
                // Indent the bar cards inside this collection
                ImGui.Indent(16 * scale);
                for (int bi = 0; bi < config.Bars.Count; bi++)
                {
                    if (config.Bars[bi].CollectionId != col.Id) continue;
                    ImGui.PushID($"bar_c{ci}_{bi}");
                    DrawBarCard(config, bi);
                    ImGui.PopID();
                    theme.SpacerY(0.5f);
                }
                ImGui.Unindent(16 * scale);
            }

            ImGui.PopID();
            theme.SpacerY(0.5f);
        }

        // ── Uncollected bars (shown after collections) ────────────────────────
        for (int i = 0; i < config.Bars.Count; i++)
        {
            if (!string.IsNullOrEmpty(config.Bars[i].CollectionId)) continue;
            ImGui.PushID($"bar_unc_{i}");
            DrawBarCard(config, i);
            ImGui.PopID();
            theme.SpacerY(0.5f);
        }

        if (config.Bars.Count == 0 && config.Collections.Count == 0)
        {
            theme.SpacerY(2f);
            var text = "No bars configured. Click '+ New Bar' to create one.";
            var textSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorPosX((availW - textSize.X) * 0.5f);
            ImGui.TextColored(theme.MutedText, text);
        }
    }

    private void DrawCollectionHeader(QoLBarConfig config, BarCollectionCfg col, int ci)
    {
        float scale = ImGuiHelpers.GlobalScale;
        float availW = ImGui.GetContentRegionAvail().X;
        float padX = theme.PadX(0.7f);
        float padY = theme.PadY(0.6f);
        float radius = theme.Radius(1.0f);

        var draw = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();

        draw.ChannelsSplit(2);
        draw.ChannelsSetCurrent(1);

        ImGui.BeginGroup();
        ImGui.Dummy(new Vector2(0, padY));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padX);

        // Collapse toggle (chevron icon)
        ImGui.PushFont(UiBuilder.IconFont);
        var chevron = col.Collapsed ? FontAwesomeIcon.ChevronRight.ToIconString() : FontAwesomeIcon.ChevronDown.ToIconString();
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1, 1, 1, 0.08f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1, 1, 1, 0.15f));
        if (ImGui.Button($"{chevron}##toggle", new Vector2(22 * scale, 0)))
        {
            col.Collapsed = !col.Collapsed;
            config.Save();
        }
        ImGui.PopStyleColor(3);
        ImGui.PopFont();

        ImGui.SameLine(0, theme.Gap(0.4f));

        // Folder icon
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.PushStyleColor(ImGuiCol.Text, theme.Accent);
        ImGui.TextUnformatted(FontAwesomeIcon.Folder.ToIconString());
        ImGui.PopStyleColor();
        ImGui.PopFont();

        ImGui.SameLine(0, theme.Gap(0.4f));

        // Editable name
        ImGui.PushStyleColor(ImGuiCol.Text, theme.Text);
        ImGui.SetNextItemWidth(180 * scale);
        theme.PushInputScope();
        var colName = col.Name;
        if (ImGui.InputText("##colName", ref colName, 64))
        {
            col.Name = colName;
            config.Save();
        }
        theme.PopInputScope();
        ImGui.PopStyleColor();

        // Bar count badge
        int barCount = config.Bars.Count(b => b.CollectionId == col.Id);
        ImGui.SameLine(0, theme.Gap(0.5f));
        ImGui.PushStyleColor(ImGuiCol.Text, theme.MutedText);
        ImGui.TextUnformatted($"{barCount} bar{(barCount != 1 ? "s" : "")}");
        ImGui.PopStyleColor();

        // Right-side action buttons
        float rightEdge = startPos.X + availW - padX;
        float btnW = 24 * scale;
        float btnH = 24 * scale;
        float btnGap = theme.Gap(0.3f);
        float totalBtnWidth = (btnW * 3) + (btnGap * 2);

        // Position at top-right, aligned with the text baseline (padY offset)
        ImGui.SetCursorScreenPos(new Vector2(rightEdge - totalBtnWidth, startPos.Y + padY));

        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, theme.Radius(0.6f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.PushFont(UiBuilder.IconFont);

        // 1. Move Up (Left)
        if (ci > 0)
        {
            if (ImGui.Button(FontAwesomeIcon.ArrowUp.ToIconString(), new Vector2(btnW, btnH)))
            {
                config.Collections.RemoveAt(ci);
                config.Collections.Insert(ci - 1, col);
                config.Save();
            }
            theme.HoverHandIfItem();
        }
        else
            ImGui.Dummy(new Vector2(btnW, btnH));

        ImGui.SameLine(0, btnGap);

        // 2. Move Down (Middle)
        if (ci < config.Collections.Count - 1)
        {
            if (ImGui.Button(FontAwesomeIcon.ArrowDown.ToIconString(), new Vector2(btnW, btnH)))
            {
                config.Collections.RemoveAt(ci);
                config.Collections.Insert(ci + 1, col);
                config.Save();
            }
            theme.HoverHandIfItem();
        }
        else
            ImGui.Dummy(new Vector2(btnW, btnH));

        ImGui.SameLine(0, btnGap);

        // 3. Delete (Right/Corner)
        ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.ColorDanger);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.1f, 0.1f, 1f));
        if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString(), new Vector2(btnW, btnH)))
        {
            foreach (var b in config.Bars)
                if (b.CollectionId == col.Id)
                    b.CollectionId = null;
            config.Collections.RemoveAt(ci);
            config.Save();
            ImGui.PopStyleColor(2);
            ImGui.PopFont();
            ImGui.PopStyleVar(2);
            ImGui.Dummy(new Vector2(0, padY));
            ImGui.EndGroup();
            draw.ChannelsMerge();
            return;
        }
        ImGui.PopStyleColor(2);
        if (ImGui.IsItemHovered()) { ImGui.PopFont(); ImGui.SetTooltip("Delete collection (bars are kept)"); ImGui.PushFont(UiBuilder.IconFont); }

        ImGui.PopFont();
        ImGui.PopStyleVar(2);

        ImGui.Dummy(new Vector2(0, padY));
        ImGui.EndGroup();

        var itemMax = ImGui.GetItemRectMax();
        var endPos = new Vector2(startPos.X + availW, itemMax.Y);

        var headerBg = new Vector4(theme.Accent.X, theme.Accent.Y, theme.Accent.Z, 0.10f);
        draw.ChannelsSetCurrent(0);
        draw.AddRectFilled(startPos, endPos, ImGui.GetColorU32(headerBg), radius);
        draw.AddRect(startPos, endPos, ImGui.GetColorU32(theme.Accent with { W = 0.3f }), radius);

        draw.ChannelsMerge();
        ImGui.SetCursorScreenPos(new Vector2(startPos.X, endPos.Y));
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
        ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);

        if (ImGui.Button(bar.Hidden ? FontAwesomeIcon.Eye.ToIconString() : FontAwesomeIcon.EyeSlash.ToIconString(), new Vector2(btnWidth, 0)))
        {
            overlay.SetBarHidden(i, true);
        }
        theme.HoverHandIfItem();
        if (ImGui.IsItemHovered()) { ImGui.PopFont(); ImGui.SetTooltip(bar.Hidden ? "Show" : "Hide"); ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont); }

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
        if (ImGui.IsItemHovered()) { ImGui.PopFont(); ImGui.SetTooltip("Export to clipboard"); ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont); }

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
        if (ImGui.IsItemHovered()) { ImGui.PopFont(); ImGui.SetTooltip("Delete bar"); ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont); }

        ImGui.PopFont();
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

        ImGui.SameLine();

        ImGui.SetNextItemWidth(smallW);
        var spX = bar.Spacing[0];
        if (ImGui.DragFloat("SpX", ref spX, 0.1f, 0f, 100f, "%.0f"))
        {
            bar.Spacing[0] = spX;
            plugin.QoLBarConfig.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Horizontal Spacing");

        ImGui.SameLine();

        ImGui.SetNextItemWidth(smallW);
        var spY = bar.Spacing[1];
        if (ImGui.DragFloat("SpY", ref spY, 0.1f, 0f, 100f, "%.0f"))
        {
            bar.Spacing[1] = spY;
            plugin.QoLBarConfig.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Vertical Spacing");

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

        ImGui.SameLine();

        var clickThrough = bar.ClickThrough;
        if (ImGui.Checkbox("Click-through", ref clickThrough))
        {
            bar.ClickThrough = clickThrough;
            plugin.QoLBarConfig.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Makes the bar ignore all mouse input");

        ImGui.SameLine();

        var dynVis = bar.DynVisEnabled;
        if (ImGui.Checkbox("Dyn Vis", ref dynVis))
        {
            bar.DynVisEnabled = dynVis;
            plugin.QoLBarConfig.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Enable visibility based on a dynamic variable");

        if (bar.DynVisEnabled)
        {
            ImGui.Indent();
            ImGui.TextDisabled("Show when:");
            ImGui.SameLine();

            ImGui.SetNextItemWidth(100 * scale);
            var vName = bar.DynVisVar;
            if (ImGui.InputTextWithHint("##visVar", "Var Name", ref vName, 32))
            {
                bar.DynVisVar = vName;
                plugin.QoLBarConfig.Save();
            }

            ImGui.SameLine();
            ImGui.Text("==");
            ImGui.SameLine();

            ImGui.SetNextItemWidth(100 * scale);
            var vVal = bar.DynVisVal;
            if (ImGui.InputTextWithHint("##visVal", "Value", ref vVal, 32))
            {
                bar.DynVisVal = vVal;
                plugin.QoLBarConfig.Save();
            }
            ImGui.Unindent();
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

        // Collection assignment row (only shown when collections exist)
        if (config.Collections.Count > 0)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + theme.PadX(0.9f));
            ImGui.PushStyleColor(ImGuiCol.Text, theme.MutedText);
            ImGui.TextUnformatted("Collection:");
            ImGui.PopStyleColor();
            ImGui.SameLine();

            int currentColIdx = 0;
            for (int ci = 0; ci < config.Collections.Count; ci++)
            {
                if (config.Collections[ci].Id == bar.CollectionId)
                { currentColIdx = ci + 1; break; }
            }

            var colItems = new List<string> { "  None" };
            colItems.AddRange(config.Collections.Select(c => $"  {c.Name}"));
            var colArr = colItems.ToArray();

            ImGui.SetNextItemWidth(160 * scale);
            if (ImGui.Combo("##barCol", ref currentColIdx, colArr, colArr.Length))
            {
                bar.CollectionId = currentColIdx == 0 ? null : config.Collections[currentColIdx - 1].Id;
                config.Save();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Assign this bar to a collection group");
        }

        theme.PopInputScope();
    }

    private void DrawConditionSets(QoLBarConfig config)
    {
        float scale = ImGuiHelpers.GlobalScale;

        ImGui.TextDisabled("Variable Context Defs (New)");

        if (theme.PrimaryButton("+ New Variable Context", new Vector2(250 * scale, 0)))
        {
            var newDef = new ShConditionDefinition { Name = $"New Context {config.ConditionDefinitions.Count + 1}", Variable = "job" };
            config.ConditionDefinitions.Add(newDef);
            plugin.QoLBarConfig.Save();
        }

        theme.SpacerY(0.5f);

        if (theme.BeginTable("##varCondTable", 3))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Target Variable Context", ImGuiTableColumnFlags.WidthFixed, 200 * scale);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 60 * scale);
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
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                {
                    config.ConditionDefinitions.RemoveAt(i);
                    plugin.QoLBarConfig.Save();
                    i--;
                }

                ImGui.PopID();
            }

            theme.EndTable();
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
    private void DrawDynamicVariables(QoLBarConfig config)
    {
        bool open = ImGui.CollapsingHeader("     Dynamic Variables", ImGuiTreeNodeFlags.DefaultOpen);

        var headerMin = ImGui.GetItemRectMin();
        var headerMax = ImGui.GetItemRectMax();
        float centerY = headerMin.Y + (headerMax.Y - headerMin.Y) * 0.5f;
        float iconSize = ImGui.GetFontSize();
        var iconPos = new Vector2(headerMin.X + 25 * ImGuiHelpers.GlobalScale, centerY - iconSize * 0.5f);
        ImGui.GetWindowDrawList().AddText(UiBuilder.IconFont, iconSize, iconPos, ImGui.GetColorU32(ImGuiCol.Text), FontAwesomeIcon.Bolt.ToIconString());

        if (!open) return;

        ImGui.Indent(10f);
        theme.SpacerY(0.5f);

        // Header row
        ImGui.TextDisabled("Variable Name");
        ImGui.SameLine(200 * ImGuiHelpers.GlobalScale);
        ImGui.TextDisabled("Source");
        ImGui.SameLine(400 * ImGuiHelpers.GlobalScale);
        ImGui.TextDisabled("Current Value");

        int indexToRemove = -1;

        for (int i = 0; i < config.DynamicVariables.Count; i++)
        {
            var entry = config.DynamicVariables[i];
            ImGui.PushID($"dynvar_{i}");

            // Enabled checkbox
            var enabled = entry.Enabled;
            if (ImGui.Checkbox("##en", ref enabled))
            {
                entry.Enabled = enabled;
                config.Save();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Enable/Disable this variable update");
            ImGui.SameLine();

            // Name
            ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
            var name = entry.VariableName;
            if (ImGui.InputText("##name", ref name, 32))
            {
                entry.VariableName = name;
                config.Save();
            }
            ImGui.SameLine();

            // Source
            ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
            var src = entry.Source;
            if (theme.EnumCombo("##source", ref src))
            {
                entry.Source = src;
                config.Save();
            }
            ImGui.SameLine();

            // Current Value (readonly)
            var currentVal = plugin.VariableService.GetVariable(entry.VariableName);
            ImGui.TextUnformatted(string.IsNullOrEmpty(currentVal) ? "-" : $"\"{currentVal}\"");

            ImGui.SameLine(ImGui.GetContentRegionAvail().X - 30 * ImGuiHelpers.GlobalScale);
            if (theme.IconButton($"##qolVar{indexToRemove}", FontAwesomeIcon.Trash, tooltip: "Delete"))
            {
                indexToRemove = i;
            }
            
            // if (theme.IconButton(FontAwesomeIcon.Trash, "Delete"))
            // {
            //     indexToRemove = i;
            // }

            ImGui.PopID();
        }

        if (indexToRemove >= 0)
        {
            config.DynamicVariables.RemoveAt(indexToRemove);
            config.Save();
        }

        theme.SpacerY(0.5f);
        if (ImGui.Button("+ Add Dynamic Variable"))
        {
            config.DynamicVariables.Add(new DynamicVarEntry { VariableName = "new_var", Enabled = true });
            config.Save();
        }

        ImGui.Unindent(10f);
        theme.SpacerY(1f);
        ImGui.Separator();
        theme.SpacerY(1f);
    }
}
