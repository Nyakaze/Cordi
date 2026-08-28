using System.Threading;
using System.Threading.Tasks;
using Crovus.Events;

namespace Cordi.Services.Discord.Dispatch;

public interface IDiscordReactionHandler
{
    Task HandleAsync(ReactionAddedEvent e, CancellationToken ct);
}
