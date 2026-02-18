using System;
using System.Collections.Generic;
using System.Linq;
using Cordi.Configuration.QoLBar;
using Dalamud.Plugin.Services;

namespace Cordi.Services.QoLBar;

public interface IBarCondition
{
    int ID { get; }
    string Name { get; }
    string Category { get; }
    bool Check(int arg);
    string GetArgLabel(int arg);
}

public class ConditionService : IDisposable
{
    private readonly ICondition gameCondition;
    private readonly IClientState clientState;
    private readonly List<IBarCondition> conditions = new();
    private readonly Dictionary<CndSetCfg, (bool result, float time)> conditionSetCache = new();
    private readonly HashSet<CndSetCfg> lockedSets = new();
    private float runTime;

    public bool NoCacheMode { get; set; } = false;

    public ConditionService(ICondition condition, IClientState clientState)
    {
        this.gameCondition = condition;
        this.clientState = clientState;
        RegisterBuiltinConditions();
    }

    private void RegisterBuiltinConditions()
    {
        conditions.Add(new LoggedInCondition(clientState));
        conditions.Add(new InCombatCondition(gameCondition));
        conditions.Add(new MountedCondition(gameCondition));
        conditions.Add(new FlyingCondition(gameCondition));
        conditions.Add(new SwimmingCondition(gameCondition));
        conditions.Add(new CraftingCondition(gameCondition));
        conditions.Add(new GatheringCondition(gameCondition));
        conditions.Add(new InDutyCondition(gameCondition));
        conditions.Add(new PerformingCondition(gameCondition));
        conditions.Add(new WeaponDrawnCondition(gameCondition));
    }

    public void RegisterCondition(IBarCondition condition)
    {
        if (conditions.All(c => c.ID != condition.ID))
            conditions.Add(condition);
    }

    public IBarCondition? GetCondition(int id)
    {
        return conditions.FirstOrDefault(c => c.ID == id);
    }

    public IReadOnlyList<IBarCondition> GetAllConditions() => conditions;

    public void Update(float deltaTime)
    {
        runTime += deltaTime;
    }

    public bool CheckConditionSet(int index, IReadOnlyList<CndSetCfg> sets)
    {
        if (index < 0 || index >= sets.Count) return true;
        return CheckConditionSet(sets[index]);
    }

    public bool CheckConditionSet(CndSetCfg set)
    {
        if (lockedSets.Contains(set))
            return conditionSetCache.TryGetValue(set, out var c) && c.result;

        if (conditionSetCache.TryGetValue(set, out var cache) && runTime <= cache.time + (NoCacheMode ? 0 : 0.1f))
            return cache.result;

        lockedSets.Add(set);

        var first = true;
        var prev = true;
        foreach (var cnd in set.Conditions)
        {
            var condition = GetCondition(cnd.ID);
            if (condition == null) continue;

            if (first)
            {
                prev = CheckUnary(cnd.Negate, condition, cnd.Arg);
                first = false;
            }
            else
            {
                prev = CheckBinary(prev, cnd.Operator, cnd.Negate, condition, cnd.Arg);
            }
        }

        lockedSets.Remove(set);
        conditionSetCache[set] = (prev, runTime);
        return prev;
    }

    private bool CheckUnary(bool negate, IBarCondition condition, int arg)
    {
        var result = condition.Check(arg);
        return negate ? !result : result;
    }

    private bool CheckBinary(bool prev, BinaryOperator op, bool negate, IBarCondition condition, int arg)
    {
        var current = CheckUnary(negate, condition, arg);
        return op switch
        {
            BinaryOperator.AND => prev && current,
            BinaryOperator.OR => prev || current,
            BinaryOperator.EQUALS => prev == current,
            BinaryOperator.XOR => prev ^ current,
            _ => prev && current
        };
    }

    public void ClearCache()
    {
        conditionSetCache.Clear();
    }

    public void Dispose()
    {
        conditions.Clear();
        conditionSetCache.Clear();
    }
}

public class LoggedInCondition : IBarCondition
{
    private readonly IClientState clientState;
    public int ID => 1;
    public string Name => "Logged In";
    public string Category => "General";

    public LoggedInCondition(IClientState clientState) => this.clientState = clientState;
    public bool Check(int arg) => clientState.IsLoggedIn;
    public string GetArgLabel(int arg) => string.Empty;
}

public class InCombatCondition : IBarCondition
{
    private readonly ICondition condition;
    public int ID => 2;
    public string Name => "In Combat";
    public string Category => "General";

    public InCombatCondition(ICondition condition) => this.condition = condition;
    public bool Check(int arg) => condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat];
    public string GetArgLabel(int arg) => string.Empty;
}

public class MountedCondition : IBarCondition
{
    private readonly ICondition condition;
    public int ID => 3;
    public string Name => "Mounted";
    public string Category => "General";

    public MountedCondition(ICondition condition) => this.condition = condition;
    public bool Check(int arg) => condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Mounted];
    public string GetArgLabel(int arg) => string.Empty;
}

public class FlyingCondition : IBarCondition
{
    private readonly ICondition condition;
    public int ID => 4;
    public string Name => "Flying";
    public string Category => "General";

    public FlyingCondition(ICondition condition) => this.condition = condition;
    public bool Check(int arg) => condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InFlight];
    public string GetArgLabel(int arg) => string.Empty;
}

public class SwimmingCondition : IBarCondition
{
    private readonly ICondition condition;
    public int ID => 5;
    public string Name => "Swimming";
    public string Category => "General";

    public SwimmingCondition(ICondition condition) => this.condition = condition;
    public bool Check(int arg) => condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Swimming];
    public string GetArgLabel(int arg) => string.Empty;
}

public class CraftingCondition : IBarCondition
{
    private readonly ICondition condition;
    public int ID => 6;
    public string Name => "Crafting";
    public string Category => "General";

    public CraftingCondition(ICondition condition) => this.condition = condition;
    public bool Check(int arg) => condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Crafting];
    public string GetArgLabel(int arg) => string.Empty;
}

public class GatheringCondition : IBarCondition
{
    private readonly ICondition condition;
    public int ID => 7;
    public string Name => "Gathering";
    public string Category => "General";

    public GatheringCondition(ICondition condition) => this.condition = condition;
    public bool Check(int arg) => condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Gathering];
    public string GetArgLabel(int arg) => string.Empty;
}

public class InDutyCondition : IBarCondition
{
    private readonly ICondition condition;
    public int ID => 8;
    public string Name => "In Duty";
    public string Category => "General";

    public InDutyCondition(ICondition condition) => this.condition = condition;
    public bool Check(int arg) => condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty];
    public string GetArgLabel(int arg) => string.Empty;
}

public class PerformingCondition : IBarCondition
{
    private readonly ICondition condition;
    public int ID => 9;
    public string Name => "Performing";
    public string Category => "General";

    public PerformingCondition(ICondition condition) => this.condition = condition;
    public bool Check(int arg) => condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Performing];
    public string GetArgLabel(int arg) => string.Empty;
}

public class WeaponDrawnCondition : IBarCondition
{
    private readonly ICondition condition;
    public int ID => 10;
    public string Name => "Weapon Drawn";
    public string Category => "General";

    public WeaponDrawnCondition(ICondition condition) => this.condition = condition;
    public bool Check(int arg) => condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.NormalConditions];
    public string GetArgLabel(int arg) => string.Empty;
}
