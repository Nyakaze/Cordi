using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;

namespace Cordi.Services;

/// <summary>
/// Thin wrapper around the IPC surface exposed by the Lightless Sync plugin.
/// Lightless only exposes three gates:
///   - LightlessSync.LoadMcdf(path, target) -> bool
///   - LightlessSync.LoadMcdfAsync(path, target) -> Task&lt;bool&gt;
///   - LightlessSync.GetHandledAddresses() -> List&lt;nint&gt;
/// There is intentionally NO connection-state / reconnect / pair-list IPC in Lightless.
/// </summary>
public class LightlessBridge : IDisposable
{
    private const string PluginInternalName = "LightlessSync";

    private readonly IDalamudPluginInterface _pluginInterface;

    private readonly ICallGateSubscriber<string, IGameObject, bool> _loadMcdf;
    private readonly ICallGateSubscriber<string, IGameObject, Task<bool>> _loadMcdfAsync;
    private readonly ICallGateSubscriber<List<nint>> _getHandledAddresses;

    public LightlessReflection Reflection { get; }

    public LightlessBridge(IDalamudPluginInterface pi)
    {
        _pluginInterface = pi;

        _loadMcdf = pi.GetIpcSubscriber<string, IGameObject, bool>("LightlessSync.LoadMcdf");
        _loadMcdfAsync = pi.GetIpcSubscriber<string, IGameObject, Task<bool>>("LightlessSync.LoadMcdfAsync");
        _getHandledAddresses = pi.GetIpcSubscriber<List<nint>>("LightlessSync.GetHandledAddresses");

        Reflection = new LightlessReflection(pi);
    }

    /// <summary>
    /// True if Lightless says it is connected to its server.
    /// Null when unknown (reflection layer didn't resolve, or plugin not loaded).
    /// </summary>
    public bool? IsConnected() => Reflection.IsConnected();

    /// <summary>Raw connection state string ("Connected", "Disconnected", "Reconnecting", ...).</summary>
    public string? ConnectionStateRaw() => Reflection.GetConnectionStateRaw();

    /// <summary>Fire-and-forget reconnect attempt. False if reflection couldn't resolve the method.</summary>
    public bool TryReconnect() => Reflection.TryReconnect();

    /// <summary>Total pair count from PairManager (reflection). Null if unavailable.</summary>
    public int? GetPairCount() => Reflection.GetPairCount();

    /// <summary>
    /// True if the Lightless Sync plugin is installed and loaded.
    /// Note: this does NOT mean Lightless is connected to its server — that state
    /// is not exposed via IPC and cannot be queried.
    /// </summary>
    public bool IsAvailable
    {
        get
        {
            try
            {
                return _pluginInterface.InstalledPlugins.Any(p =>
                    p.IsLoaded && string.Equals(p.InternalName, PluginInternalName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Native pointers of game objects Lightless is currently handling (i.e. synced/visible pairs).
    /// Returns empty list if IPC is unavailable.
    /// </summary>
    public IReadOnlyList<nint> GetHandledAddresses()
    {
        try
        {
            return _getHandledAddresses.InvokeFunc() ?? new List<nint>();
        }
        catch (IpcNotReadyError)
        {
            return Array.Empty<nint>();
        }
        catch (Exception ex)
        {
            Service.Log.Warning($"[Lightless] GetHandledAddresses failed: {ex.Message}");
            return Array.Empty<nint>();
        }
    }

    /// <summary>
    /// Fire-and-forget MCDF apply. Returns false if IPC unavailable or call threw.
    /// </summary>
    public bool LoadMcdf(string path, IGameObject target)
    {
        try
        {
            return _loadMcdf.InvokeFunc(path, target);
        }
        catch (Exception ex)
        {
            Service.Log.Warning($"[Lightless] LoadMcdf failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Awaitable MCDF apply.
    /// </summary>
    public async Task<bool> LoadMcdfAsync(string path, IGameObject target)
    {
        try
        {
            return await _loadMcdfAsync.InvokeFunc(path, target).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Service.Log.Warning($"[Lightless] LoadMcdfAsync failed: {ex.Message}");
            return false;
        }
    }

    public void Dispose() { }
}
