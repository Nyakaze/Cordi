using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Dispatch;
using Crovus.Events;

namespace Cordi.Services.Discord.Handlers.Reactions;

[DiscordHandler]
public class EmoteReactionHandler : IDiscordReactionHandler
{
    private readonly EmoteLogService _emoteLog;

    public EmoteReactionHandler(EmoteLogService emoteLog)
    {
        _emoteLog = emoteLog;
    }

    public Task HandleAsync(ReactionAddedEvent e, CancellationToken ct)
        => _emoteLog.DiscordNotifier.OnDiscordReactionAdded(e);
}
