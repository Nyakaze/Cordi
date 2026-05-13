using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Domain.Observations;

namespace Cordi.Services.Observations.Handlers;

[ObservationHandler]
public class TrackingObservationHandler : IPlayerObservedHandler
{
    private readonly CordiPlugin _plugin;

    public TrackingObservationHandler(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public Task HandleAsync(PlayerObservation obs, CancellationToken ct)
    {
        _plugin.PlayerTracker.Observe(obs.Player, obs.Context);
        return Task.CompletedTask;
    }
}
