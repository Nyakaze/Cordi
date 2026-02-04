using System.Collections.Generic;
using System.Linq;
using Cordi.Packets.Handler;
using Cordi.Packets.Handler.Chat;
using Cordi.Packets.Handler.Chat.ChatTypes;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

namespace Cordi.Packets.Factory;

public class ChatHandlerFactory : IChatHandlerFactory
{
    private readonly IReadOnlyDictionary<XivChatType, IChatHandler> _map;
    private readonly IChatHandler _defaultHandler;

    public ChatHandlerFactory(IEnumerable<IChatHandler> handlers)
    {
        _defaultHandler = new DefaultHandler();


        _map = handlers.ToDictionary(h => h.ChatType, h => h);
    }

    public IChatHandler Get(XivChatType type)
    {
        if (_map.TryGetValue(type, out var h))
        {
            return h;
        }
        return _defaultHandler;
    }
}
