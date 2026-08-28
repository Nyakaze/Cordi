using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Dispatch;
using Crovus.Events;

namespace Cordi.Services.Discord.Handlers.Reactions;

[DiscordHandler]
public class EmoteReactionHandler : IDiscordReactionHandler
{
    private readonly CordiPlugin _plugin;

    public EmoteReactionHandler(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public Task HandleAsync(ReactionAddedEvent e, CancellationToken ct)
        => _plugin.EmoteLog.DiscordNotifier.OnDiscordReactionAdded(e);
}
