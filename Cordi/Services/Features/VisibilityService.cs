using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Cordi.Core;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Ipc;

namespace Cordi.Services.Features;

public sealed class VisibilityService : IDisposable
{
    private const string LogSource = "Visibility";
    private const string PluginInternalName = "Visibility";
    private const string LeaseReason = "Cordi";
    private const int MinimumApiVersion = 1;

    private static readonly TimeSpan AvailabilityInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ExpiryInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MinimumHold = TimeSpan.FromSeconds(5);

    private readonly record struct Lease(string Name, uint WorldId, uint EntityId, DateTime Expires);

    private readonly Configuration.Configuration _config;
    private readonly CordiLogService _log;
    private readonly ConcurrentDictionary<ulong, Lease> _leases = new();
    private readonly ConcurrentDictionary<ulong, byte> _unhidden = new();

    private ICallGateSubscriber<int>? _apiVersion;
    private ICallGateSubscriber<string, uint, string, object>? _addToWhitelist;
    private ICallGateSubscriber<string, uint, object>? _removeFromWhitelist;

    private object? _frameworkHandler;
    private MethodInfo? _removeChecked;
    private bool _interopFailed;

    private DateTime _nextAvailabilityCheck = DateTime.MinValue;
    private DateTime _nextExpiry = DateTime.MinValue;
    private bool _available;
    private bool _disposed;

    public VisibilityService(Configuration.Configuration config, CordiLogService log)
    {
        _config = config;
        _log = log;

        Service.ClientState.Logout += OnLogout;
        Service.ClientState.TerritoryChanged += OnTerritoryChanged;
    }

    public bool FeatureEnabled => !_disposed && _config.CordiPeep.UnhideFromVisibility;

    public bool IsPluginAvailable => ResolveAvailability();

    public bool IsAvailable => FeatureEnabled && ResolveAvailability();

    public int ActiveLeases => _leases.Count;

    public void Unhide(IPlayerCharacter? player) => Unhide(player, MinimumHold);

    public void Unhide(IPlayerCharacter? player, TimeSpan hold)
    {
        if (player == null) return;
        if (!IsAvailable) return;

        var name = player.Name.TextValue;
        if (string.IsNullOrWhiteSpace(name)) return;

        var worldId = player.HomeWorld.RowId;
        if (worldId == 0) return;

        if (hold < MinimumHold) hold = MinimumHold;

        var id = player.GameObjectId;
        var expires = DateTime.UtcNow + hold;

        if (_leases.TryGetValue(id, out var existing))
        {
            if (expires > existing.Expires)
            {
                _leases[id] = existing with { Expires = expires };
            }

            return;
        }

        if (!EnsureInterop() && _unhidden.ContainsKey(id)) return;

        if (!Try(() =>
            {
                _addToWhitelist ??= Service.PluginInterface.GetIpcSubscriber<string, uint, string, object>("Visibility.AddToWhitelist");
                _addToWhitelist.InvokeAction(name, worldId, LeaseReason);
            }, "AddToWhitelist"))
        {
            return;
        }

        _unhidden[id] = 0;
        _leases[id] = new Lease(name, worldId, player.EntityId, expires);
        _log.Debug(LogSource, $"Whitelisted {name} in Visibility while Cordi needs them visible");
    }

    public void Release(ulong gameObjectId)
    {
        if (!_leases.TryRemove(gameObjectId, out var lease)) return;

        Try(() =>
        {
            _removeFromWhitelist ??= Service.PluginInterface.GetIpcSubscriber<string, uint, object>("Visibility.RemoveFromWhitelist");
            _removeFromWhitelist.InvokeAction(lease.Name, lease.WorldId);
        }, "RemoveFromWhitelist");

        ForgetChecked(lease.EntityId);

        _log.Debug(LogSource, $"Removed the Visibility whitelist entry for {lease.Name}");
    }

    public void ReleaseAll()
    {
        foreach (var id in _leases.Keys.ToList())
        {
            Release(id);
        }
    }

    public void OnFrameworkUpdate()
    {
        if (_disposed) return;

        if (!FeatureEnabled)
        {
            if (!_leases.IsEmpty) ReleaseAll();
            if (!_unhidden.IsEmpty) _unhidden.Clear();
            return;
        }

        var now = DateTime.UtcNow;
        if (now < _nextExpiry) return;
        _nextExpiry = now + ExpiryInterval;

        if (!ResolveAvailability())
        {
            if (!_leases.IsEmpty) ReleaseAll();
            return;
        }

        foreach (var (id, lease) in _leases)
        {
            if (now < lease.Expires) continue;
            Release(id);
        }
    }

    public void DumpDebugInfo()
    {
        _log.Info(LogSource, $"Feature enabled: {FeatureEnabled}");
        _log.Info(LogSource, $"Visibility plugin available: {ResolveAvailability()}");
        _log.Info(LogSource, $"Active whitelist leases: {_leases.Count}");
        _log.Info(LogSource, $"Players unhidden in this zone: {_unhidden.Count}");
        _log.Info(LogSource, $"Can hide players again without a zone change: {EnsureInterop()}");

        foreach (var (id, lease) in _leases)
        {
            _log.Info(LogSource, $"  {lease.Name} (world {lease.WorldId}, object {id:X}) until {lease.Expires:HH:mm:ss} UTC");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Service.ClientState.Logout -= OnLogout;
        Service.ClientState.TerritoryChanged -= OnTerritoryChanged;

        ReleaseAll();
        _unhidden.Clear();
    }

    private void OnLogout(int type, int code)
    {
        ReleaseAll();
        _unhidden.Clear();
    }

    private void OnTerritoryChanged(uint territoryType)
    {
        ReleaseAll();
        _unhidden.Clear();
    }

    private bool ResolveAvailability()
    {
        if (_disposed) return false;

        var now = DateTime.UtcNow;
        if (now < _nextAvailabilityCheck) return _available;
        _nextAvailabilityCheck = now + AvailabilityInterval;

        if (!IsPluginLoaded())
        {
            if (_available) _log.Info(LogSource, "The Visibility plugin is no longer loaded; Cordi stopped unhiding players.");
            Forget();
            return false;
        }

        int version;

        try
        {
            _apiVersion ??= Service.PluginInterface.GetIpcSubscriber<int>("Visibility.ApiVersion");
            version = _apiVersion.InvokeFunc();
        }
        catch (Exception ex)
        {
            if (_available) _log.Info(LogSource, "The Visibility plugin stopped answering; Cordi stopped unhiding players.");
            _log.Debug(LogSource, $"Visibility.ApiVersion is unavailable: {ex.Message}");
            Forget();
            return false;
        }

        if (version < MinimumApiVersion)
        {
            _log.Log(LogSource, CordiLogLevel.Warning, $"The installed Visibility plugin reports IPC version {version}, but Cordi needs at least {MinimumApiVersion}. Unhiding stays off.");
            Forget();
            return false;
        }

        if (!_available) _log.Info(LogSource, $"Visibility plugin detected (IPC version {version}).");

        _available = true;
        return true;
    }

    private bool IsPluginLoaded()
    {
        try
        {
            foreach (var plugin in Service.PluginInterface.InstalledPlugins)
            {
                if (!plugin.IsLoaded) continue;
                if (!string.Equals(plugin.InternalName, PluginInternalName, StringComparison.OrdinalIgnoreCase)) continue;

                return true;
            }
        }
        catch (Exception ex)
        {
            _log.Debug(LogSource, $"Could not enumerate installed plugins: {ex.Message}");
        }

        return false;
    }

    private bool EnsureInterop()
    {
        if (_interopFailed) return false;
        if (_removeChecked != null) return true;

        const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        try
        {
            _removeFromWhitelist ??= Service.PluginInterface.GetIpcSubscriber<string, uint, object>("Visibility.RemoveFromWhitelist");

            var channel = _removeFromWhitelist.GetType().GetProperty("Channel", any)?.GetValue(_removeFromWhitelist);
            var registered = channel?.GetType().GetProperty("Action", any)?.GetValue(channel) as Delegate;
            var api = registered?.Target;
            var handler = api?.GetType().GetField("frameworkHandler", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(api);
            var method = handler?.GetType().GetMethod("RemoveChecked", BindingFlags.Instance | BindingFlags.Public, null, [typeof(uint)], null);

            if (method == null)
            {
                _interopFailed = true;
                _log.Log(LogSource, CordiLogLevel.Warning, "Cordi could not reach the Visibility plugin's internals, so it cannot hide a player again on its own. Unhidden players stay visible until you change zone.");
                return false;
            }

            _frameworkHandler = handler;
            _removeChecked = method;
            return true;
        }
        catch (Exception ex)
        {
            _interopFailed = true;
            _log.Log(LogSource, CordiLogLevel.Warning, "Cordi could not reach the Visibility plugin's internals, so it cannot hide a player again on its own. Unhidden players stay visible until you change zone.", ex);
            return false;
        }
    }

    private void ForgetChecked(uint entityId)
    {
        if (entityId == 0) return;
        if (!EnsureInterop()) return;

        try
        {
            _removeChecked!.Invoke(_frameworkHandler, [entityId]);
        }
        catch (Exception ex)
        {
            _interopFailed = true;
            _log.Debug(LogSource, $"Visibility RemoveChecked failed: {ex.Message}");
        }
    }

    private bool Try(Action action, string gate)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            _available = false;
            _nextAvailabilityCheck = DateTime.UtcNow + AvailabilityInterval;
            _log.Debug(LogSource, $"Visibility.{gate} failed: {ex.Message}");
            return false;
        }
    }

    private void Forget()
    {
        _available = false;

        if (!_leases.IsEmpty) ReleaseAll();

        _apiVersion = null;
        _addToWhitelist = null;
        _removeFromWhitelist = null;
        _frameworkHandler = null;
        _removeChecked = null;
        _interopFailed = false;
        _leases.Clear();
        _unhidden.Clear();
    }
}
