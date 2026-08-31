using System;
using System.Numerics;
using Cordi.Extensions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Themes;
using Cordi.Configuration;
using Dalamud.Interface.Components;

public sealed partial class UiTheme
{
    public void DrawCollapsableCardWithTable<T>(
        string id,
        string title,
        ref bool expanded,
        IEnumerable<T> collection,
        Action<T, int> drawRow,
        string[]? headers = null,
        bool showCount = false,
        int? explicitCount = null,
        Action? setupColumns = null,
        bool showHeaders = false,
        Action<float>? drawFooter = null,
        Action<float>? drawTopContent = null,
        Action? extraRows = null,
        float maxTableHeight = 0,
        bool collapsible = true,
        string? mutedText = null,
        Action? drawHeaderRight = null)
    {
        int count = explicitCount ?? collection.Count();
        title = showCount ? $"{title}: {count}" : title;

        float scale = ImGuiHelpers.GlobalScale;
        float headerHeight = CollapsableHeaderHeight * scale;
        if (!string.IsNullOrEmpty(mutedText)) headerHeight += ImGui.GetTextLineHeight() + Gap(0.2f);
        if (headerHeight < ImGui.GetFrameHeightWithSpacing()) headerHeight = ImGui.GetFrameHeightWithSpacing();

        float padX = PadX(0.9f);
        float padY = PadY(0.9f);
        float radius = Radius(1.0f);

        var draw = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var availW = ImGui.GetContentRegionAvail().X;

        draw.ChannelsSplit(2);
        draw.ChannelsSetCurrent(1);

        using (var group = ImRaii.Group())
        {
            float titleY = startPos.Y + padY;
            ImGui.SetCursorScreenPos(new Vector2(startPos.X + padX, titleY));

            using (ImRaii.PushColor(ImGuiCol.Text, Text))
            {
                ImGui.TextUnformatted(title);
            }

            if (!string.IsNullOrEmpty(mutedText))
            {
                ImGui.SetCursorScreenPos(new Vector2(startPos.X + padX, titleY + ImGui.GetTextLineHeight() + Gap(0.1f)));
                ImGui.TextColored(MutedText, mutedText);
            }

            float rightElementsY = startPos.Y + (headerHeight - ImGui.GetTextLineHeight()) * 0.5f;

            if (drawHeaderRight != null)
            {
                ImGui.SameLine();
                float chevronArea = collapsible ? (24f * scale) : 0;
                float headerRightCursorX = (startPos.X + availW - padX) - chevronArea - 10f * scale;

                if (headerRightCursorX < ImGui.GetCursorPosX()) headerRightCursorX = ImGui.GetCursorPosX() + 10f;

                ImGui.SetCursorScreenPos(new Vector2(headerRightCursorX, rightElementsY));
                drawHeaderRight();
            }

            if (collapsible)
            {
                using (ImRaii.PushFont(Dalamud.Interface.UiBuilder.IconFont))
                {
                    string icon = expanded ? FontAwesomeIcon.ChevronUp.ToIconString() : FontAwesomeIcon.ChevronDown.ToIconString();
                    var iconSize = ImGui.CalcTextSize(icon);
                    ImGui.SetCursorScreenPos(new Vector2(startPos.X + availW - padX - iconSize.X, rightElementsY));
                    ImGui.SetCursorScreenPos(new Vector2(startPos.X + availW - padX - iconSize.X, rightElementsY));
                    ImGui.TextUnformatted(icon);
                }

                ImGui.SetCursorScreenPos(startPos);
                if (InvisibleButton($"##{id}HeaderBtn", new Vector2(availW, headerHeight)))
                {
                    expanded = !expanded;
                }
            }
            else
            {
                ImGui.SetCursorScreenPos(new Vector2(startPos.X, startPos.Y + headerHeight));
            }

            if (!collapsible || expanded)
            {
                float targetWidth = availW * 0.95f;
                float xOffset = (availW - targetWidth) / 2.0f;
                float effectiveStartX = startPos.X + xOffset;

                ImGui.SetCursorScreenPos(new Vector2(effectiveStartX, startPos.Y + headerHeight + Gap(0.2f)));

                if (count == 0 && drawFooter == null)
                {
                    ImGui.TextUnformatted("No items");
                }
                else
                {
                    if (drawTopContent != null)
                    {
                        ImGui.SetCursorScreenPos(new Vector2(effectiveStartX, ImGui.GetCursorScreenPos().Y));
                        drawTopContent(targetWidth);
                        SpacerY(0.5f);
                    }

                    if (count > 0 || extraRows != null)
                    {
                        ImGui.SetCursorScreenPos(new Vector2(effectiveStartX, ImGui.GetCursorScreenPos().Y));
                        DrawTable(id, collection, drawRow, headers, setupColumns, showHeaders, new Vector2(targetWidth, maxTableHeight), extraRows);
                    }

                    if (drawFooter != null)
                    {
                        SpacerY(0.5f);
                        ImGui.SetCursorScreenPos(new Vector2(effectiveStartX, ImGui.GetCursorScreenPos().Y));
                        drawFooter(targetWidth);
                    }
                }

                ImGui.Dummy(new Vector2(0, padY * 0.5f));
            }
        }
        var itemMin = ImGui.GetItemRectMin();
        var itemMax = ImGui.GetItemRectMax();

        float totalHeight = itemMax.Y - startPos.Y;
        if (totalHeight < headerHeight) totalHeight = headerHeight;

        var endPos = new Vector2(startPos.X + availW, startPos.Y + totalHeight);
        draw.ChannelsSetCurrent(0);

        draw.AddRectFilled(startPos, endPos, ImGui.GetColorU32(CardBg), radius);
        draw.AddRect(startPos, endPos, ImGui.GetColorU32(WindowBorder), radius);

        var headerRectMax = new Vector2(endPos.X, startPos.Y + headerHeight);
        var mousePos = ImGui.GetMousePos();
        bool headerHovered = mousePos.X >= startPos.X && mousePos.X < endPos.X &&
                             mousePos.Y >= startPos.Y && mousePos.Y < headerRectMax.Y;

        if (headerHovered && collapsible)
        {
            draw.AddRectFilled(startPos, headerRectMax, ImGui.GetColorU32(new Vector4(1, 1, 1, 0.05f)), radius, ImDrawFlags.RoundCornersTop);
        }

        draw.ChannelsMerge();

        SpacerY(0.5f);
    }

    public void DrawTable<T>(
        string id,
        IEnumerable<T> collection,
        Action<T, int> drawRow,
        string[]? headers = null,
        Action? setupColumns = null,
        bool showHeaders = false,
        Vector2? outerSize = null,
        Action? extraRows = null)
    {
        int columns = headers?.Length ?? 1;
        var flags = ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp;
        if (outerSize != null && outerSize.Value.Y > 0) flags |= ImGuiTableFlags.ScrollY;

        using (ImRaii.PushColor(ImGuiCol.TableHeaderBg, FrameBg)
                .Push(ImGuiCol.TableBorderStrong, WindowBorder)
                .Push(ImGuiCol.TableBorderLight, WindowBorder))
        using (var table = ImRaii.Table($"##table_{id}", columns, flags, outerSize ?? Vector2.Zero))
        {
            if (table)
            {
                if (setupColumns != null)
                {
                    setupColumns();
                }
                else if (headers != null)
                {
                    foreach (var h in headers)
                    {
                        ImGui.TableSetupColumn(h);
                    }
                }

                if (showHeaders && (headers != null || setupColumns != null))
                {
                    ImGui.TableHeadersRow();
                }

                // Snapshot collection to avoid modification errors during iteration
                var snapshot = collection.ToList();
                int idx = 0;
                foreach (var item in snapshot)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    drawRow(item, idx++);
                }
                extraRows?.Invoke();
            }
        }
    }

    public void DrawTable<T>(string id, IEnumerable<T> collection, Action<T, int> drawRow, int columns)
    {
        using (var table = ImRaii.Table($"##table_{id}", columns, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp))
        {
            if (table)
            {
                // Snapshot collection here too
                var snapshot = collection.ToList();
                int idx = 0;
                foreach (var item in snapshot)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    drawRow(item, idx++);
                }
            }
        }
    }


}
