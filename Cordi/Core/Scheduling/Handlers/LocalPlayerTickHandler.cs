using Dalamud.Plugin.Services;

namespace Cordi.Core.Scheduling.Handlers;

[FrameworkTick]
public class LocalPlayerTickHandler : IFrameworkTickHandler
{
    private readonly CordiPlugin _plugin;

    public LocalPlayerTickHandler(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public void Tick(IFramework framework) => _plugin.LocalPlayer.Tick(framework);
}
