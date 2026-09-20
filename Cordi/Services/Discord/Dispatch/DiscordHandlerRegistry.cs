using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cordi.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Cordi.Services.Discord.Dispatch;

public sealed class DiscordHandlerRegistry
{
    private readonly IReadOnlyList<IDiscordMessageHandler> _messageHandlers;
    private readonly IReadOnlyList<IDiscordReactionHandler> _reactionHandlers;

    public DiscordHandlerRegistry(IServiceProvider services)
    {
        _messageHandlers = Discover<IDiscordMessageHandler>(services);
        _reactionHandlers = Discover<IDiscordReactionHandler>(services);
    }

    public IReadOnlyList<IDiscordMessageHandler> MessageHandlers => _messageHandlers;

    public IReadOnlyList<IDiscordReactionHandler> ReactionHandlers => _reactionHandlers;

    public IReadOnlyList<string> MessageHandlerNames => Names(_messageHandlers);

    public IReadOnlyList<string> ReactionHandlerNames => Names(_reactionHandlers);

    private static IReadOnlyList<string> Names<T>(IReadOnlyList<T> handlers) where T : class =>
        handlers.Select(handler => handler.GetType().Name).ToList();

    private static IReadOnlyList<T> Discover<T>(IServiceProvider services) where T : class =>
        Assembly.GetExecutingAssembly().GetTypes()
            .Where(type => !type.IsAbstract
                           && !type.IsInterface
                           && typeof(T).IsAssignableFrom(type)
                           && type.GetCustomAttribute<DiscordHandlerAttribute>() != null)
            .Select(type => (T)ActivatorUtilities.CreateInstance(services, type))
            .ToList();
}
