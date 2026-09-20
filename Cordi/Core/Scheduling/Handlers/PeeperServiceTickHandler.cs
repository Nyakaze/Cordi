using Cordi.Services;
using Dalamud.Plugin.Services;

using Cordi.Services.Features;

namespace Cordi.Core.Scheduling.Handlers;

[FrameworkTick(IntervalSeconds = PeeperService.TickIntervalSeconds, RequiresLogin = true)]
public class PeeperServiceTickHandler : IFrameworkTickHandler
{
    private readonly PeeperService _peeper;

    public PeeperServiceTickHandler(PeeperService peeper)
    {
        _peeper = peeper;
    }

    public void Tick(IFramework framework) => _peeper.OnFrameworkUpdate(framework);
}
