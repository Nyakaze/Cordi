using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Dispatch;
using DSharpPlus.EventArgs;

namespace Cordi.Services.Discord.Handlers.Messages;

[DiscordHandler]
public class StandardChatRelayHandler : IDiscordMessageHandler
{
    private readonly CordiPlugin _plugin;

    public StandardChatRelayHandler(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public async Task HandleAsync(MessageCreateEventArgs e, CancellationToken ct)
    {
        await _plugin.Discord.MessageRouter.RouteStandardMessage(e.Message, e.Channel.Id);
    }
}
