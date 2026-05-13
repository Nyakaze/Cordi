using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Dispatch;
using DSharpPlus.EventArgs;

namespace Cordi.Services.Discord.Handlers.Messages;

[DiscordHandler]
public class ExtraChatRelayHandler : IDiscordMessageHandler
{
    private readonly CordiPlugin _plugin;

    public ExtraChatRelayHandler(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public async Task HandleAsync(MessageCreateEventArgs e, CancellationToken ct)
    {
        await _plugin.Discord.MessageRouter.RouteExtraChatMessage(e.Message, e.Channel.Id);
    }
}
