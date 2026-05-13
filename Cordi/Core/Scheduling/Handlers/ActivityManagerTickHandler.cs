using Dalamud.Plugin.Services;

namespace Cordi.Core.Scheduling.Handlers;

[FrameworkTick]
public class ActivityManagerTickHandler : IFrameworkTickHandler
{
    private readonly CordiPlugin _plugin;

    public ActivityManagerTickHandler(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public void Tick(IFramework framework) => _plugin.ActivityManager.OnFrameworkUpdate(framework);
}
