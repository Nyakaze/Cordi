using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Dispatch;
using Crovus.Events;

namespace Cordi.Services.Discord.Handlers.Messages;

[DiscordHandler]
public class ExtraChatRelayHandler : IDiscordMessageHandler
{
    private readonly DiscordHandler _discord;

    public ExtraChatRelayHandler(DiscordHandler discord)
    {
        _discord = discord;
    }

    public async Task HandleAsync(MessageCreatedEvent e, CancellationToken ct)
    {
        await _discord.MessageRouter.RouteExtraChatMessage(e.Message, e.ChannelId);
    }
}
