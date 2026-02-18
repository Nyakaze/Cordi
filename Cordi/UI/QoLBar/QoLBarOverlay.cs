using System;
using System.Collections.Generic;
using Cordi.Configuration.QoLBar;
using Cordi.Core;
using Cordi.Services.QoLBar;

namespace Cordi.UI.QoLBar;

public class QoLBarOverlay : IDisposable
{
    private readonly ConditionService conditionService;
    private readonly CommandExecutor commandExecutor;
    public List<BarRenderer> Bars { get; } = new();
    public IconPickerPopup IconPicker { get; } = new();

    public QoLBarOverlay(ConditionService conditionService, CommandExecutor commandExecutor)
    {
        this.conditionService = conditionService;
        this.commandExecutor = commandExecutor;
        Reload();
    }

    public void Reload()
    {
        foreach (var bar in Bars)
            bar.Dispose();
        Bars.Clear();

        var config = CordiPlugin.Plugin.QoLBarConfig;
        for (int i = 0; i < config.Bars.Count; i++)
            Bars.Add(new BarRenderer(i, conditionService, commandExecutor));
    }

    public void Draw()
    {
        var config = CordiPlugin.Plugin.QoLBarConfig;
        if (config.AlwaysDisplayBars || Service.ClientState.IsLoggedIn)
        {
            foreach (var bar in Bars)
                bar.Draw();
        }
        IconPicker.Draw();
    }

    public void AddBar(BarCfg barCfg)
    {
        CordiPlugin.Plugin.QoLBarConfig.Bars.Add(barCfg);
        Bars.Add(new BarRenderer(Bars.Count, conditionService, commandExecutor));
        CordiPlugin.Plugin.QoLBarConfig.Save();
    }

    public void RemoveBar(int i)
    {
        if (i < 0 || i >= Bars.Count) return;
        Bars[i].Dispose();
        Bars.RemoveAt(i);
        CordiPlugin.Plugin.QoLBarConfig.Bars.RemoveAt(i);
        CordiPlugin.Plugin.QoLBarConfig.Save();
        RefreshBarIndexes();
    }

    public void ShiftBar(int i, bool increment)
    {
        if (!increment ? i > 0 : i < (Bars.Count - 1))
        {
            var j = increment ? i + 1 : i - 1;
            var bar = Bars[i];
            Bars.RemoveAt(i);
            Bars.Insert(j, bar);

            var cfg = CordiPlugin.Plugin.QoLBarConfig.Bars[i];
            CordiPlugin.Plugin.QoLBarConfig.Bars.RemoveAt(i);
            CordiPlugin.Plugin.QoLBarConfig.Bars.Insert(j, cfg);
            CordiPlugin.Plugin.QoLBarConfig.Save();
            RefreshBarIndexes();
        }
    }

    public void SetBarHidden(int i, bool toggle, bool hidden = false)
    {
        if (i < 0 || i >= Bars.Count) return;
        if (toggle)
            Bars[i].IsHidden = !Bars[i].IsHidden;
        else
            Bars[i].IsHidden = hidden;
    }

    private void RefreshBarIndexes()
    {
        for (int i = 0; i < Bars.Count; i++)
            Bars[i].ID = i;
    }

    public void Dispose()
    {
        foreach (var bar in Bars)
            bar.Dispose();
    }
}
