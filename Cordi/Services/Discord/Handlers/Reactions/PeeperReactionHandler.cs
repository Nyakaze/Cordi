using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Dispatch;
using Crovus.Events;

using Cordi.Services.Features;

namespace Cordi.Services.Discord.Handlers.Reactions;

[DiscordHandler]
public class PeeperReactionHandler : IDiscordReactionHandler
{
    private readonly PeeperService _peeper;

    public PeeperReactionHandler(PeeperService peeper)
    {
        _peeper = peeper;
    }

    public Task HandleAsync(ReactionAddedEvent e, CancellationToken ct)
        => _peeper.OnDiscordReactionAdded(e);
}
