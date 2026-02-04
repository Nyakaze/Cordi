using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Cordi.Services;
using Dalamud.Plugin.Services;
using ECommons.ImGuiMethods;
using Dalamud.Interface;

using Cordi.Core;

namespace Cordi.UI.Windows;

public class EmoteLogWindow : Window, IDisposable
{
    private readonly CordiPlugin _plugin;

    public EmoteLogWindow(CordiPlugin plugin) : base("Emote Log##CordiEmoteLog", ImGuiWindowFlags.None)
    {
        _plugin = plugin;


        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(250, 100),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void Draw()
    {
        if (!_plugin.Config.EmoteLog.WindowEnabled)
        {
            IsOpen = false;
            return;
        }

        var logs = _plugin.EmoteLog.Logs;

        lock (logs)
        {

            var buttonSize = new Vector2(ImGui.GetFrameHeight());
            var rightAlignPos = ImGui.GetWindowContentRegionMax().X - buttonSize.X - ImGui.GetStyle().ItemSpacing.X;

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


                    ImGui.SetCursorPosX(rightAlignPos);

                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
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
                            entry.GameObjectId
                        );
                    }
                    ImGui.PopStyleVar();
                    ImGui.PopFont();

                    if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Emote Back ({(!string.IsNullOrEmpty(entry.Command) ? entry.Command : entry.Emote)})");
                }

                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    var target = Service.ObjectTable.FirstOrDefault(x => x.Name.ToString() == entry.User);
                    if (target != null) Service.TargetManager.Target = target;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Left-click to emote back\nRight-click to target");
                }
            }
        }
    }

    public override void PreDraw()
    {

        if (_plugin.Config.EmoteLog.WindowLockPosition)
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }

        if (_plugin.Config.EmoteLog.WindowLockSize)
        {
            Flags |= ImGuiWindowFlags.NoResize;
        }
        else
        {
            Flags &= ~ImGuiWindowFlags.NoResize;
        }
    }

    public void Dispose()
    {
    }
}
