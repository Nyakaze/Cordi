using System;
using System.Numerics;

namespace Cordi.Domain.Tracking;

public class PlayerStats
{
    public int SeenCount { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public ObservationSource FirstSeenVia { get; set; }

    public uint? LastTerritoryId { get; set; }
    public string? LastTerritoryName { get; set; }
    public Vector3? LastPosition { get; set; }
}
