using Cordi.Services.Features;
using Dalamud.Plugin.Services;

namespace Cordi.Core.Scheduling.Handlers;

[FrameworkTick(IntervalSeconds = PartyService.TickIntervalSeconds, RequiresLogin = true)]
public class PartyServiceTickHandler : IFrameworkTickHandler
{
    private readonly PartyService _party;

    public PartyServiceTickHandler(PartyService party)
    {
        _party = party;
    }

    public void Tick(IFramework framework) => _party.OnFrameworkUpdate(framework);
}
