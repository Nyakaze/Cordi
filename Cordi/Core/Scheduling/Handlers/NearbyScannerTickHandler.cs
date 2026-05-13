using Cordi.Services.Features;
using Dalamud.Plugin.Services;

namespace Cordi.Core.Scheduling.Handlers;

[FrameworkTick(IntervalSeconds = NearbyPlayerScanner.ScanIntervalSeconds, RequiresLogin = true)]
public class NearbyScannerTickHandler : IFrameworkTickHandler
{
    private readonly CordiPlugin _plugin;

    public NearbyScannerTickHandler(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public void Tick(IFramework framework) => _plugin.NearbyScanner.Tick(framework);
}
