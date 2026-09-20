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
    private readonly Configuration.Configuration _config;
    private readonly EmoteLogPanel _emoteLogPanel;
    private readonly PeeperPanel _peepPanel;

    public CombinedWindow(
        Configuration.Configuration config,
        Services.Features.EmoteLogService emoteLog,
        Services.Features.PeeperService peeper) : base(
        "Emote Log & Peeper###CordiCombo",
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _config = config;
        _emoteLogPanel = new EmoteLogPanel(config, emoteLog, _theme);
        _peepPanel = new PeeperPanel(config, peeper, _theme);

        this.SizeConstraints = new WindowSizeConstraints
        {
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    protected override IWindowChromeConfig Chrome => _config.CombinedWindow;

    public override void Draw()
    {
        _theme.ApplyFontScale();
        var cfg = _config.CombinedWindow;
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
