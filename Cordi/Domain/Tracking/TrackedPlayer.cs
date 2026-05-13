using System;
using System.Collections.Generic;

namespace Cordi.Domain.Tracking;

public class TrackedPlayer
{
    public Guid LocalId { get; set; } = Guid.NewGuid();

    public ulong? ContentId { get; set; }
    public string? LodestoneId { get; set; }
    public string NameWorldKey { get; set; } = string.Empty;
    public bool IsProvisional { get; set; } = true;
    public DateTime? LastLodestoneLookupAt { get; set; }

    public PlayerInfo Info { get; set; } = new();
    public PlayerStats Stats { get; set; } = new();
    public List<IdentityChange> History { get; set; } = new();

    public string Notes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}
