using Cordi.Services;
using Dalamud.Plugin.Services;

namespace Cordi.Core.Scheduling.Handlers;

[FrameworkTick(IntervalSeconds = CordiPeepService.TickIntervalSeconds, RequiresLogin = true)]
public class PeeperServiceTickHandler : IFrameworkTickHandler
{
    private readonly CordiPlugin _plugin;

    public PeeperServiceTickHandler(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public void Tick(IFramework framework) => _plugin.CordiPeep.OnFrameworkUpdate(framework);
}
