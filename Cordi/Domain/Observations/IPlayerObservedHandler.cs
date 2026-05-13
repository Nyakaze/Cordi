using System.Threading;
using System.Threading.Tasks;

namespace Cordi.Domain.Observations;

public interface IPlayerObservedHandler
{
    Task HandleAsync(PlayerObservation obs, CancellationToken ct);
}
