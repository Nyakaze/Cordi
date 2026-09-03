using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Core.Caching;
using Cordi.Services.Discord.Connection;
using Crovus.Events;

namespace Cordi.Services.Discord.Dispatch;

public sealed class DiscordEventPump : IDisposable
{
    private const string LogSource = "DiscordDispatcher";

    private readonly CordiPlugin _plugin;
    private readonly DiscordConnection _connection;
    private readonly DiscordHandlerRegistry _registry;
    private readonly Cache<ulong, DateTime> _processedMessages;

    private bool _bound;
    private bool _disposed;

    public DiscordEventPump(CordiPlugin plugin, DiscordConnection connection)
    {
        _plugin = plugin;
        _connection = connection;
        _registry = new DiscordHandlerRegistry(plugin);
        _processedMessages = new Cache<ulong, DateTime>(
            "discord.dispatch.processed", plugin.CacheRegistry,
            maxSize: 1000, ttl: TimeSpan.FromMinutes(10));

        Log.Info(LogSource,
            $"Registered {_registry.ReactionHandlerNames.Count} reaction handler(s), " +
            $"{_registry.MessageHandlerNames.Count} message handler(s)");
    }

    public DiscordHandlerRegistry Registry => _registry;

    public IReadOnlyList<string> MessageHandlerNames => _registry.MessageHandlerNames;

    public IReadOnlyList<string> ReactionHandlerNames => _registry.ReactionHandlerNames;

    public void Bind()
    {
        if (_bound) return;
        _bound = true;

        _connection.Register<MessageCreatedEvent>(OnMessageCreatedAsync);
        _connection.Register<ReactionAddedEvent>(OnReactionAddedAsync);
    }

    private async Task OnMessageCreatedAsync(MessageCreatedEvent e, CancellationToken ct)
    {
        if (e.Author.IsBot == true || e.Message.IsWebhook || _connection.Session.IsSelf(e.Author))
            return;

        if (!_processedMessages.TryAdd(e.MessageId, DateTime.UtcNow))
        {
            Log.Debug(LogSource, $"Ignored duplicate message {e.MessageId}");
            return;
        }

        Log.Debug(LogSource, $"Received from {e.Author.DisplayName} in #{e.Channel.Name}: {e.Content}");

        _plugin.Config.Stats.IncrementTotal();

        await FanOutAsync(_registry.MessageHandlers, handler => handler.HandleAsync(e, ct));
    }

    private async Task OnReactionAddedAsync(ReactionAddedEvent e, CancellationToken ct)
    {
        if (e.User.IsBot == true || _connection.Session.IsSelf(e.User))
            return;

        await FanOutAsync(_registry.ReactionHandlers, handler => handler.HandleAsync(e, ct));
    }

    private async Task FanOutAsync<T>(IReadOnlyList<T> handlers, Func<T, Task> invoke) where T : class
    {
        foreach (var handler in handlers)
        {
            try
            {
                await invoke(handler);
            }
            catch (Exception ex)
            {
                Log.Error(LogSource, $"{handler.GetType().Name} threw: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    private CordiLogService Log => _plugin.LogService;
}
