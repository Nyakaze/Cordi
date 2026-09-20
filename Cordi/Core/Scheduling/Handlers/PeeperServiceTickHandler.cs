using Cordi.Services;
using Dalamud.Plugin.Services;

namespace Cordi.Core.Scheduling.Handlers;

[FrameworkTick(IntervalSeconds = CordiPeepService.TickIntervalSeconds, RequiresLogin = true)]
public class PeeperServiceTickHandler : IFrameworkTickHandler
{
    private readonly CordiPeepService _peeper;

    public PeeperServiceTickHandler(CordiPeepService peeper)
    {
        _peeper = peeper;
    }

    public void Tick(IFramework framework) => _peeper.OnFrameworkUpdate(framework);
}
