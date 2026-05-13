using Dalamud.Plugin.Services;

namespace Cordi.Core.Scheduling;

public interface IFrameworkTickHandler
{
    void Tick(IFramework framework);
}
