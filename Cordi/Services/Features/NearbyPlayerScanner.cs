using System;
using System.Collections.Generic;
using System.Linq;
using Cordi.Core;
using Cordi.Domain;
using Cordi.Domain.Observations;
using Cordi.Domain.Tracking;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Cordi.Services.Features;

public class NearbyPlayerScanner : IDisposable
{
    public const double ScanIntervalSeconds = 2.0;

    /// <summary>
    /// How long a player may be out of view before their encounter is finalized. Brief
    /// disappearances shorter than this — loading screens, duty cutscenes, render-range
    /// flicker, or zone transitions within the same instanced content — are tolerated so a
    /// continuous encounter stays a single record instead of fragmenting into many.
    /// </summary>
    private static readonly TimeSpan EncounterGraceWindow = TimeSpan.FromSeconds(60);

    private readonly CordiPlugin _plugin;
    private readonly Dictionary<string, OpenEncounter> _openEncounters = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>An encounter session that is still in progress, held in memory until the player leaves view.</summary>
    private sealed class OpenEncounter
    {
        public string Name = string.Empty;
        public string World = string.Empty;
        public DateTime StartedAt;
        public DateTime LastSeenAt;
        public uint? TerritoryId;
        public string? TerritoryName;
        public bool InInstance;
        public byte? Level;
        public uint? ClassJobId;
    }

    private CordiLogService Log => _plugin.LogService;
    private const string LogSource = "NearbyScanner";

    public NearbyPlayerScanner(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public void Tick(IFramework framework)
    {
        if (_disposed) return;
        Scan();
    }

    private void Scan()
    {
        var localPlayer = Service.ObjectTable.LocalPlayer;
        var localId = localPlayer?.GameObjectId ?? 0;

        var territoryId = (uint)Service.ClientState.TerritoryType;
        var territoryName = ResolveTerritoryName(territoryId);
        var inInstance = IsInInstance();
        var now = DateTime.UtcNow;

        var nowVisible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var obj in Service.ObjectTable)
        {
            if (obj is not IPlayerCharacter pc) continue;
            if (pc.GameObjectId == localId) continue;

            var name = pc.Name.TextValue;
            var world = pc.HomeWorld.Value.Name.ExtractText();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(world)) continue;

            var key = $"{name}@{world}";
            nowVisible.Add(key);

            if (_openEncounters.TryGetValue(key, out var open))
            {
                if (IsSameEncounterContext(open, territoryId, inInstance))
                {
                    // Same place (or continuous within one instance) — extend the session.
                    open.LastSeenAt = now;
                    continue;
                }

                // Context changed (moved to a different open-world map, or crossed the
                // instance boundary): close the old session and start a fresh one here.
                CloseEncounter(key);
            }

            OpenNewEncounter(pc, name, world, key, territoryId, territoryName, inInstance, now);
        }

        // Finalize only sessions whose player has been out of view longer than the grace
        // window. Anyone gone for a shorter time stays open so that, if they reappear after a
        // load screen / cutscene / brief range loss, the same encounter simply resumes.
        if (_openEncounters.Count > 0)
        {
            var expired = _openEncounters
                .Where(kv => (now - kv.Value.LastSeenAt) > EncounterGraceWindow)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var key in expired)
                CloseEncounter(key);
        }
    }

    /// <summary>
    /// Decides whether a player still in view belongs to their existing open encounter.
    /// Inside instanced content any internal zone transition counts as the same encounter;
    /// in the open world an encounter is tied to a single map, so a different territory is a
    /// new encounter. Crossing the instance boundary (entering/leaving a duty) always splits.
    /// </summary>
    private static bool IsSameEncounterContext(OpenEncounter open, uint territoryId, bool inInstance)
    {
        if (open.InInstance != inInstance) return false;   // entered or left a duty
        if (inInstance) return true;                       // continuous within one instance
        return open.TerritoryId == territoryId;            // open world: same map only
    }

    private void OpenNewEncounter(
        IPlayerCharacter pc, string name, string world, string key,
        uint territoryId, string? territoryName, bool inInstance, DateTime now)
    {
        _openEncounters[key] = new OpenEncounter
        {
            Name = name,
            World = world,
            StartedAt = now,
            LastSeenAt = now,
            TerritoryId = territoryId,
            TerritoryName = territoryName,
            InInstance = inInstance,
            Level = pc.Level,
            ClassJobId = pc.ClassJob.RowId,
        };

        // Fire the observation that creates/updates the tracked player record.
        var player = Player.FromGameObject(pc);
        var ctx = new ObservationContext(
            Source: ObservationSource.Nearby,
            TerritoryId: territoryId,
            TerritoryName: territoryName,
            Position: pc.Position,
            At: now);

        _ = _plugin.PlayerObservations.FireAsync(new PlayerObservation(player, ctx));
    }

    private static bool IsInInstance() =>
        Service.Condition[ConditionFlag.BoundByDuty] ||
        Service.Condition[ConditionFlag.BoundByDuty56] ||
        Service.Condition[ConditionFlag.BoundByDuty95] ||
        Service.Condition[ConditionFlag.InDeepDungeon];

    private void CloseEncounter(string key)
    {
        if (!_openEncounters.TryGetValue(key, out var open)) return;
        _openEncounters.Remove(key);

        var encounter = new Encounter
        {
            StartedAt = open.StartedAt,
            LastSeenAt = open.LastSeenAt,
            EndedAt = open.LastSeenAt,
            TerritoryId = open.TerritoryId,
            TerritoryName = open.TerritoryName,
            Level = open.Level,
            ClassJobId = open.ClassJobId,
            Source = ObservationSource.Nearby,
        };

        try
        {
            _plugin.PlayerTracker.RecordEncounter(open.Name, open.World, encounter);
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Failed to record encounter for {open.Name}@{open.World}: {ex.Message}");
        }
    }

    private void CloseAllEncounters()
    {
        if (_openEncounters.Count == 0) return;
        foreach (var key in _openEncounters.Keys.ToList())
            CloseEncounter(key);
    }

    private static string? ResolveTerritoryName(uint territoryId)
    {
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<TerritoryType>();
            if (sheet == null) return null;
            var row = sheet.GetRow(territoryId);
            var placeName = row.PlaceName.Value.Name.ExtractText();
            return string.IsNullOrEmpty(placeName) ? null : placeName;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseAllEncounters();
    }
}
