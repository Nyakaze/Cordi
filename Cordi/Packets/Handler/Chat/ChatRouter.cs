using System.Threading;
using System.Threading.Tasks;
using Cordi.Packets.Handler.Chat;
using Cordi.Packets.Factory;
using Cordi.Services.Discord;

namespace Cordi.Packets.Handler.Chat;

public sealed class ChatRouter
{
    private readonly IChatHandlerFactory _factory;
    public ChatRouter(IChatHandlerFactory factory) => _factory = factory;

    public Task RouteAsync(ChatMessage msg, DiscordHandler discord, CancellationToken ct = default)
    {
        return _factory.Get(msg.ChatType).HandleAsync(msg, discord,ct);
    }
}
