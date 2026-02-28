using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

using Cordi.Core;
using Cordi.UI.Panels;

namespace Cordi.UI.Windows;

public class CombinedWindow : Window
{
    private readonly CordiPlugin _plugin;
    private readonly EmoteLogPanel _emoteLogPanel;
    private readonly CordiPeepPanel _peepPanel;

    public CombinedWindow(CordiPlugin plugin) : base("Emote Log & Peeper###CordiCombo", ImGuiWindowFlags.None)
    {
        _plugin = plugin;
        _emoteLogPanel = new EmoteLogPanel(plugin);
        _peepPanel = new CordiPeepPanel(plugin);

        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void PreDraw()
    {
        base.PreDraw();
        var cfg = _plugin.Config.CombinedWindow;
        RespectCloseHotkey = !cfg.IgnoreEsc;
        Flags = ImGuiWindowFlags.None;
        if (cfg.WindowLocked) Flags |= ImGuiWindowFlags.NoMove;
        if (cfg.WindowNoResize) Flags |= ImGuiWindowFlags.NoResize;
    }

    public override void Draw()
    {
        var swap = _plugin.Config.CombinedWindow.SwapPanels;

        if (ImGui.BeginTable("##CombinedTable", 2,
                ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame,
                ImGui.GetContentRegionAvail()))
        {
            ImGui.TableSetupColumn("LeftCol", ImGuiTableColumnFlags.None);
            ImGui.TableSetupColumn("RightCol", ImGuiTableColumnFlags.None);

            ImGui.TableNextRow(ImGuiTableRowFlags.Headers);

            ImGui.TableSetColumnIndex(0);
            var text0 = swap ? "Peeper" : "Emote Log";
            var posX0 = (ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text0).X) / 2;
            if (posX0 > 0) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + posX0);
            ImGui.TextUnformatted(text0);

            ImGui.TableSetColumnIndex(1);
            var text1 = swap ? "Emote Log" : "Peeper";
            var posX1 = (ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text1).X) / 2;
            if (posX1 > 0) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + posX1);
            ImGui.TextUnformatted(text1);

            ImGui.TableNextRow();

            // Left column
            ImGui.TableSetColumnIndex(0);
            ImGui.BeginChild("##LeftPanel", new Vector2(0, -1), false);
            if (swap)
                _peepPanel.Draw();
            else
                _emoteLogPanel.Draw();
            ImGui.EndChild();

            // Right column
            ImGui.TableSetColumnIndex(1);
            ImGui.BeginChild("##RightPanel", new Vector2(0, -1), false);
            if (swap)
                _emoteLogPanel.Draw();
            else
                _peepPanel.Draw();
            ImGui.EndChild();

            ImGui.EndTable();
        }
    }
}
