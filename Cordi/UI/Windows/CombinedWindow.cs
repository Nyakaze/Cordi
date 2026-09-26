using System;
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

        if (!cfg.Enabled)
        {
            IsOpen = false;
            return;
        }

        var emoteSlot = cfg.EmoteLogSlot;
        var peeperSlot = cfg.PeeperSlot;

        if (emoteSlot == peeperSlot)
            peeperSlot = emoteSlot == OverlaySlot.TopLeft ? OverlaySlot.TopRight : OverlaySlot.TopLeft;

        int emoteRow = CombinedWindowConfig.RowOf(emoteSlot);
        int emoteColumn = CombinedWindowConfig.ColumnOf(emoteSlot);
        int peeperRow = CombinedWindowConfig.RowOf(peeperSlot);
        int peeperColumn = CombinedWindowConfig.ColumnOf(peeperSlot);

        bool splitRows = emoteRow != peeperRow;
        int columns = emoteColumn == peeperColumn ? 1 : 2;

        var border = _theme.WindowBorder;
        border.W *= cfg.BackgroundOpacity;

        using var borderScope = ImRaii.PushColor(ImGuiCol.TableBorderStrong, border)
            .Push(ImGuiCol.TableBorderLight, border);

        using (var table = ImRaii.Table("##CombinedTable", columns,
                ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame,
                ImGui.GetContentRegionAvail()))
        {
            if (!table) return;

            for (int column = 0; column < columns; column++)
            {
                ImGui.TableSetupColumn($"Col{column}", ImGuiTableColumnFlags.None);
            }

            ImGui.TableNextRow();

            int cell = 0;

            for (int column = 0; column < 2; column++)
            {
                if (columns == 1 && column != emoteColumn) continue;

                ImGui.TableSetColumnIndex(cell);

                Action<bool>? top = null;
                Action<bool>? bottom = null;

                if (emoteColumn == column) AssignPanel(emoteRow, _emoteLogPanel.Draw, ref top, ref bottom);
                if (peeperColumn == column) AssignPanel(peeperRow, _peepPanel.Draw, ref top, ref bottom);

                DrawColumn(column, top, bottom, splitRows, cfg.TextShadow, border);
                cell++;
            }
        }
    }

    private static void AssignPanel(int row, Action<bool> panel, ref Action<bool>? top, ref Action<bool>? bottom)
    {
        if (row == 0)
            top = panel;
        else
            bottom = panel;
    }

    private void DrawColumn(int column, Action<bool>? top, Action<bool>? bottom, bool splitRows, bool shadow, Vector4 border)
    {
        if (!splitRows)
        {
            using (ImRaii.Child($"##Cell{column}", Vector2.Zero, false))
            {
                (top ?? bottom)?.Invoke(shadow);
            }

            return;
        }

        float spacing = ImGui.GetStyle().ItemSpacing.Y;
        float height = MathF.Max((ImGui.GetContentRegionAvail().Y - spacing) * 0.5f, 1f);

        using (ImRaii.Child($"##Cell{column}Top", new Vector2(0, height), false))
        {
            top?.Invoke(shadow);
        }

        var divider = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;

        ImGui.GetWindowDrawList().AddLine(
            new Vector2(divider.X, divider.Y + spacing * 0.5f),
            new Vector2(divider.X + width, divider.Y + spacing * 0.5f),
            ImGui.GetColorU32(border));

        using (ImRaii.Child($"##Cell{column}Bottom", Vector2.Zero, false))
        {
            bottom?.Invoke(shadow);
        }
    }
}
