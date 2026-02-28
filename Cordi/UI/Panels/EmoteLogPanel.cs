using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Cordi.Services;
using Dalamud.Interface;

using Cordi.Core;
using Cordi.Extensions;

namespace Cordi.UI.Panels;

public class EmoteLogPanel
{
    private readonly CordiPlugin _plugin;

    public EmoteLogPanel(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw()
    {
        var logs = _plugin.EmoteLog.Logs;

        lock (logs)
        {
            var buttonSize = new Vector2(ImGui.GetFrameHeight());
            var rightAlignPos = ImGui.GetContentRegionAvail().X - buttonSize.X - ImGui.GetStyle().ItemSpacing.X;

            for (int i = logs.Count - 1; i >= 0; i--)
            {
                var entry = logs[i];
                var time = entry.Timestamp.ToString("HH:mm");

                var text = $"[{time}] {entry.User} used {entry.Emote}";
                if (entry.Count > 1)
                {
                    text += $" [{entry.Count}]";
                }

                ImGui.TextUnformatted(text);

                if (_plugin.Config.EmoteLog.ShowReplyButton)
                {
                    ImGui.SameLine();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + rightAlignPos - ImGui.GetCursorPosX());

                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
                    bool keepTarget = ImGui.GetIO().KeyShift;
                    bool keepRotation = ImGui.GetIO().KeyAlt;

                    if (ImGui.Button($"{FontAwesomeIcon.Reply.ToIconString()}##{i}", buttonSize))
                    {
                        var cmd = entry.Command;
                        if (string.IsNullOrEmpty(cmd))
                        {
                            cmd = entry.Emote;
                            if (!cmd.StartsWith("/")) cmd = "/" + cmd;
                        }

                        _ = _plugin.EmoteLog.PerformEmoteBack(
                            entry.User,
                            entry.World,
                            cmd,
                            entry.GameObjectId,
                            keepTarget,
                            keepRotation
                        );
                    }
                    ImGui.PopStyleVar();
                    ImGui.PopFont();

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip($"Emote Back ({(!string.IsNullOrEmpty(entry.Command) ? entry.Command : entry.Emote)})\n[Shift] Keep target\n[Alt] Keep rotation");
                    }
                }

                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    var target = Service.ObjectTable.FindPlayerByName(entry.User);
                    if (target != null) Service.TargetManager.Target = target;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Left-click to emote back\nRight-click to target");
                }
            }
        }
    }
}
