using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Plugin;

namespace Cordi.Services;

/// <summary>
/// Defensive reflection layer for Lightless Sync internals.
/// Lightless's public IPC is intentionally minimal (LoadMcdf / GetHandledAddresses only),
/// so deeper integration (connection state, reconnect, pair list, ...) requires reaching
/// into the loaded plugin assembly via reflection.
///
/// Design rules — must hold even if Lightless ships breaking changes:
///   1. Every reflection call is wrapped in try/catch. Nothing throws out of this class.
///   2. Resolution is lazy + cached. A failed resolve flips a flag and stops retrying
///      until <see cref="ResolveRetryInterval"/> elapses, so a missing member can't spam logs.
///   3. Every member resolve tries multiple candidate names (forks rename things often).
///   4. Failures degrade the affected feature only — other features keep working.
///   5. Public surface returns nullable / bool — callers must handle "not available".
/// </summary>
public class LightlessReflection
{
    private static readonly TimeSpan ResolveRetryInterval = TimeSpan.FromMinutes(2);

    // Plugin assembly internal name candidates.
    private static readonly string[] AssemblyNameCandidates =
    {
        "LightlessSync",
        "LightlessClient",
        "Lightless",
    };

    // Service-locator candidates inside the plugin type.
    private static readonly string[] ServiceProviderMemberCandidates =
    {
        "ServiceProvider", "_serviceProvider", "Services", "_services", "Host", "_host",
    };

    // ApiController type-name candidates (the class that owns the websocket).
    private static readonly string[] ApiControllerTypeCandidates =
    {
        "ApiController", "MareApiController", "LightlessApiController", "ApiClient",
    };

    // Connection-state property/field candidates on the api controller.
    private static readonly string[] ConnectionStateMemberCandidates =
    {
        "ServerState", "_serverState", "State", "ConnectionState",
    };

    private static readonly string[] IsConnectedMemberCandidates =
    {
        "IsConnected", "Connected",
    };

    // Reconnect-method candidates.
    private static readonly string[] ReconnectMethodCandidates =
    {
        "CreateConnectionsAsync", "CreateConnections", "Reconnect", "ReconnectAsync",
        "Connect", "ConnectAsync", "Initialize", "InitializeAsync",
    };

    // PairManager type-name candidates.
    private static readonly string[] PairManagerTypeCandidates =
    {
        "PairManager",
    };

    // Tried in priority order at read time; the first non-null/empty result wins.
    private static readonly string[] PairListMemberCandidates =
    {
        "_allClientPairs", "AllUserPairs", "DirectPairs", "Pairs", "DirectPair",
    };

    // Method fallbacks on PairManager, tried when no member yielded a count.
    private static readonly string[] PairCountMethodCandidates =
    {
        "GetPairCount", "GetOnlineUserCount", "GetVisibleUserCount",
    };

    private readonly IDalamudPluginInterface _pi;
    private readonly object _lock = new();

    private DateTime _lastAttempt = DateTime.MinValue;
    private bool _initAttempted;

    // Cached resolved references. Any may be null if resolution failed.
    private Assembly? _asm;
    private Type? _pluginType;
    private object? _pluginInstance;
    private object? _serviceProvider;

    private Type? _apiControllerType;
    private object? _apiControllerInstance;
    private MemberInfo? _connectionStateMember; // PropertyInfo or FieldInfo
    private MemberInfo? _isConnectedMember;
    private MethodInfo? _reconnectMethod;

    private Type? _pairManagerType;
    private object? _pairManagerInstance;

    public LightlessReflection(IDalamudPluginInterface pi)
    {
        _pi = pi;
    }

    // ─── Public diagnostics ────────────────────────────────────────────────

    public bool AssemblyResolved => _asm != null;
    public bool PluginInstanceResolved => _pluginInstance != null;
    public bool ServiceProviderResolved => _serviceProvider != null;
    public bool ApiControllerTypeResolved => _apiControllerType != null;
    public bool ApiControllerResolved => _apiControllerInstance != null;
    public bool ReconnectResolved => _reconnectMethod != null;
    public bool ConnectionStateResolved => _connectionStateMember != null || _isConnectedMember != null;
    public bool PairManagerResolved => _pairManagerInstance != null;

    public string? AssemblyName => _asm?.GetName().Name;
    public string? PluginTypeName => _pluginType?.FullName;
    public string? ServiceProviderTypeName => _serviceProvider?.GetType().FullName;
    public string? ApiControllerTypeName => _apiControllerType?.FullName;
    public string? ReconnectMethodName => _reconnectMethod?.Name;
    public string? ConnectionStateMemberName => _connectionStateMember?.Name ?? _isConnectedMember?.Name;

    /// <summary>
    /// Dumps every field + property name on Lightless's IExposedPlugin wrapper to the log,
    /// so we can see which member holds the live plugin instance (if any).
    /// </summary>
    public void DumpExposedPluginMembers()
    {
        try
        {
            foreach (var exposed in _pi.InstalledPlugins)
            {
                bool match = false;
                try
                {
                    match = AssemblyNameCandidates.Any(n =>
                        string.Equals(exposed.InternalName, n, StringComparison.OrdinalIgnoreCase));
                }
                catch { }
                if (!match) continue;

                var t = exposed.GetType();
                Service.Log.Information($"[Lightless] Exposed wrapper type: {t.FullName}");
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    string val = "(unread)";
                    try { val = (f.GetValue(exposed)?.GetType().FullName) ?? "null"; } catch { val = "(threw)"; }
                    Service.Log.Information($"  field  {f.Name} : {f.FieldType.FullName}   = {val}");
                }
                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (p.GetIndexParameters().Length > 0) continue;
                    string val = "(unread)";
                    try { val = (p.GetValue(exposed)?.GetType().FullName) ?? "null"; } catch { val = "(threw)"; }
                    Service.Log.Information($"  prop   {p.Name} : {p.PropertyType.FullName}   = {val}");
                }

                // Drill into <plugin>P (the LocalPlugin held by ExposedPlugin).
                var localPluginField = t.GetField("<plugin>P", BindingFlags.NonPublic | BindingFlags.Instance);
                var localPlugin = SafeGet(() => localPluginField?.GetValue(exposed));
                if (localPlugin != null)
                {
                    var lt = localPlugin.GetType();
                    Service.Log.Information($"[Lightless] LocalPlugin type: {lt.FullName}");
                    foreach (var f in lt.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        string val = "(unread)";
                        try { val = (f.GetValue(localPlugin)?.GetType().FullName) ?? "null"; } catch { val = "(threw)"; }
                        Service.Log.Information($"  L.field  {f.Name} : {f.FieldType.FullName}   = {val}");
                    }
                    foreach (var p in lt.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (p.GetIndexParameters().Length > 0) continue;
                        string val = "(unread)";
                        try { val = (p.GetValue(localPlugin)?.GetType().FullName) ?? "null"; } catch { val = "(threw)"; }
                        Service.Log.Information($"  L.prop   {p.Name} : {p.PropertyType.FullName}   = {val}");
                    }
                }
                return;
            }
            Service.Log.Information("[Lightless] No matching InstalledPlugins entry found.");
        }
        catch (Exception ex)
        {
            Service.Log.Warning($"[Lightless] DumpExposedPluginMembers failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns type names from the loaded Lightless assembly, filtered to interesting candidates
    /// (anything ending in Controller / Manager / Service / Plugin / Host).
    /// Use this to spot the real internal names when adapting candidate lists after an update.
    /// </summary>
    public System.Collections.Generic.List<string> DumpInterestingTypes()
    {
        var result = new System.Collections.Generic.List<string>();
        EnsureResolved();
        if (_asm == null) return result;
        try
        {
            foreach (var t in SafeGetTypes(_asm))
            {
                try
                {
                    var n = t.Name;
                    if (n.EndsWith("Controller", StringComparison.Ordinal)
                     || n.EndsWith("Manager", StringComparison.Ordinal)
                     || n.EndsWith("Service", StringComparison.Ordinal)
                     || n.EndsWith("Plugin", StringComparison.Ordinal)
                     || n.EndsWith("Host", StringComparison.Ordinal)
                     || n.EndsWith("Client", StringComparison.Ordinal)
                     || n.EndsWith("Mediator", StringComparison.Ordinal))
                    {
                        result.Add(t.FullName ?? t.Name);
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Debug($"[Lightless.Reflection] DumpInterestingTypes failed: {ex.Message}");
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>Human-readable status string for the UI.</summary>
    public string StatusSummary
    {
        get
        {
            if (!_initAttempted) return "Not initialized";
            if (_asm == null) return "Lightless assembly not found";
            if (_pluginInstance == null) return "Plugin instance not resolved";
            int ok = 0, total = 0;
            void Count(bool b) { total++; if (b) ok++; }
            Count(ApiControllerResolved);
            Count(ConnectionStateResolved);
            Count(ReconnectResolved);
            Count(PairManagerResolved);
            return $"Deep integration: {ok}/{total} hooks resolved";
        }
    }

    // ─── Resolution ────────────────────────────────────────────────────────

    /// <summary>Force a fresh resolution attempt. Safe to call repeatedly.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _asm = null;
            _pluginType = null;
            _pluginInstance = null;
            _serviceProvider = null;
            _apiControllerType = null;
            _apiControllerInstance = null;
            _connectionStateMember = null;
            _isConnectedMember = null;
            _reconnectMethod = null;
            _pairManagerType = null;
            _pairManagerInstance = null;
            _initAttempted = false;
            _lastAttempt = DateTime.MinValue;
        }
    }

    private void EnsureResolved()
    {
        lock (_lock)
        {
            // Already fully resolved?
            if (_pluginInstance != null && _apiControllerInstance != null) return;

            // Honor retry cooldown.
            if (_initAttempted && (DateTime.UtcNow - _lastAttempt) < ResolveRetryInterval) return;

            _lastAttempt = DateTime.UtcNow;
            _initAttempted = true;

            try
            {
                ResolveAssembly();
                if (_asm == null) return;

                ResolvePluginInstance();
                if (_pluginInstance == null) return;

                ResolveServiceProvider();
                ResolveApiController();
                ResolvePairManager();
            }
            catch (Exception ex)
            {
                Service.Log.Debug($"[Lightless.Reflection] EnsureResolved failed: {ex.Message}");
            }
        }
    }

    private void ResolveAssembly()
    {
        try
        {
            _asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a =>
                {
                    try { return AssemblyNameCandidates.Any(n =>
                        string.Equals(a.GetName().Name, n, StringComparison.OrdinalIgnoreCase)); }
                    catch { return false; }
                });
        }
        catch (Exception ex)
        {
            Service.Log.Debug($"[Lightless.Reflection] ResolveAssembly: {ex.Message}");
        }
    }

    private void ResolvePluginInstance()
    {
        if (_asm == null) return;
        try
        {
            // Match by NAME, not by IDalamudPlugin interface — each Dalamud plugin loads
            // into its own AssemblyLoadContext, so Cordi's typeof(IDalamudPlugin) is not
            // the same Type as Lightless's IDalamudPlugin and IsAssignableFrom returns false.
            var allTypes = SafeGetTypes(_asm);
            var candidates = allTypes.Where(t =>
            {
                try
                {
                    if (t.IsAbstract || t.IsInterface) return false;
                    if (string.Equals(t.Name, "LightlessPlugin", StringComparison.Ordinal)) return true;
                    if (string.Equals(t.Name, "Plugin", StringComparison.Ordinal)) return true;
                    return t.GetInterfaces().Any(i => string.Equals(i.Name, "IDalamudPlugin", StringComparison.Ordinal));
                }
                catch { return false; }
            }).ToList();

            if (candidates.Count == 0) return;
            _pluginType = candidates.FirstOrDefault(t => string.Equals(t.Name, "LightlessPlugin", StringComparison.Ordinal))
                       ?? candidates.FirstOrDefault(t => !string.Equals(t.Name, "Plugin", StringComparison.Ordinal))
                       ?? candidates[0];

            // Primary path: ask Dalamud's plugin manager for the live IDalamudPlugin.
            // Modern Mare-style plugins build everything via IHost and have no static self-pointer.
            _pluginInstance = TryGetInstanceFromDalamud();
            if (_pluginInstance != null)
            {
                _pluginType = _pluginInstance.GetType();
                return;
            }

            // Fallback: static field on plugin type.
            foreach (var name in new[] { "Plugin", "Instance", "Self" })
            {
                var f = _pluginType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (f != null) { _pluginInstance = SafeGet(() => f.GetValue(null)); if (_pluginInstance != null) return; }
                var p = _pluginType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (p != null) { _pluginInstance = SafeGet(() => p.GetValue(null)); if (_pluginInstance != null) return; }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Debug($"[Lightless.Reflection] ResolvePluginInstance: {ex.Message}");
        }
    }

    /// <summary>
    /// Walk Dalamud's InstalledPlugins list and pull the live IDalamudPlugin instance out
    /// of Lightless's exposed wrapper via reflection.
    /// </summary>
    private object? TryGetInstanceFromDalamud()
    {
        try
        {
            foreach (var exposed in _pi.InstalledPlugins)
            {
                bool match = false;
                try
                {
                    match = AssemblyNameCandidates.Any(n =>
                        string.Equals(exposed.InternalName, n, StringComparison.OrdinalIgnoreCase));
                }
                catch { }
                if (!match) continue;

                // ExposedPlugin holds the real LocalPlugin in the auto-property backing field <plugin>P.
                var exposedType = exposed.GetType();
                var localPluginField = exposedType.GetField("<plugin>P",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var localPlugin = SafeGet(() => localPluginField?.GetValue(exposed));
                if (localPlugin != null)
                {
                    var found = ExtractLightlessInstanceFromLocalPlugin(localPlugin);
                    if (found != null) return found;
                }

                var t = exposed.GetType();
                // Common field/prop names where the live plugin instance is stored.
                foreach (var memberName in new[] { "Instance", "Plugin", "_plugin", "_instance", "DalamudPlugin" })
                {
                    var f = t.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null)
                    {
                        var v = SafeGet(() => f.GetValue(exposed));
                        if (IsFromLightlessAssembly(v)) return v;
                    }
                    var prop = t.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && prop.GetIndexParameters().Length == 0)
                    {
                        var v = SafeGet(() => prop.GetValue(exposed));
                        if (IsFromLightlessAssembly(v)) return v;
                    }
                }

                // Last resort: scan everything on the exposed wrapper.
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    var v = SafeGet(() => f.GetValue(exposed));
                    if (IsFromLightlessAssembly(v)) return v;
                }
                foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (prop.GetIndexParameters().Length > 0) continue;
                    var v = SafeGet(() => prop.GetValue(exposed));
                    if (IsFromLightlessAssembly(v)) return v;
                }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Debug($"[Lightless.Reflection] TryGetInstanceFromDalamud: {ex.Message}");
        }
        return null;
    }

    private void ResolveServiceProvider()
    {
        if (_pluginInstance == null) return;
        try
        {
            foreach (var name in ServiceProviderMemberCandidates)
            {
                var f = _pluginType!.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                {
                    var v = SafeGet(() => f.GetValue(_pluginInstance));
                    if (v != null) { _serviceProvider = UnwrapHostToServiceProvider(v); if (_serviceProvider != null) return; }
                }
                var p = _pluginType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null)
                {
                    var v = SafeGet(() => p.GetValue(_pluginInstance));
                    if (v != null) { _serviceProvider = UnwrapHostToServiceProvider(v); if (_serviceProvider != null) return; }
                }
            }

            // Fallback: any field on plugin whose type implements IServiceProvider.
            foreach (var f in _pluginType!.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (typeof(IServiceProvider).IsAssignableFrom(f.FieldType))
                {
                    _serviceProvider = SafeGet(() => f.GetValue(_pluginInstance));
                    if (_serviceProvider != null) return;
                }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Debug($"[Lightless.Reflection] ResolveServiceProvider: {ex.Message}");
        }
    }

    private static object? UnwrapHostToServiceProvider(object o)
    {
        if (o is IServiceProvider sp) return sp;
        try
        {
            // Microsoft.Extensions.Hosting.IHost exposes Services.
            var servicesProp = o.GetType().GetProperty("Services");
            if (servicesProp != null) return servicesProp.GetValue(o);
        }
        catch { }
        return null;
    }

    private void ResolveApiController()
    {
        if (_asm == null) return;
        try
        {
            _apiControllerType = SafeGetTypes(_asm).FirstOrDefault(t =>
            {
                try { return ApiControllerTypeCandidates.Any(c =>
                    string.Equals(t.Name, c, StringComparison.OrdinalIgnoreCase)); }
                catch { return false; }
            });
            if (_apiControllerType == null) return;

            _apiControllerInstance = ResolveFromServiceProvider(_apiControllerType)
                                  ?? ScanInstanceForType(_pluginInstance, _apiControllerType);
            if (_apiControllerInstance == null) return;

            // Resolve members.
            foreach (var name in ConnectionStateMemberCandidates)
            {
                var m = (MemberInfo?)_apiControllerType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                     ?? _apiControllerType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (m != null) { _connectionStateMember = m; break; }
            }
            foreach (var name in IsConnectedMemberCandidates)
            {
                var m = (MemberInfo?)_apiControllerType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                     ?? _apiControllerType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (m != null) { _isConnectedMember = m; break; }
            }
            foreach (var name in ReconnectMethodCandidates)
            {
                var m = _apiControllerType.GetMethod(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null, types: Type.EmptyTypes, modifiers: null);
                if (m != null) { _reconnectMethod = m; break; }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Debug($"[Lightless.Reflection] ResolveApiController: {ex.Message}");
        }
    }

    private void ResolvePairManager()
    {
        if (_asm == null) return;
        try
        {
            _pairManagerType = SafeGetTypes(_asm).FirstOrDefault(t =>
            {
                try { return PairManagerTypeCandidates.Any(c =>
                    string.Equals(t.Name, c, StringComparison.OrdinalIgnoreCase)); }
                catch { return false; }
            });
            if (_pairManagerType == null) return;

            _pairManagerInstance = ResolveFromServiceProvider(_pairManagerType)
                                ?? ScanInstanceForType(_pluginInstance, _pairManagerType);
            if (_pairManagerInstance == null) return;

            // No-op here — we resolve members lazily at read time in GetPairCount,
            // because each candidate may be present-but-null in different lifecycle phases.
        }
        catch (Exception ex)
        {
            Service.Log.Debug($"[Lightless.Reflection] ResolvePairManager: {ex.Message}");
        }
    }

    private object? ResolveFromServiceProvider(Type targetType)
    {
        if (_serviceProvider is not IServiceProvider sp) return null;
        try { return sp.GetService(targetType); }
        catch { return null; }
    }

    private object? ScanInstanceForType(object? root, Type targetType, int maxDepth = 2)
    {
        if (root == null || maxDepth < 0) return null;
        try
        {
            var t = root.GetType();
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                object? v;
                try { v = f.GetValue(root); } catch { continue; }
                if (v == null) continue;
                if (targetType.IsInstanceOfType(v)) return v;
                if (maxDepth > 0)
                {
                    var nested = ScanInstanceForType(v, targetType, maxDepth - 1);
                    if (nested != null) return nested;
                }
            }
        }
        catch { }
        return null;
    }

    private static object? SafeGet(Func<object?> f)
    {
        try { return f(); } catch { return null; }
    }

    private static Type[] SafeGetTypes(Assembly asm)
    {
        try { return asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex)
        {
            // Some types failed to load (missing transient deps); use what we got.
            return ex.Types?.Where(t => t != null).Select(t => t!).ToArray() ?? Array.Empty<Type>();
        }
        catch { return Array.Empty<Type>(); }
    }

    private bool IsFromLightlessAssembly(object? v)
    {
        if (v == null || _asm == null) return false;
        try { return v.GetType().Assembly == _asm; }
        catch { return false; }
    }

    /// <summary>
    /// Walks a Dalamud LocalPlugin object looking for the IDalamudPlugin instance
    /// (held in fields/properties whose type lives in the Lightless assembly).
    /// </summary>
    private object? ExtractLightlessInstanceFromLocalPlugin(object localPlugin)
    {
        try
        {
            var t = localPlugin.GetType();

            // Common Dalamud LocalPlugin member names that hold the plugin instance.
            foreach (var name in new[] { "InstanceUntyped", "Instance", "DalamudPlugin", "plugin", "_plugin" })
            {
                var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                {
                    var v = SafeGet(() => f.GetValue(localPlugin));
                    if (IsFromLightlessAssembly(v)) return v;
                }
                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.GetIndexParameters().Length == 0)
                {
                    var v = SafeGet(() => p.GetValue(localPlugin));
                    if (IsFromLightlessAssembly(v)) return v;
                }
            }

            // Last resort: scan every field/property for something from Lightless's assembly.
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var v = SafeGet(() => f.GetValue(localPlugin));
                if (IsFromLightlessAssembly(v)) return v;
            }
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                var v = SafeGet(() => p.GetValue(localPlugin));
                if (IsFromLightlessAssembly(v)) return v;
            }
        }
        catch (Exception ex)
        {
            Service.Log.Debug($"[Lightless.Reflection] ExtractLightlessInstanceFromLocalPlugin: {ex.Message}");
        }
        return null;
    }

    // ─── Feature API (every method returns nullable; never throws) ─────────

    /// <summary>
    /// True/false if state is known, null if unavailable.
    /// </summary>
    public bool? IsConnected()
    {
        EnsureResolved();
        try
        {
            if (_apiControllerInstance == null) return null;

            // Direct bool path.
            if (_isConnectedMember != null)
            {
                var v = ReadMember(_isConnectedMember, _apiControllerInstance);
                if (v is bool b) return b;
            }

            // ServerState enum path. "Connected" name convention.
            if (_connectionStateMember != null)
            {
                var v = ReadMember(_connectionStateMember, _apiControllerInstance);
                if (v != null)
                {
                    var s = v.ToString();
                    if (!string.IsNullOrEmpty(s))
                        return string.Equals(s, "Connected", StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Debug($"[Lightless.Reflection] IsConnected: {ex.Message}");
        }
        return null;
    }

    /// <summary>Raw connection-state value (e.g. enum name). Null if unavailable.</summary>
    public string? GetConnectionStateRaw()
    {
        EnsureResolved();
        try
        {
            if (_apiControllerInstance == null) return null;
            if (_connectionStateMember != null)
                return ReadMember(_connectionStateMember, _apiControllerInstance)?.ToString();
            if (_isConnectedMember != null)
            {
                var v = ReadMember(_isConnectedMember, _apiControllerInstance);
                if (v is bool b) return b ? "Connected" : "Disconnected";
            }
        }
        catch (Exception ex)
        {
            Service.Log.Debug($"[Lightless.Reflection] GetConnectionStateRaw: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Reconnect by running the canonical Mare-style cycle:
    /// <c>StopConnectionAsync(Disconnecting)</c> → small delay → <c>CreateConnectionsAsync()</c>.
    /// Falls back to a plain <c>CreateConnectionsAsync()</c> when StopConnectionAsync isn't present.
    /// Returns true if a sequence was dispatched (the actual Tasks are awaited in the background).
    /// </summary>
    public bool TryReconnect()
    {
        EnsureResolved();
        try
        {
            if (_apiControllerInstance == null || _apiControllerType == null) return false;

            var instance = _apiControllerInstance;
            var type = _apiControllerType;

            var stopMethod = type.GetMethod("StopConnectionAsync",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var startMethod = type.GetMethod("CreateConnectionsAsync",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null, types: Type.EmptyTypes, modifiers: null);

            // Resolve a ServerState enum value to pass to StopConnectionAsync.
            object? stopArg = null;
            if (stopMethod != null)
            {
                var enumType = stopMethod.GetParameters().FirstOrDefault()?.ParameterType;
                if (enumType != null && enumType.IsEnum)
                {
                    foreach (var name in new[] { "Disconnecting", "Disconnected", "Offline" })
                    {
                        try { stopArg = Enum.Parse(enumType, name, ignoreCase: true); break; }
                        catch { }
                    }
                }
            }

            var stateRaw = GetConnectionStateRaw();
            bool isDisconnectedNow = stateRaw != null && (
                string.Equals(stateRaw, "Disconnected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(stateRaw, "Offline", StringComparison.OrdinalIgnoreCase));

            // CRITICAL: Mare-style CreateConnectionsAsync silently returns when FullPause is true.
            // The "Disconnected" state in Mare/Lightless usually *is* the paused state, so we must
            // clear it before any reconnect attempt or every call below will be a no-op.
            var unpauseResult = TryClearFullPause();

            if (startMethod == null)
            {
                Service.Log.Warning("[Lightless.Reflection] CreateConnectionsAsync not found — cannot reconnect.");
                return false;
            }

            // When already disconnected we don't need a Stop step — go straight to Start.
            if (isDisconnectedNow)
            {
                Service.Log.Information(
                    $"[Lightless.Reflection] Connect (state={stateRaw}, unpaused={unpauseResult}): CreateConnectionsAsync()");
                InvokeAndLog(instance, startMethod, args: null, label: "CreateConnectionsAsync");
                return true;
            }

            // Otherwise run the canonical Mare reconnect cycle: Stop → delay → Start.
            if (stopMethod != null && stopArg != null)
            {
                Service.Log.Information(
                    $"[Lightless.Reflection] Reconnect cycle (state={stateRaw}, unpaused={unpauseResult}): StopConnectionAsync({stopArg}) → CreateConnectionsAsync()");
                RunReconnectCycle(instance, stopMethod, stopArg, startMethod);
                return true;
            }

            Service.Log.Information(
                $"[Lightless.Reflection] Connect-only fallback (state={stateRaw ?? "unknown"}, unpaused={unpauseResult}): CreateConnectionsAsync()");
            InvokeAndLog(instance, startMethod, args: null, label: "CreateConnectionsAsync");
            return true;
        }
        catch (Exception ex)
        {
            var inner = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
            Service.Log.Warning($"[Lightless.Reflection] TryReconnect failed: {inner.GetType().Name}: {inner.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sets the current server's <c>FullPause</c> flag to false and persists the change.
    /// Without this, Mare/Lightless's <c>CreateConnectionsAsync</c> silently aborts at its
    /// very first guard and no reconnect is attempted.
    /// Returns: "cleared" if pause was on and is now off, "already-off" if it was already off,
    /// "n/a" if the server config couldn't be resolved.
    /// </summary>
    private string TryClearFullPause()
    {
        try
        {
            if (_asm == null) return "n/a";

            // Find ServerConfigurationManager (or fork rename).
            var smType = SafeGetTypes(_asm).FirstOrDefault(t =>
            {
                try
                {
                    var n = t.Name;
                    return string.Equals(n, "ServerConfigurationManager", StringComparison.Ordinal)
                        || string.Equals(n, "ServerConfigManager", StringComparison.Ordinal)
                        || string.Equals(n, "ServerManager", StringComparison.Ordinal);
                }
                catch { return false; }
            });
            if (smType == null) return "n/a";

            var smInstance = ResolveFromServiceProvider(smType)
                          ?? ScanInstanceForType(_pluginInstance, smType);
            if (smInstance == null) return "n/a";

            // Get the active ServerStorage.
            var currentServerMember = (MemberInfo?)smType.GetProperty("CurrentServer",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? smType.GetField("CurrentServer",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var currentServer = currentServerMember != null
                ? ReadMember(currentServerMember, smInstance)
                : null;
            if (currentServer == null) return "n/a";

            var fullPauseProp = currentServer.GetType().GetProperty("FullPause",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fullPauseProp == null || !fullPauseProp.CanWrite) return "n/a";

            var wasPaused = (bool?)SafeGet(() => fullPauseProp.GetValue(currentServer)) ?? false;
            if (!wasPaused) return "already-off";

            fullPauseProp.SetValue(currentServer, false);

            // Persist so the change survives a restart and so the Lightless UI reflects it.
            var saveMethod = smType.GetMethod("Save",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null, types: Type.EmptyTypes, modifiers: null);
            if (saveMethod != null)
            {
                try { saveMethod.Invoke(smInstance, null); }
                catch (Exception ex)
                {
                    Service.Log.Debug($"[Lightless.Reflection] FullPause save failed: {ex.Message}");
                }
            }

            Service.Log.Information("[Lightless.Reflection] Cleared FullPause on current server.");
            return "cleared";
        }
        catch (Exception ex)
        {
            Service.Log.Debug($"[Lightless.Reflection] TryClearFullPause: {ex.Message}");
            return "n/a";
        }
    }

    /// <summary>
    /// Publishes a DalamudLoginMessage on Lightless's mediator — the same path the game uses
    /// to trigger an automatic connect when the player logs in.
    /// </summary>
    private bool TryPublishLoginMessage(object apiController, Type apiControllerType)
    {
        try
        {
            // Get the Mediator property/field on ApiController.
            var mediatorMember = (MemberInfo?)apiControllerType.GetProperty("Mediator",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? apiControllerType.GetField("Mediator",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var mediator = mediatorMember != null ? ReadMember(mediatorMember, apiController) : null;
            if (mediator == null)
            {
                Service.Log.Warning("[Lightless.Reflection] TryPublishLoginMessage: Mediator unavailable.");
                return false;
            }

            // Find the DalamudLoginMessage type in the Lightless assembly.
            var msgType = SafeGetTypes(_asm!).FirstOrDefault(t =>
                string.Equals(t.Name, "DalamudLoginMessage", StringComparison.Ordinal));
            if (msgType == null)
            {
                Service.Log.Warning("[Lightless.Reflection] TryPublishLoginMessage: DalamudLoginMessage type not found.");
                return false;
            }

            // Construct an instance. Records / parameterless ctor preferred; fall back to uninitialized object.
            object? msg = null;
            try { msg = Activator.CreateInstance(msgType); } catch { }
            if (msg == null)
            {
                try
                {
                    msg = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(msgType);
                }
                catch (Exception ex)
                {
                    Service.Log.Warning($"[Lightless.Reflection] TryPublishLoginMessage: cannot construct DalamudLoginMessage ({ex.Message}).");
                    return false;
                }
            }

            // Find a Publish<T>(T msg) method on the mediator.
            var publish = mediator.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "Publish"
                                  && m.IsGenericMethodDefinition
                                  && m.GetParameters().Length == 1);

            if (publish == null)
            {
                // Non-generic Publish?
                publish = mediator.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "Publish" && m.GetParameters().Length == 1);
            }

            if (publish == null)
            {
                Service.Log.Warning("[Lightless.Reflection] TryPublishLoginMessage: Publish method not found on mediator.");
                return false;
            }

            Service.Log.Information(
                $"[Lightless.Reflection] Publishing DalamudLoginMessage via {mediator.GetType().Name}.Publish()");

            var publishToCall = publish.IsGenericMethodDefinition
                ? publish.MakeGenericMethod(msgType)
                : publish;

            _ = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    publishToCall.Invoke(mediator, new[] { msg });
                    Service.Log.Information("[Lightless.Reflection] DalamudLoginMessage publish completed.");
                }
                catch (Exception ex)
                {
                    var inner = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
                    Service.Log.Warning($"[Lightless.Reflection] Publish failed: {inner.GetType().Name}: {inner.Message}");
                }
            });
            return true;
        }
        catch (Exception ex)
        {
            Service.Log.Warning($"[Lightless.Reflection] TryPublishLoginMessage: {ex.Message}");
            return false;
        }
    }

    private static void RunReconnectCycle(object instance, MethodInfo stop, object stopArg, MethodInfo start)
    {
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var stopResult = stop.Invoke(instance, new[] { stopArg });
                if (stopResult is System.Threading.Tasks.Task stopTask) await stopTask.ConfigureAwait(false);
                Service.Log.Information("[Lightless.Reflection] Stop step completed.");

                await System.Threading.Tasks.Task.Delay(750).ConfigureAwait(false);

                var startResult = start.Invoke(instance, null);
                if (startResult is System.Threading.Tasks.Task startTask) await startTask.ConfigureAwait(false);
                Service.Log.Information("[Lightless.Reflection] Start step completed.");
            }
            catch (Exception ex)
            {
                var inner = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
                Service.Log.Warning($"[Lightless.Reflection] Reconnect cycle failed: {inner.GetType().Name}: {inner.Message}");
            }
        });
    }

    private static void InvokeAndLog(object instance, MethodInfo method, object?[]? args, string label)
    {
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var result = method.Invoke(instance, args);
                if (result is System.Threading.Tasks.Task t) await t.ConfigureAwait(false);
                Service.Log.Information($"[Lightless.Reflection] {label} completed.");
            }
            catch (Exception ex)
            {
                var inner = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
                Service.Log.Warning($"[Lightless.Reflection] {label} failed: {inner.GetType().Name}: {inner.Message}");
            }
        });
    }

    /// <summary>
    /// Dumps the names + signatures of all instance methods on the ApiController to the log,
    /// to spot the right reconnect entry point when CreateConnectionsAsync alone doesn't restore the connection.
    /// </summary>
    public void DumpApiControllerMethods()
    {
        EnsureResolved();
        try
        {
            if (_apiControllerType == null)
            {
                Service.Log.Information("[Lightless] ApiController type not resolved.");
                return;
            }
            Service.Log.Information($"[Lightless] ApiController methods on {_apiControllerType.FullName}:");
            foreach (var m in _apiControllerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (m.DeclaringType == typeof(object)) continue;
                var ps = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                Service.Log.Information($"  {m.ReturnType.Name} {m.Name}({ps})");
            }
        }
        catch (Exception ex)
        {
            Service.Log.Warning($"[Lightless] DumpApiControllerMethods failed: {ex.Message}");
        }
    }

    /// <summary>Total known pairs, or null if pair manager unavailable.</summary>
    public int? GetPairCount()
    {
        EnsureResolved();
        try
        {
            if (_pairManagerInstance == null || _pairManagerType == null) return null;

            // Try member-based candidates first.
            foreach (var name in PairListMemberCandidates)
            {
                var member = (MemberInfo?)_pairManagerType.GetProperty(name,
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                          ?? _pairManagerType.GetField(name,
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (member == null) continue;

                var v = ReadMember(member, _pairManagerInstance);
                if (v == null) continue;

                if (v is ICollection col) return col.Count;
                if (v is IEnumerable e)
                {
                    try { return e.Cast<object>().Count(); } catch { /* try next */ }
                }
            }

            // Method-based fallbacks (Mare exposes GetVisibleUserCount / GetOnlineUserCount).
            foreach (var name in PairCountMethodCandidates)
            {
                var m = _pairManagerType.GetMethod(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null, types: Type.EmptyTypes, modifiers: null);
                if (m == null) continue;
                try
                {
                    var v = m.Invoke(_pairManagerInstance, null);
                    if (v is int i) return i;
                    if (v is long l) return (int)l;
                }
                catch { /* try next */ }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Debug($"[Lightless.Reflection] GetPairCount: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Diagnostics: dumps every field+property on the resolved PairManager so we can spot
    /// the right name for pair-count when none of the candidates match.
    /// </summary>
    public void DumpPairManagerMembers()
    {
        EnsureResolved();
        try
        {
            if (_pairManagerType == null || _pairManagerInstance == null)
            {
                Service.Log.Information("[Lightless] PairManager not resolved (type or instance missing).");
                return;
            }
            Service.Log.Information($"[Lightless] PairManager type: {_pairManagerType.FullName}");
            foreach (var f in _pairManagerType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                string val = "(unread)";
                try
                {
                    var v = f.GetValue(_pairManagerInstance);
                    val = v == null ? "null"
                        : v is ICollection c ? $"{v.GetType().Name} count={c.Count}"
                        : v.GetType().Name;
                }
                catch { val = "(threw)"; }
                Service.Log.Information($"  field  {f.Name} : {f.FieldType.Name}  = {val}");
            }
            foreach (var p in _pairManagerType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                string val = "(unread)";
                try
                {
                    var v = p.GetValue(_pairManagerInstance);
                    val = v == null ? "null"
                        : v is ICollection c ? $"{v.GetType().Name} count={c.Count}"
                        : v.GetType().Name;
                }
                catch { val = "(threw)"; }
                Service.Log.Information($"  prop   {p.Name} : {p.PropertyType.Name}  = {val}");
            }
            foreach (var m in _pairManagerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (m.DeclaringType == typeof(object)) continue;
                if (m.GetParameters().Length != 0) continue;
                if (m.ReturnType != typeof(int) && m.ReturnType != typeof(long)) continue;
                Service.Log.Information($"  method {m.ReturnType.Name} {m.Name}()");
            }
        }
        catch (Exception ex)
        {
            Service.Log.Warning($"[Lightless] DumpPairManagerMembers failed: {ex.Message}");
        }
    }

    private static object? ReadMember(MemberInfo m, object target)
    {
        try
        {
            return m switch
            {
                PropertyInfo p => p.GetValue(target),
                FieldInfo f => f.GetValue(target),
                _ => null,
            };
        }
        catch { return null; }
    }
}
