using System;
using System.Numerics;

namespace Cordi.Domain.Tracking;

public record ObservationContext(
    ObservationSource Source,
    uint? TerritoryId = null,
    string? TerritoryName = null,
    Vector3? Position = null,
    DateTime? At = null
);
