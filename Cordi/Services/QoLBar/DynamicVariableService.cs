using System;
using System.Collections.Generic;
using System.Linq;
using Cordi.Configuration.QoLBar;
using Cordi.Core;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace Cordi.Services.QoLBar;

public class DynamicVariableService : IDisposable
{
    private readonly VariableService variableService;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IDataManager dataManager;
    private readonly IFramework framework;
    private readonly QoLBarConfig config;

    // Cache last written values to avoid spamming the dictionary if unchanged
    private readonly Dictionary<string, string> lastValues = new();

    public DynamicVariableService(
        VariableService variableService,
        IClientState clientState,
        ICondition condition,
        IDataManager dataManager,
        IFramework framework,
        QoLBarConfig config)
    {
        this.variableService = variableService;
        this.clientState = clientState;
        this.condition = condition;
        this.dataManager = dataManager;
        this.framework = framework;
        this.config = config;

        framework.Update += OnUpdate;
    }

    private void OnUpdate(IFramework framework)
    {
        if (config.DynamicVariables.Count == 0) return;

        var player = clientState.LocalPlayer; // Can be null

        foreach (var entry in config.DynamicVariables)
        {
            if (!entry.Enabled || string.IsNullOrWhiteSpace(entry.VariableName)) continue;

            string newValue = GetValue(entry.Source, player);

            // Only update if changed
            if (!lastValues.TryGetValue(entry.VariableName, out var oldVal) || oldVal != newValue)
            {
                variableService.SetVariableInternal(entry.VariableName, newValue);
                lastValues[entry.VariableName] = newValue;
            }
        }
    }

    private string GetValue(DynamicVarSource source, Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter? player)
    {
        if (player == null) return "";

        return source switch
        {
            DynamicVarSource.JobAbbr => player.ClassJob.Value.Abbreviation.ToString(),
            DynamicVarSource.JobName => player.ClassJob.Value.Name.ToString(),
            DynamicVarSource.JobRole => GetJobRole(player.ClassJob.Value.Role),
            DynamicVarSource.Level => player.Level.ToString(),

            DynamicVarSource.HpPct => player.MaxHp > 0
                ? $"{(int)((float)player.CurrentHp / player.MaxHp * 100)}"
                : "0",

            DynamicVarSource.MpPct => player.MaxMp > 0
                ? $"{(int)((float)player.CurrentMp / player.MaxMp * 100)}"
                : "0",

            DynamicVarSource.ZoneId => clientState.TerritoryType.ToString(),
            DynamicVarSource.ZoneName => GetZoneName(clientState.TerritoryType),

            DynamicVarSource.OnlineStatus => player.OnlineStatus.Value.Name.ToString(),

            // Booleans return "true" or "false"
            DynamicVarSource.InCombat => condition[ConditionFlag.InCombat].ToString().ToLower(),
            DynamicVarSource.InDuty => condition[ConditionFlag.BoundByDuty].ToString().ToLower(),
            DynamicVarSource.Mounted => condition[ConditionFlag.Mounted].ToString().ToLower(),
            DynamicVarSource.Flying => condition[ConditionFlag.InFlight].ToString().ToLower(),
            DynamicVarSource.Swimming => (condition[ConditionFlag.Swimming] || condition[ConditionFlag.Diving]).ToString().ToLower(),
            DynamicVarSource.Crafting => condition[ConditionFlag.Crafting].ToString().ToLower(),
            DynamicVarSource.Gathering => condition[ConditionFlag.Gathering].ToString().ToLower(),
            DynamicVarSource.WeaponDrawn => (!condition[ConditionFlag.NormalConditions]).ToString().ToLower(),
            DynamicVarSource.Performing => condition[ConditionFlag.Performing].ToString().ToLower(),

            _ => ""
        };
    }

    private string GetJobRole(byte role)
    {
        // ClassJobRole enum: 0=None, 1=Tank, 2=Attacker, 3=Healer, 4=Crafter, 5=Gatherer
        return role switch
        {
            1 => "Tank",
            2 => "DPS",
            3 => "Healer",
            4 => "Crafter",
            5 => "Gatherer",
            _ => "None"
        };
    }

    private string GetZoneName(uint territoryId)
    {
        var territory = dataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()?.GetRow(territoryId);
        return territory.HasValue ? territory.Value.PlaceName.Value.Name.ToString() : "";
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
    }
}
