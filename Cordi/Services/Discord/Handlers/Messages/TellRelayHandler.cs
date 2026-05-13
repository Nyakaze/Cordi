using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Dispatch;
using DSharpPlus.EventArgs;

namespace Cordi.Services.Discord.Handlers.Messages;

[DiscordHandler]
public class TellRelayHandler : IDiscordMessageHandler
{
    private readonly CordiPlugin _plugin;

    public TellRelayHandler(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public async Task HandleAsync(MessageCreateEventArgs e, CancellationToken ct)
    {
        await _plugin.Discord.MessageRouter.RouteTellMessage(e.Message, e.Channel.Id);
    }
}
