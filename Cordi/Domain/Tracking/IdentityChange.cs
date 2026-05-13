using System;

namespace Cordi.Domain.Tracking;

public class IdentityChange
{
    public DateTime When { get; set; }
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
