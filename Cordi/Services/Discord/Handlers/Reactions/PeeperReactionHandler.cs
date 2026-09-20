using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Dispatch;
using Crovus.Events;

namespace Cordi.Services.Discord.Handlers.Reactions;

[DiscordHandler]
public class PeeperReactionHandler : IDiscordReactionHandler
{
    private readonly CordiPeepService _peeper;

    public PeeperReactionHandler(CordiPeepService peeper)
    {
        _peeper = peeper;
    }

    public Task HandleAsync(ReactionAddedEvent e, CancellationToken ct)
        => _peeper.OnDiscordReactionAdded(e);
}
