using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cordi.Packets.Attributes;
using Cordi.Packets.Handler;
using Cordi.Packets.Handler.Chat;
using Cordi.Packets.Handler.Chat.ChatTypes;
using Dalamud.Game.Text;
using Microsoft.Extensions.DependencyInjection;

namespace Cordi.Packets.Factory;

public class AttributeChatHandlerFactory : IChatHandlerFactory
{
    private readonly IServiceProvider _sp;
    private readonly IReadOnlyDictionary<XivChatType, Type> _map;
    private readonly IChatHandler _defaultHandler;
    
    public AttributeChatHandlerFactory(IServiceProvider sp)
    {
        _defaultHandler = new DefaultHandler();
        _sp = sp;
        _map = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(IChatHandler).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(t => new
            {
                Type = t,
                Attr = t.GetCustomAttributes(typeof(ChatTypeAttribute), false)
                    .Cast<ChatTypeAttribute>()
                    .FirstOrDefault()
            })
            .Where(x => x.Attr != null)
            .ToDictionary(x => x.Attr.ChatType, x => x.Type);
    }
    
    public IChatHandler Get(XivChatType type)
    {
        if (!_map.TryGetValue(type, out var impl))
            return _defaultHandler;

        return (IChatHandler)ActivatorUtilities.CreateInstance(_sp, impl);
    }
}
