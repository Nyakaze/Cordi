using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using DSharpPlus;
using DSharpPlus.EventArgs;

namespace Cordi.Services.Discord.Dispatch;

public class DiscordEventDispatcher : IDisposable
{
    private readonly CordiPlugin _plugin;
    private readonly IReadOnlyList<IDiscordReactionHandler> _reactionHandlers;
    private readonly IReadOnlyList<IDiscordMessageHandler> _messageHandlers;
    private readonly IReadOnlyList<IDiscordPresenceHandler> _presenceHandlers;
    private bool _bound;
    private bool _disposed;

    private CordiLogService Log => _plugin.LogService;
    private const string LogSource = "DiscordDispatcher";

    public DiscordEventDispatcher(CordiPlugin plugin)
    {
        _plugin = plugin;
        _reactionHandlers = DiscoverHandlers<IDiscordReactionHandler>(plugin);
        _messageHandlers = DiscoverHandlers<IDiscordMessageHandler>(plugin);
        _presenceHandlers = DiscoverHandlers<IDiscordPresenceHandler>(plugin);
        Log.Info(LogSource, $"Registered {_reactionHandlers.Count} reaction handler(s), {_messageHandlers.Count} message handler(s), {_presenceHandlers.Count} presence handler(s)");
    }

    public void Bind()
    {
        if (_bound) return;
        _plugin.Discord.OnReactionAdded += OnReaction;
        _plugin.Discord.OnMessageCreated += OnMessage;
        _plugin.Discord.OnPresenceUpdated += OnPresence;
        _bound = true;
    }

    public IReadOnlyList<string> ReactionHandlerNames =>
        _reactionHandlers.Select(h => h.GetType().Name).ToList();

    public IReadOnlyList<string> MessageHandlerNames =>
        _messageHandlers.Select(h => h.GetType().Name).ToList();

    public IReadOnlyList<string> PresenceHandlerNames =>
        _presenceHandlers.Select(h => h.GetType().Name).ToList();

    private async Task OnReaction(MessageReactionAddEventArgs e)
    {
        foreach (var h in _reactionHandlers)
        {
            try
            {
                await h.HandleAsync(e, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, $"[DiscordDispatcher] {h.GetType().Name} threw");
                Log.Error(LogSource, $"{h.GetType().Name} threw: {ex.Message}");
            }
        }
    }

    private async Task OnMessage(MessageCreateEventArgs e)
    {
        foreach (var h in _messageHandlers)
        {
            try
            {
                await h.HandleAsync(e, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, $"[DiscordDispatcher] {h.GetType().Name} threw");
                Log.Error(LogSource, $"{h.GetType().Name} threw: {ex.Message}");
            }
        }
    }

    private async Task OnPresence(DiscordClient sender, PresenceUpdateEventArgs e)
    {
        foreach (var h in _presenceHandlers)
        {
            try
            {
                await h.HandleAsync(sender, e, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, $"[DiscordDispatcher] {h.GetType().Name} threw");
                Log.Error(LogSource, $"{h.GetType().Name} threw: {ex.Message}");
            }
        }
    }

    private static IReadOnlyList<T> DiscoverHandlers<T>(CordiPlugin plugin) where T : class
    {
        return Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => !t.IsAbstract
                     && !t.IsInterface
                     && typeof(T).IsAssignableFrom(t)
                     && t.GetCustomAttribute<DiscordHandlerAttribute>() != null)
            .Select(t => (T)Activator.CreateInstance(t, plugin)!)
            .ToList();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_bound)
        {
            _plugin.Discord.OnReactionAdded -= OnReaction;
            _plugin.Discord.OnMessageCreated -= OnMessage;
            _plugin.Discord.OnPresenceUpdated -= OnPresence;
        }
    }
}
