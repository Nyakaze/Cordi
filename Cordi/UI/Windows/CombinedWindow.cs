using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

using Cordi.Core;
using Cordi.UI.Panels;
using Cordi.UI.Themes;

namespace Cordi.UI.Windows;

public class CombinedWindow : Window
{
    private readonly CordiPlugin _plugin;
    private readonly EmoteLogPanel _emoteLogPanel;
    private readonly CordiPeepPanel _peepPanel;
    private readonly UiTheme _theme = new UiTheme();

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
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        if (cfg.WindowLocked) Flags |= ImGuiWindowFlags.NoMove;
        if (cfg.WindowNoResize) Flags |= ImGuiWindowFlags.NoResize;

        _theme.PushWindow();
    }

    public override void PostDraw()
    {
        _theme.PopWindow();
        base.PostDraw();
    }

    public override void Draw()
    {
        _theme.ApplyFontScale();
        var swap = _plugin.Config.CombinedWindow.SwapPanels;

        using (var table = ImRaii.Table("##CombinedTable", 2,
                ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame,
                ImGui.GetContentRegionAvail()))
        {
            if (table)
            {
                ImGui.TableSetupColumn("LeftCol", ImGuiTableColumnFlags.None);
                ImGui.TableSetupColumn("RightCol", ImGuiTableColumnFlags.None);

                using (ImRaii.PushColor(ImGuiCol.TableHeaderBg, _theme.WindowBg))
                {
                    ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
                }

                ImGui.TableSetColumnIndex(0);
                var text0 = swap ? "Peeper" : "Emote Log";
                var posX0 = (ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text0).X) / 2;
                if (posX0 > 0) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + posX0);
                using (ImRaii.PushColor(ImGuiCol.Text, _theme.MutedText))
                {
                    ImGui.TextUnformatted(text0);
                }

                ImGui.TableSetColumnIndex(1);
                var text1 = swap ? "Emote Log" : "Peeper";
                var posX1 = (ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text1).X) / 2;
                if (posX1 > 0) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + posX1);
                using (ImRaii.PushColor(ImGuiCol.Text, _theme.MutedText))
                {
                    ImGui.TextUnformatted(text1);
                }

                ImGui.TableNextRow();

                // Left column
                ImGui.TableSetColumnIndex(0);
                using (ImRaii.Child("##LeftPanel", new Vector2(0, 0), false))
                {
                    if (swap)
                        _peepPanel.Draw();
                    else
                        _emoteLogPanel.Draw();
                }

                // Right column
                ImGui.TableSetColumnIndex(1);
                using (ImRaii.Child("##RightPanel", new Vector2(0, 0), false))
                {
                    if (swap)
                        _emoteLogPanel.Draw();
                    else
                        _peepPanel.Draw();
                }
            }
        }
    }
}
