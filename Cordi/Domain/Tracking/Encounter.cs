using System;

namespace Cordi.Domain.Tracking;

/// <summary>
/// A single, completed sighting session of a player: a continuous window during
/// which they were visible nearby. Captures when it happened, where, how long it
/// lasted, and what level/class they were at the time.
/// </summary>
public class Encounter
{
    public DateTime StartedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public uint? TerritoryId { get; set; }
    public string? TerritoryName { get; set; }

    public byte? Level { get; set; }
    public uint? ClassJobId { get; set; }

    public ObservationSource Source { get; set; }

    /// <summary>The moment the encounter effectively ended (its close time, or the last confirmed sighting).</summary>
    public DateTime EndOrLastSeen => EndedAt ?? LastSeenAt;

    /// <summary>How long the player remained continuously in view.</summary>
    public TimeSpan Duration => EndOrLastSeen - StartedAt;
}
