using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Cordi.Services;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

using Cordi.Configuration;
using Cordi.Core;
using Cordi.Extensions;
using Cordi.UI.Themes;

using Cordi.Services.Features;

namespace Cordi.UI.Panels;

public class EmoteLogPanel
{
    private readonly Configuration.Configuration _config;
    private readonly EmoteLogService _emoteLog;
    private readonly UiTheme _theme;

    public EmoteLogPanel(Configuration.Configuration config, EmoteLogService emoteLog, UiTheme theme)
    {
        _config = config;
        _emoteLog = emoteLog;
        _theme = theme;
    }

    public void Draw(bool textShadow = false)
    {
        var logs = _emoteLog.Logs;
        float rightEdge = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;

        for (int i = logs.Count - 1; i >= 0; i--)
        {
            DrawEntry(logs[i], i, rightEdge, textShadow);
        }
    }

    private void DrawEntry(EmoteEvent entry, int index, float rightEdge, bool textShadow)
    {
        var style = ImGui.GetStyle();
        var replySize = ReplyButtonSize();

        var left = new List<RowSegment>();
        var right = new List<RowSegment>();
        var second = new List<RowSegment>();

        BuildSegments(entry, index, replySize, left, right, second);

        float rowHeight = MathF.Max(ImGui.GetTextLineHeight() + style.FramePadding.Y * 2, replySize.Y);

        using var headerHovered = ImRaii.PushColor(ImGuiCol.HeaderHovered, new Vector4(0f, 0f, 0f, 0f));
        using var headerActive = ImRaii.PushColor(ImGuiCol.HeaderActive, new Vector4(0f, 0f, 0f, 0f));

        float top = ImGui.GetCursorScreenPos().Y;
        ImGui.Selectable($"##emote_{index}", false, ImGuiSelectableFlags.None, new Vector2(0, rowHeight + (second.Count > 0 ? ImGui.GetTextLineHeight() : 0f)));
        ImGui.SetItemAllowOverlap();

        var min = ImGui.GetItemRectMin();
        bool hovered = ImGui.IsItemHovered();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            var target = Service.ObjectTable.FindPlayerByName(entry.User);
            if (target != null) Service.TargetManager.Target = target;
        }

        if (hovered) ImGui.SetTooltip("Right-click to target");

        var drawList = ImGui.GetWindowDrawList();
        float gap = style.ItemSpacing.X;
        float margin = MathF.Max(gap, _theme.Gap(0.5f));
        float textY = top + (rowHeight - ImGui.GetTextLineHeight()) * 0.5f;

        RowRenderer.DrawLeft(drawList, new Vector2(min.X + gap, textY), left, gap, textShadow, _theme);
        RowRenderer.DrawRight(drawList, new Vector2(rightEdge - margin, textY), right, gap, textShadow, _theme);

        if (second.Count > 0)
            RowRenderer.DrawLeft(drawList, new Vector2(min.X + gap, top + rowHeight), second, gap, textShadow, _theme);
    }

    private static Vector2 ReplyButtonSize()
    {
        float frame = ImGui.GetFrameHeight();
        float margin = ImGui.GetStyle().FramePadding.Y * 2f;

        Vector2 glyph;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            glyph = ImGui.CalcTextSize(FontAwesomeIcon.Reply.ToIconString());
        }

        float height = MathF.Max(frame, glyph.Y + margin);

        return new Vector2(MathF.Max(glyph.X + margin, height), height);
    }

    private void BuildSegments(EmoteEvent entry, int index, Vector2 replySize, List<RowSegment> left, List<RowSegment> right, List<RowSegment> second)
    {
        uint normal = ImGui.GetColorU32(ImGuiCol.Text);
        uint muted = ImGui.GetColorU32(ImGuiCol.TextDisabled);

        foreach (var field in _config.EmoteLog.RowLayout.Fields)
        {
            if (!field.Enabled) continue;

            var bucket = field.Side switch
            {
                RowSide.Right => right,
                RowSide.Second => second,
                _ => left,
            };

            uint color = field.Style switch
            {
                RowFieldStyle.Muted => muted,
                RowFieldStyle.Accent => ImGui.GetColorU32(_theme.Accent),
                RowFieldStyle.Custom => ImGui.GetColorU32(field.Color),
                _ => normal,
            };

            if (field.Key == RowFieldCatalog.Reply)
            {
                bucket.Add(new RowSegment
                {
                    Width = replySize.X,
                    Draw = pos => DrawReplyButton(entry, index, pos, replySize),
                });

                continue;
            }

            var text = FieldText(entry, field);
            if (string.IsNullOrEmpty(text)) continue;

            bucket.Add(new RowSegment
            {
                Text = text,
                Color = color,
                Width = ImGui.CalcTextSize(text).X,
            });
        }
    }

    private void DrawReplyButton(EmoteEvent entry, int index, Vector2 pos, Vector2 size)
    {
        var line = ImGui.GetTextLineHeight();

        ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y - (size.Y - line) * 0.5f));

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero))
        {
            bool keepTarget = ImGui.GetIO().KeyShift;
            bool keepRotation = ImGui.GetIO().KeyAlt;

            if (ImGui.Button($"{FontAwesomeIcon.Reply.ToIconString()}##reply{index}", size))
            {
                var command = entry.Command;
                if (string.IsNullOrEmpty(command))
                {
                    command = entry.EmoteName;
                    if (!command.StartsWith("/")) command = "/" + command;
                }

                _emoteLog.PerformEmoteBack(entry.User, entry.World, command, entry.GameObjectId, keepTarget, keepRotation);
            }
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.SetTooltip($"Emote Back ({(!string.IsNullOrEmpty(entry.Command) ? entry.Command : entry.EmoteName)})\n[Shift] Keep target\n[Alt] Keep rotation");
        }
    }

    private static string? FieldText(EmoteEvent entry, RowFieldConfig field) => field.Key switch
    {
        RowFieldCatalog.Time => RowFieldCatalog.Format(field.Format, token => RowFieldCatalog.TimePart(entry.LastUpdate, token)),
        RowFieldCatalog.Name => RowFieldCatalog.Format(field.Format, token => RowFieldCatalog.NamePart(entry.User, token)),
        RowFieldCatalog.World => RowFieldCatalog.Format(field.Format, token => token == "world" ? entry.World : null),
        RowFieldCatalog.Verb => RowFieldCatalog.Format(field.Format, _ => null),
        RowFieldCatalog.Emote => RowFieldCatalog.Format(field.Format, token => token switch
        {
            "emote" => entry.EmoteName,
            "command" => entry.Command,
            _ => null,
        }),
        RowFieldCatalog.Command => RowFieldCatalog.Format(field.Format, token => token == "command" ? entry.Command : null),
        RowFieldCatalog.Count => entry.Count > 1
            ? RowFieldCatalog.Format(field.Format, token => token == "count" ? entry.Count.ToString() : null)
            : null,
        _ => null,
    };
}
