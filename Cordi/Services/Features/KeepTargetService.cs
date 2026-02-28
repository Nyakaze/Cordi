using System;
using Dalamud.Plugin.Services;
using Cordi.Core;

namespace Cordi.Services.Features;

public class KeepTargetService : IDisposable
{
    private readonly CordiPlugin _plugin;
    private DateTime _lastCheck = DateTime.MinValue;

    public KeepTargetService(CordiPlugin plugin)
    {
        _plugin = plugin;
        Service.Framework.Update += OnUpdate;
    }

    private void OnUpdate(IFramework framework)
    {
        var config = _plugin.Config.KeepTarget;
        if (!config.Enabled || string.IsNullOrWhiteSpace(config.TargetName))
            return;

        if ((DateTime.Now - _lastCheck).TotalMilliseconds < 500)
            return;

        _lastCheck = DateTime.Now;

        var targetName = config.TargetName.Trim();

        // Check if current target is already the keep target
        var currentTarget = Service.TargetManager.Target;
        if (currentTarget != null && currentTarget.Name.TextValue.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            return;

        // Search for the target in the object table
        foreach (var obj in Service.ObjectTable)
        {
            if (obj != null && obj.Name.TextValue.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            {
                Service.TargetManager.Target = obj;
                break;
            }
        }
    }

    public void Dispose()
    {
        Service.Framework.Update -= OnUpdate;
    }
}
