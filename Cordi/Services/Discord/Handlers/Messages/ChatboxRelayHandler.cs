using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Dispatch;
using DSharpPlus.EventArgs;

namespace Cordi.Services.Discord.Handlers.Messages;

[DiscordHandler]
public class ChatboxRelayHandler : IDiscordMessageHandler
{
    private readonly CordiPlugin _plugin;

    public ChatboxRelayHandler(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public Task HandleAsync(MessageCreateEventArgs e, CancellationToken ct)
    {
        if (_plugin.Config.Chatbox.Enabled)
            _plugin.Chatbox?.IngestDiscordMessage(e.Message, e.Channel.Id);

        return Task.CompletedTask;
    }
}
