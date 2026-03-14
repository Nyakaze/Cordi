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
    private ImRaii.Color? _opacityScope;
    private ImRaii.Style? _borderScope;

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
        if (cfg.HideTitleBar) Flags |= ImGuiWindowFlags.NoTitleBar;

        _theme.PushWindow();

        if (cfg.BackgroundOpacity < 1.0f)
        {
            var bg = _theme.WindowBg;
            bg.W *= cfg.BackgroundOpacity;
            _opacityScope = ImRaii.PushColor(ImGuiCol.WindowBg, bg);
        }

        if (cfg.HideTitleBar)
        {
            _borderScope = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 0f);
        }
    }

    public override void PostDraw()
    {
        _borderScope?.Dispose();
        _borderScope = null;
        _opacityScope?.Dispose();
        _opacityScope = null;
        _theme.PopWindow();
        base.PostDraw();
    }

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

                // Left column
                ImGui.TableSetColumnIndex(0);
                var shadow = cfg.TextShadow;
                using (ImRaii.Child("##LeftPanel", new Vector2(0, 0), false))
                {
                    if (swap)
                        _peepPanel.Draw(shadow);
                    else
                        _emoteLogPanel.Draw(shadow);
                }

                // Right column
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
