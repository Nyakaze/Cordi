using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Cordi.Services;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

using Cordi.Core;
using Cordi.Extensions;

namespace Cordi.UI.Panels;

public class EmoteLogPanel
{
    private readonly CordiPlugin _plugin;

    private static readonly uint ShadowColor = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.8f));
    private static readonly Vector2 ShadowOffset = new(1f, 1f);

    public EmoteLogPanel(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw(bool textShadow = false)
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

                if (textShadow)
                {
                    var drawList = ImGui.GetWindowDrawList();
                    drawList.AddText(ImGui.GetCursorScreenPos() + ShadowOffset, ShadowColor, text);
                }
                ImGui.TextUnformatted(text);

                if (_plugin.Config.EmoteLog.ShowReplyButton)
                {
                    ImGui.SameLine();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + rightAlignPos - ImGui.GetCursorPosX());

                    using (var font = ImRaii.PushFont(UiBuilder.IconFont))
                    using (var style = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero))
                    {
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
                    }

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
