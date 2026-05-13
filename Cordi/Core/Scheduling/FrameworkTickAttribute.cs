using System;

namespace Cordi.Core.Scheduling;

[AttributeUsage(AttributeTargets.Class)]
public class FrameworkTickAttribute : Attribute
{
    public double IntervalSeconds { get; init; } = 0;
    public bool RequiresLogin { get; init; } = false;
}
