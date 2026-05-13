using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Cordi.Services;
using Dalamud.Plugin.Services;

namespace Cordi.Core.Scheduling;

public class FrameworkScheduler : IDisposable
{
    private readonly CordiPlugin _plugin;
    private readonly List<TickEntry> _entries;
    private bool _bound;
    private bool _disposed;

    private CordiLogService Log => _plugin.LogService;
    private const string LogSource = "FrameworkScheduler";

    private sealed class TickEntry
    {
        public IFrameworkTickHandler Handler { get; init; } = null!;
        public FrameworkTickAttribute Meta { get; init; } = null!;
        public Stopwatch Sw { get; } = Stopwatch.StartNew();
    }

    public FrameworkScheduler(CordiPlugin plugin)
    {
        _plugin = plugin;
        _entries = DiscoverHandlers(plugin);
        Log.Info(LogSource, $"Registered {_entries.Count} tick handler(s)");
    }

    public void Bind()
    {
        if (_bound) return;
        Service.Framework.Update += OnUpdate;
        _bound = true;
    }

    public IReadOnlyList<string> HandlerNames =>
        _entries.Select(e => e.Handler.GetType().Name).ToList();

    private void OnUpdate(IFramework framework)
    {
        if (_disposed) return;

        foreach (var entry in _entries)
        {
            if (entry.Meta.RequiresLogin && !Service.ClientState.IsLoggedIn) continue;
            if (entry.Meta.IntervalSeconds > 0
                && entry.Sw.Elapsed.TotalSeconds < entry.Meta.IntervalSeconds) continue;

            entry.Sw.Restart();
            try
            {
                entry.Handler.Tick(framework);
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, $"[FrameworkScheduler] {entry.Handler.GetType().Name} threw");
                Log.Error(LogSource, $"{entry.Handler.GetType().Name} threw: {ex.Message}");
            }
        }
    }

    private static List<TickEntry> DiscoverHandlers(CordiPlugin plugin)
    {
        return Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => !t.IsAbstract
                     && !t.IsInterface
                     && typeof(IFrameworkTickHandler).IsAssignableFrom(t)
                     && t.GetCustomAttribute<FrameworkTickAttribute>() != null)
            .Select(t => new TickEntry
            {
                Handler = (IFrameworkTickHandler)Activator.CreateInstance(t, plugin)!,
                Meta = t.GetCustomAttribute<FrameworkTickAttribute>()!,
            })
            .ToList();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_bound) Service.Framework.Update -= OnUpdate;
    }
}
