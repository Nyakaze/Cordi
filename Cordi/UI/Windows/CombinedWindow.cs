using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

using Cordi.Configuration;
using Cordi.Core;
using Cordi.UI.Panels;

namespace Cordi.UI.Windows;

public class CombinedWindow : ThemedWindow
{
    private readonly CordiPlugin _plugin;
    private readonly EmoteLogPanel _emoteLogPanel;
    private readonly CordiPeepPanel _peepPanel;

    public CombinedWindow(CordiPlugin plugin) : base(
        "Emote Log & Peeper###CordiCombo",
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _plugin = plugin;
        _emoteLogPanel = new EmoteLogPanel(plugin);
        _peepPanel = new CordiPeepPanel(plugin);

        this.SizeConstraints = new WindowSizeConstraints
        {
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    protected override IWindowChromeConfig Chrome => _plugin.Config.CombinedWindow;

    public override void Draw()
    {
        _theme.ApplyFontScale();
        var cfg = _plugin.Config.CombinedWindow;
        var swap = cfg.SwapPanels;

        var tableBorder = _theme.WindowBorder;
        tableBorder.W *= cfg.BackgroundOpacity;
        using var tableBorderScope = ImRaii.PushColor(ImGuiCol.TableBorderStrong, tableBorder)
            .Push(ImGuiCol.TableBorderLight, tableBorder);

        using (var table = ImRaii.Table("##CombinedTable", 2,
                ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame,
                ImGui.GetContentRegionAvail()))
        {
            if (table)
            {
                ImGui.TableSetupColumn("LeftCol", ImGuiTableColumnFlags.None);
                ImGui.TableSetupColumn("RightCol", ImGuiTableColumnFlags.None);

                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                var shadow = cfg.TextShadow;
                using (ImRaii.Child("##LeftPanel", new Vector2(0, 0), false))
                {
                    if (swap)
                        _peepPanel.Draw(shadow);
                    else
                        _emoteLogPanel.Draw(shadow);
                }

                ImGui.TableSetColumnIndex(1);
                using (ImRaii.Child("##RightPanel", new Vector2(0, 0), false))
                {
                    if (swap)
                        _emoteLogPanel.Draw(shadow);
                    else
                        _peepPanel.Draw(shadow);
                }
            }
        }
    }
}
