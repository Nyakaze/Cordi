using Dalamud.Plugin.Services;

namespace Cordi.Core.Scheduling.Handlers;

[FrameworkTick]
public class PluginCoreTickHandler : IFrameworkTickHandler
{
    private readonly CordiPlugin _plugin;

    public PluginCoreTickHandler(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public void Tick(IFramework framework) => _plugin.OnFrameworkUpdate(framework);
}
