using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Domain.Observations;

namespace Cordi.Services.Observations;

public class PlayerObservationDispatcher
{
    private readonly CordiPlugin _plugin;
    private readonly IReadOnlyList<IPlayerObservedHandler> _handlers;

    private CordiLogService Log => _plugin.LogService;
    private const string LogSource = "ObservationDispatcher";

    public PlayerObservationDispatcher(CordiPlugin plugin)
    {
        _plugin = plugin;
        _handlers = DiscoverHandlers(plugin);
        Log.Info(LogSource, $"Registered {_handlers.Count} observation handler(s)");
    }

    public async Task FireAsync(PlayerObservation obs, CancellationToken ct = default)
    {
        foreach (var handler in _handlers)
        {
            try
            {
                await handler.HandleAsync(obs, ct);
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, $"[ObservationDispatcher] {handler.GetType().Name} threw");
                Log.Error(LogSource, $"{handler.GetType().Name} threw: {ex.Message}");
            }
        }
    }

    public IReadOnlyList<string> HandlerNames =>
        _handlers.Select(h => h.GetType().Name).ToList();

    private static IReadOnlyList<IPlayerObservedHandler> DiscoverHandlers(CordiPlugin plugin)
    {
        return Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => !t.IsAbstract
                     && !t.IsInterface
                     && typeof(IPlayerObservedHandler).IsAssignableFrom(t)
                     && t.GetCustomAttribute<ObservationHandlerAttribute>() != null)
            .Select(t => (IPlayerObservedHandler)Activator.CreateInstance(t, plugin)!)
            .ToList();
    }
}
