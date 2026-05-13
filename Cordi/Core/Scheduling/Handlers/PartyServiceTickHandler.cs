using Cordi.Services.Features;
using Dalamud.Plugin.Services;

namespace Cordi.Core.Scheduling.Handlers;

[FrameworkTick(IntervalSeconds = PartyService.TickIntervalSeconds, RequiresLogin = true)]
public class PartyServiceTickHandler : IFrameworkTickHandler
{
    private readonly CordiPlugin _plugin;

    public PartyServiceTickHandler(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public void Tick(IFramework framework) => _plugin.PartyService.OnFrameworkUpdate(framework);
}
