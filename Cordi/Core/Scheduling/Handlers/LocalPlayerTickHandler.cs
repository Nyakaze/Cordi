using Cordi.Domain;
using Dalamud.Plugin.Services;

namespace Cordi.Core.Scheduling.Handlers;

[FrameworkTick]
public class LocalPlayerTickHandler : IFrameworkTickHandler
{
    private readonly LocalPlayerProvider _localPlayer;

    public LocalPlayerTickHandler(LocalPlayerProvider localPlayer)
    {
        _localPlayer = localPlayer;
    }

    public void Tick(IFramework framework) => _localPlayer.Tick(framework);
}
