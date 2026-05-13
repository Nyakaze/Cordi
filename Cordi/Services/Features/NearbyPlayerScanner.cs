using System;
using System.Collections.Generic;
using System.Linq;
using Cordi.Core;
using Cordi.Domain;
using Cordi.Domain.Observations;
using Cordi.Domain.Tracking;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Cordi.Services.Features;

public class NearbyPlayerScanner : IDisposable
{
    public const double ScanIntervalSeconds = 2.0;

    private readonly CordiPlugin _plugin;
    private readonly HashSet<string> _currentlyVisible = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

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

            if (_currentlyVisible.Contains(key)) continue;

            var player = Player.FromGameObject(pc);
            var ctx = new ObservationContext(
                Source: ObservationSource.Nearby,
                TerritoryId: territoryId,
                TerritoryName: territoryName,
                Position: pc.Position,
                At: DateTime.UtcNow);

            _ = _plugin.PlayerObservations.FireAsync(new PlayerObservation(player, ctx));
        }

        _currentlyVisible.Clear();
        foreach (var k in nowVisible) _currentlyVisible.Add(k);
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
        _currentlyVisible.Clear();
    }
}
