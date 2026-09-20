using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Dispatch;
using Crovus.Events;

namespace Cordi.Services.Discord.Handlers.Messages;

[DiscordHandler]
public class StandardChatRelayHandler : IDiscordMessageHandler
{
    private readonly DiscordHandler _discord;

    public StandardChatRelayHandler(DiscordHandler discord)
    {
        _discord = discord;
    }

    public async Task HandleAsync(MessageCreatedEvent e, CancellationToken ct)
    {
        await _discord.MessageRouter.RouteStandardMessage(e.Message, e.ChannelId);
    }
}
