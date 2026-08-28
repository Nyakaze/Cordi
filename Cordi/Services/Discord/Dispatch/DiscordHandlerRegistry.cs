using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cordi.Core;

namespace Cordi.Services.Discord.Dispatch;

public sealed class DiscordHandlerRegistry
{
    private readonly IReadOnlyList<IDiscordMessageHandler> _messageHandlers;
    private readonly IReadOnlyList<IDiscordReactionHandler> _reactionHandlers;

    public DiscordHandlerRegistry(CordiPlugin plugin)
    {
        _messageHandlers = Discover<IDiscordMessageHandler>(plugin);
        _reactionHandlers = Discover<IDiscordReactionHandler>(plugin);
    }

    public IReadOnlyList<IDiscordMessageHandler> MessageHandlers => _messageHandlers;

    public IReadOnlyList<IDiscordReactionHandler> ReactionHandlers => _reactionHandlers;

    public IReadOnlyList<string> MessageHandlerNames => Names(_messageHandlers);

    public IReadOnlyList<string> ReactionHandlerNames => Names(_reactionHandlers);

    private static IReadOnlyList<string> Names<T>(IReadOnlyList<T> handlers) where T : class =>
        handlers.Select(handler => handler.GetType().Name).ToList();

    private static IReadOnlyList<T> Discover<T>(CordiPlugin plugin) where T : class =>
        Assembly.GetExecutingAssembly().GetTypes()
            .Where(type => !type.IsAbstract
                           && !type.IsInterface
                           && typeof(T).IsAssignableFrom(type)
                           && type.GetCustomAttribute<DiscordHandlerAttribute>() != null)
            .Select(type => (T)Activator.CreateInstance(type, plugin)!)
            .ToList();
}
