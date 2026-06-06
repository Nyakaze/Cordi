using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Dalamud.Plugin.Services;
using Dalamud.Plugin.Ipc;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Cordi.Core;

namespace Cordi.Services.Features;

public static class VisibilityBridge
{
    private static readonly IPluginLog Log = Service.Log;

    // Track players temporarily unhidden
    private class TempUnhideState
    {
        public string Name = string.Empty;
        public string World = string.Empty;
        public uint HomeworldId;
        public ulong ObjectId;
        public ulong ContentId;
        public DateTime? LastTargetedTime;
        public DateTime? EmoteExpireTime;
    }

    private static readonly ConcurrentDictionary<ulong, TempUnhideState> TempUnhiddenPlayers = new();

    private const string WhitelistReason = "Cordi Peeper";

    // Visibility's official IPC. AddToWhitelist internally adds the entry AND calls
    // RemoveChecked + ShowPlayer, which is the only reliable way to make Visibility itself
    // keep a player shown. Reflecting into its internals raced Visibility's per-frame re-hide.
    private static ICallGateSubscriber<string, uint, string, object>? _ipcAddToWhitelist;
    private static ICallGateSubscriber<string, uint, object>? _ipcRemoveFromWhitelist;

    private static bool IpcAddToWhitelist(string name, uint worldId, string reason)
    {
        try
        {
            _ipcAddToWhitelist ??= Service.PluginInterface.GetIpcSubscriber<string, uint, string, object>("Visibility.AddToWhitelist");
            _ipcAddToWhitelist.InvokeAction(name, worldId, reason);
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug($"[VisibilityBridge] IPC AddToWhitelist failed: {ex.Message}");
            return false;
        }
    }

    private static bool IpcRemoveFromWhitelist(string name, uint worldId)
    {
        try
        {
            _ipcRemoveFromWhitelist ??= Service.PluginInterface.GetIpcSubscriber<string, uint, object>("Visibility.RemoveFromWhitelist");
            _ipcRemoveFromWhitelist.InvokeAction(name, worldId);
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug($"[VisibilityBridge] IPC RemoveFromWhitelist failed: {ex.Message}");
            return false;
        }
    }

    private static Assembly? _visibilityAssembly;
    private static Type? _pluginType;
    private static Type? _voidItemType;

    private static object? GetMemberValue(object? obj, MemberInfo? member)
    {
        if (member is PropertyInfo prop) return prop.GetValue(obj);
        if (member is FieldInfo field) return field.GetValue(obj);
        return null;
    }

    private static void SetMemberValue(object? obj, MemberInfo? member, object? value)
    {
        if (member is PropertyInfo prop) prop.SetValue(obj, value);
        else if (member is FieldInfo field) field.SetValue(obj, value);
    }

    private static MemberInfo? GetFieldOrProperty(Type type, string name)
    {
        return (MemberInfo?)type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? (MemberInfo?)type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }

    private static object? _visibilityPluginInstance;
    private static DateTime _lastInstanceCheckTime = DateTime.MinValue;

    private static object? FindPluginInstance(string pluginTypeName)
    {
        try
        {
            var visited = new HashSet<object>();

            // 1. Search static fields/properties in Visibility assembly
            if (_visibilityAssembly != null)
            {
                foreach (var type in _visibilityAssembly.GetTypes())
                {
                    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        try
                        {
                            var val = field.GetValue(null);
                            if (val != null && val.GetType().FullName == pluginTypeName)
                            {
                                Log.Info($"[VisibilityBridge] Found plugin instance in Visibility static field: {type.FullName}.{field.Name}");
                                return val;
                            }
                        }
                        catch {}
                    }
                    foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        try
                        {
                            var val = prop.GetValue(null);
                            if (val != null && val.GetType().FullName == pluginTypeName)
                            {
                                Log.Info($"[VisibilityBridge] Found plugin instance in Visibility static property: {type.FullName}.{prop.Name}");
                                return val;
                            }
                        }
                        catch {}
                    }
                }
            }

            // 2. Directly search InstalledPlugins on PluginInterface
            var pi = Service.PluginInterface;
            if (pi != null)
            {
                var installedPluginsProp = pi.GetType().GetProperty("InstalledPlugins", BindingFlags.Public | BindingFlags.Instance);
                if (installedPluginsProp != null)
                {
                    var installedPlugins = installedPluginsProp.GetValue(pi) as System.Collections.IEnumerable;
                    if (installedPlugins != null)
                    {
                        foreach (var wrapper in installedPlugins)
                        {
                            if (wrapper == null) continue;
                            var wrapperType = wrapper.GetType();

                            // Use CrawlObject to recursively search this wrapper's fields
                            var res = CrawlObject(wrapper, pluginTypeName, visited, 0);
                            if (res != null)
                            {
                                Log.Info($"[VisibilityBridge] Found plugin instance by crawling wrapper {wrapperType.FullName}");
                                return res;
                            }
                        }
                    }
                }
            }

            // 3. Search static fields/properties in Dalamud assembly
            var dalamudAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Dalamud");
            if (dalamudAssembly != null)
            {
                foreach (var type in dalamudAssembly.GetTypes())
                {
                    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        try
                        {
                            if (field.FieldType.IsPrimitive || field.FieldType == typeof(string) || field.FieldType.IsEnum) continue;
                            var val = field.GetValue(null);
                            if (val == null) continue;

                            if (val.GetType().FullName == pluginTypeName)
                            {
                                Log.Info($"[VisibilityBridge] Found plugin instance in Dalamud static field: {type.FullName}.{field.Name}");
                                return val;
                            }

                            if (val.GetType().Name.Contains("PluginManager") || val.GetType().Name.Contains("PluginLoader"))
                            {
                                Log.Info($"[VisibilityBridge] Found manager {val.GetType().FullName} in Dalamud static field: {type.FullName}.{field.Name}, crawling...");
                                var res = CrawlObject(val, pluginTypeName, visited, 0);
                                if (res != null) return res;
                            }
                        }
                        catch {}
                    }

                    foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        try
                        {
                            if (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string) || prop.PropertyType.IsEnum) continue;
                            var val = prop.GetValue(null);
                            if (val == null) continue;

                            if (val.GetType().FullName == pluginTypeName)
                            {
                                Log.Info($"[VisibilityBridge] Found plugin instance in Dalamud static property: {type.FullName}.{prop.Name}");
                                return val;
                            }

                            if (val.GetType().Name.Contains("PluginManager") || val.GetType().Name.Contains("PluginLoader"))
                            {
                                Log.Info($"[VisibilityBridge] Found manager {val.GetType().FullName} in Dalamud static property: {type.FullName}.{prop.Name}, crawling...");
                                var res = CrawlObject(val, pluginTypeName, visited, 0);
                                if (res != null) return res;
                            }
                        }
                        catch {}
                    }
                }
            }

            // 4. Fallback: crawl from PluginInterface
            if (pi != null)
            {
                return CrawlObject(pi, pluginTypeName, visited, depth: 0);
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Info($"[VisibilityBridge] Error in FindPluginInstance: {ex}");
            return null;
        }
    }

    private static object? CrawlObject(object obj, string targetTypeName, HashSet<object> visited, int depth)
    {
        if (obj == null || depth > 5) return null;
        if (!visited.Add(obj)) return null;

        var type = obj.GetType();
        if (type.FullName == targetTypeName) return obj;

        // If it's a dictionary, crawl keys and values
        if (obj is System.Collections.IDictionary dict)
        {
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                if (entry.Value != null)
                {
                    var res = CrawlObject(entry.Value, targetTypeName, visited, depth + 1);
                    if (res != null) return res;
                }
                if (entry.Key != null)
                {
                    var res = CrawlObject(entry.Key, targetTypeName, visited, depth + 1);
                    if (res != null) return res;
                }
            }
        }
        // If it's a collection or array, crawl items
        else if (obj is System.Collections.IEnumerable enumerable && obj is not string)
        {
            foreach (var item in enumerable)
            {
                if (item != null)
                {
                    var res = CrawlObject(item, targetTypeName, visited, depth + 1);
                    if (res != null) return res;
                }
            }
        }

        // Crawl instance fields
        var currType = type;
        while (currType != null)
        {
            var fields = currType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var fType = field.FieldType;
                if (fType.IsPrimitive || fType == typeof(string) || fType.IsEnum || fType.Namespace?.StartsWith("System.Reflection") == true || fType.Namespace?.StartsWith("System.Diagnostics") == true)
                    continue;

                try
                {
                    var val = field.GetValue(obj);
                    if (val != null)
                    {
                        var res = CrawlObject(val, targetTypeName, visited, depth + 1);
                        if (res != null) return res;
                    }
                }
                catch { }
            }
            currType = currType.BaseType;
        }

        return null;
    }

    private static object? GetVisibilityPluginInstance()
    {
        if (_visibilityPluginInstance != null) return _visibilityPluginInstance;

        var now = DateTime.Now;
        if ((now - _lastInstanceCheckTime).TotalSeconds < 5.0) return null;
        _lastInstanceCheckTime = now;

        try
        {
            if (_visibilityAssembly == null)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                _visibilityAssembly = assemblies.FirstOrDefault(a => a.GetName().Name == "Visibility");
                if (_visibilityAssembly == null)
                {
                    Log.Info("[VisibilityBridge] Visibility assembly not found in AppDomain. Current assemblies containing 'Vis':");
                    foreach (var asm in assemblies)
                    {
                        var name = asm.GetName().Name;
                        if (name != null && (name.Contains("Vis") || name.Contains("Plugin")))
                        {
                            Log.Info($"  - {name}");
                        }
                    }
                    return null;
                }
            }

            if (_pluginType == null)
            {
                _pluginType = _visibilityAssembly.GetType("Visibility.VisibilityPlugin")
                           ?? _visibilityAssembly.GetType("Visibility.Plugin");
                if (_pluginType == null)
                {
                    Log.Info($"[VisibilityBridge] VisibilityPlugin type not found in assembly {_visibilityAssembly.FullName}");
                    return null;
                }
            }

            var instance = FindPluginInstance(_pluginType.FullName!);
            if (instance != null)
            {
                _visibilityPluginInstance = instance;

                // Rebind all reflection metadata to the instance's ACTUAL type/assembly.
                // Dalamud loads each plugin in its own AssemblyLoadContext, so the "Visibility"
                // assembly we discovered by scanning the AppDomain can be a different load
                // context than the live plugin instance. Both produce a Type named
                // "Visibility.VisibilityPlugin", but a FieldInfo obtained from one cannot read
                // an instance of the other ("Field 'configuration' ... is not a field on the
                // target object"). Always trust the instance's own type.
                _pluginType = instance.GetType();
                _visibilityAssembly = _pluginType.Assembly;
                ResetReflectionCaches(); // force re-resolve from the correct assembly
            }
            else
            {
                Log.Info($"[VisibilityBridge] FindPluginInstance returned null for {_pluginType.FullName}");
            }
            return instance;
        }
        catch (Exception ex)
        {
            Log.Info($"[VisibilityBridge] Failed to get plugin instance: {ex}");
            return null;
        }
    }

    private static MemberInfo? _configMember;

    private static object? GetVisibilityConfig()
    {
        var pluginInstance = GetVisibilityPluginInstance();
        if (pluginInstance == null) return null;

        try
        {
            // Resolve the member once; this runs every framework tick.
            _configMember ??= (MemberInfo?)_pluginType!.GetProperty("Configuration", BindingFlags.Public | BindingFlags.Instance)
                          ?? (MemberInfo?)_pluginType.GetProperty("Config", BindingFlags.Public | BindingFlags.Instance)
                          ?? (MemberInfo?)_pluginType.GetField("configuration", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                          ?? (MemberInfo?)_pluginType.GetField("configuration", BindingFlags.Public | BindingFlags.Instance)
                          ?? (MemberInfo?)_pluginType.GetField("configuration", BindingFlags.NonPublic | BindingFlags.Instance)
                          ?? (MemberInfo?)_pluginType.GetField("Config", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            return GetMemberValue(pluginInstance, _configMember);
        }
        catch (Exception ex)
        {
            // Throttle: this runs every framework tick, so an unthrottled log floods the console.
            if ((DateTime.Now - _lastConfigErrorLogTime).TotalSeconds > 10.0)
            {
                _lastConfigErrorLogTime = DateTime.Now;
                Log.Debug($"[VisibilityBridge] Failed to get config instance: {ex.Message}");
            }
            // The cached instance is likely stale (e.g. Visibility was reloaded); drop it and
            // its reflection caches so the next lookup re-discovers the live instance.
            _visibilityPluginInstance = null;
            ResetReflectionCaches();
            return null;
        }
    }

    private static DateTime _lastConfigErrorLogTime = DateTime.MinValue;

    private static MemberInfo? _enabledMember;
    private static MemberInfo? _currentConfigMember;
    private static MemberInfo? _hidePlayerMember;
    private static bool _disabledMembersResolved;

    // Reset every cached MemberInfo/Type so they are re-resolved against the live plugin.
    // Called when the instance is (re)discovered or dropped, since a Visibility reload swaps
    // the AssemblyLoadContext and invalidates all previously cached reflection metadata.
    private static void ResetReflectionCaches()
    {
        _configMember = null;
        _voidItemType = null;
        _enabledMember = null;
        _currentConfigMember = null;
        _hidePlayerMember = null;
        _disabledMembersResolved = false;
    }

    // True when Visibility is enabled AND configured to hide players in the current territory.
    // Caches the member lookups because this is evaluated on every framework tick.
    private static bool IsVisibilityHidingPlayers(object config)
    {
        try
        {
            if (!_disabledMembersResolved)
            {
                _enabledMember = GetFieldOrProperty(config.GetType(), "Enabled");
                _currentConfigMember = GetFieldOrProperty(config.GetType(), "CurrentConfig");
                _disabledMembersResolved = true;
            }

            if (_enabledMember != null && GetMemberValue(config, _enabledMember) is bool enabled && !enabled)
            {
                LogHidingDecision("Enabled=false", config);
                return false;
            }

            if (_currentConfigMember != null)
            {
                var currentConfig = GetMemberValue(config, _currentConfigMember);
                if (currentConfig != null)
                {
                    _hidePlayerMember ??= GetFieldOrProperty(currentConfig.GetType(), "HidePlayer");
                    if (_hidePlayerMember != null && GetMemberValue(currentConfig, _hidePlayerMember) is bool hidePlayer && !hidePlayer)
                    {
                        LogHidingDecision("HidePlayer=false", config);
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Debug($"[VisibilityBridge] Error checking visibility configuration state: {ex.Message}");
            // On error, assume Visibility is hiding players so we still attempt to unhide.
            return true;
        }
    }

    private static DateTime _lastHidingDecisionLogTime = DateTime.MinValue;

    // Throttled diagnostic: when we conclude Visibility is NOT hiding players (which triggers the
    // tracked-player release), record why and against which type, so a schema/reflection mismatch
    // is easy to spot instead of silently un-hiding everyone.
    private static void LogHidingDecision(string reason, object config)
    {
        if ((DateTime.Now - _lastHidingDecisionLogTime).TotalSeconds <= 10.0) return;
        _lastHidingDecisionLogTime = DateTime.Now;
        Log.Debug($"[VisibilityBridge] Treating Visibility as not hiding players ({reason}); config type = {config.GetType().FullName}");
    }

    public static bool IsVisibilityLoaded()
    {
        return GetVisibilityPluginInstance() != null;
    }

    public static bool IsPlayerHidden(ulong objectId)
    {
        try
        {
            var pluginInstance = GetVisibilityPluginInstance();
            if (pluginInstance == null) return false;

            var frameworkHandlerField = _pluginType!.GetField("frameworkHandler", BindingFlags.NonPublic | BindingFlags.Instance)
                                     ?? _pluginType.GetField("frameworkHandler", BindingFlags.Public | BindingFlags.Instance);
            if (frameworkHandlerField == null) return false;

            var frameworkHandler = frameworkHandlerField.GetValue(pluginInstance);
            if (frameworkHandler == null) return false;

            var visibilityManagerField = frameworkHandler.GetType().GetField("visibilityManager", BindingFlags.NonPublic | BindingFlags.Instance)
                                      ?? frameworkHandler.GetType().GetField("visibilityManager", BindingFlags.Public | BindingFlags.Instance);
            if (visibilityManagerField == null) return false;

            var visibilityManager = visibilityManagerField.GetValue(frameworkHandler);
            if (visibilityManager == null) return false;

            var isObjectHiddenMethod = visibilityManager.GetType().GetMethod("IsObjectHidden", BindingFlags.Public | BindingFlags.Instance);

            if (isObjectHiddenMethod != null)
            {
                var parameters = isObjectHiddenMethod.GetParameters();
                object? result;
                if (parameters.Length == 2)
                {
                    var enumType = parameters[1].ParameterType;
                    var characterEnumVal = Enum.ToObject(enumType, 0);
                    result = isObjectHiddenMethod.Invoke(visibilityManager, new object[] { (uint)objectId, characterEnumVal });
                }
                else
                {
                    result = isObjectHiddenMethod.Invoke(visibilityManager, new object[] { (uint)objectId });
                }
                return result is bool isHidden && isHidden;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[VisibilityBridge] Failed to check if player is hidden: {ex}");
        }
        return false;
    }

    private static void ClearVisibilityCache(ulong objectId)
    {
        try
        {
            var pluginInstance = GetVisibilityPluginInstance();
            if (pluginInstance == null) return;

            var frameworkHandlerField = _pluginType!.GetField("frameworkHandler", BindingFlags.NonPublic | BindingFlags.Instance)
                                     ?? _pluginType.GetField("frameworkHandler", BindingFlags.Public | BindingFlags.Instance);
            if (frameworkHandlerField == null) return;

            var frameworkHandler = frameworkHandlerField.GetValue(pluginInstance);
            if (frameworkHandler == null) return;

            var removeCheckedMethod = frameworkHandler.GetType().GetMethod("RemoveChecked", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(uint) }, null);
            if (removeCheckedMethod != null)
            {
                removeCheckedMethod.Invoke(frameworkHandler, new object[] { (uint)objectId });
                Log.Debug($"[VisibilityBridge] Cleared checked cache in FrameworkHandler for ObjectId: {objectId:X}");
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"[VisibilityBridge] Failed to clear checked cache: {ex.Message}");
        }
    }

    public static unsafe void UnhidePlayer(IPlayerCharacter player, bool allowVoided, bool isEmote)
    {
        if (player == null) return;

        var charStruct = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)player.Address;
        var invisibleFlags = FFXIVClientStructs.FFXIV.Client.Game.Object.VisibilityFlags.Model | 
                             FFXIVClientStructs.FFXIV.Client.Game.Object.VisibilityFlags.Nameplate;

        var config = GetVisibilityConfig();
        if (config == null)
        {
            if (charStruct != null && (charStruct->GameObject.RenderFlags & invisibleFlags) != 0)
            {
                charStruct->GameObject.RenderFlags &= ~invisibleFlags;
            }
            return;
        }

        if (!IsVisibilityHidingPlayers(config))
        {
            if (charStruct != null && (charStruct->GameObject.RenderFlags & invisibleFlags) != 0)
            {
                charStruct->GameObject.RenderFlags &= ~invisibleFlags;
            }
            ManipulateVisibilityCaches(player.GameObjectId, unhide: true);
            return;
        }

        var name = player.Name.ToString();
        var world = player.HomeWorld.Value.Name.ToString();
        var objectId = player.GameObjectId;
        var homeworldId = player.HomeWorld.RowId;
        var contentId = charStruct != null ? charStruct->ContentId : 0UL;

        try
        {
            // Check if they are already in our temporary state tracking
            if (TempUnhiddenPlayers.TryGetValue(objectId, out var state))
            {
                if (isEmote)
                {
                    state.EmoteExpireTime = DateTime.Now.AddSeconds(15);
                }
                else
                {
                    state.LastTargetedTime = DateTime.Now;
                }

                // Steady state: the IPC whitelist entry is sticky, so Visibility keeps the player
                // shown with no per-frame work from us. Only re-assert (cheaply) if Visibility has
                // actually re-hidden them - e.g. its runtime whitelist cache was cleared on a zone
                // change. Detected by a direct render-flag read; no reflection, no per-frame logging.
                if (charStruct != null && (charStruct->GameObject.RenderFlags & invisibleFlags) != 0)
                {
                    IpcAddToWhitelist(name, (uint)homeworldId, WhitelistReason);
                    charStruct->GameObject.RenderFlags &= ~invisibleFlags;
                }
                return;
            }

            // Look up in Visibility plugin's VoidList (stored in config) to honor the
            // "don't unhide voided players" option before whitelisting them.
            var voidListProp = config.GetType().GetProperty("VoidList");
            if (voidListProp == null) return;

            var voidList = voidListProp.GetValue(config) as System.Collections.IList;

            if (voidList == null) return;

            // Check if player is voided/blocked
            object? matchingVoidItem = null;
            foreach (var item in voidList)
            {
                if (item == null) continue;
                var itemIdProp = (MemberInfo?)item.GetType().GetProperty("Id") ?? item.GetType().GetField("Id");
                var itemNameProp = (MemberInfo?)item.GetType().GetProperty("Name") ?? item.GetType().GetField("Name");

                if (itemIdProp != null)
                {
                    var itemId = (ulong)GetMemberValue(item, itemIdProp)!;
                    if (itemId == contentId || (contentId == 0 && itemId == objectId))
                    {
                        matchingVoidItem = item;
                        break;
                    }
                }

                if (itemNameProp != null)
                {
                    var itemName = GetMemberValue(item, itemNameProp) as string;
                    if (itemName != null && itemName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingVoidItem = item;
                        break;
                    }
                }
            }

            bool wasVoided = matchingVoidItem != null;

            if (wasVoided && !allowVoided)
            {
                // Player is on VoidList, but we are NOT allowed to unhide voided players.
                return;
            }

            // We only reach here when Visibility is enabled and actively hiding players
            // (the disabled / HidePlayer-off cases already returned above). A detected looker
            // or target must therefore be whitelisted and tracked unconditionally so that
            // Visibility's per-frame whitelist check keeps them shown. Gating this on the
            // hidden flag being set on *this* frame was racy: if Cordi processed the player
            // before Visibility hid them, it returned without tracking and they stayed hidden.

            // Whitelist via Visibility's official IPC. This is the only path that reliably keeps
            // the player shown: AddToWhitelist adds the entry AND internally calls RemoveChecked +
            // ShowPlayer, so Visibility's own per-frame check stops re-hiding them. The previous
            // reflection re-implementation raced Visibility and lost (the player just flickered).
            if (!IpcAddToWhitelist(name, (uint)homeworldId, WhitelistReason))
            {
                // Visibility/IPC unavailable - nothing we can do reliably; don't track a player
                // we can't actually keep visible.
                return;
            }

            TempUnhiddenPlayers[objectId] = new TempUnhideState
            {
                Name = name,
                World = world,
                HomeworldId = (uint)homeworldId,
                ObjectId = objectId,
                ContentId = contentId,
                LastTargetedTime = isEmote ? null : DateTime.Now,
                EmoteExpireTime = isEmote ? DateTime.Now.AddSeconds(15) : null
            };

            Log.Info($"[VisibilityBridge] Temporarily whitelisting {name}@{world} (ObjectId: {objectId:X}) via Visibility IPC");

            // Clear the invisible flags this frame too, so targeting/focus works immediately,
            // before Visibility's own update runs.
            if (charStruct != null)
            {
                charStruct->GameObject.RenderFlags &= ~invisibleFlags;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[VisibilityBridge] Error unhiding player {name}@{world}: {ex}");
        }
    }

    public static unsafe void OnFrameworkUpdate()
    {
        // 1. Guard check: if Visibility integration is disabled in Peeper config, restore any unhidden players and return.
        if (!CordiPlugin.Plugin.Config.CordiPeep.UnhideFromVisibility)
        {
            if (!TempUnhiddenPlayers.IsEmpty)
            {
                foreach (var id in TempUnhiddenPlayers.Keys.ToList())
                {
                    RestorePlayerHiddenState(id);
                }
            }
            return;
        }

        var myTarget = Service.TargetManager.Target as IPlayerCharacter;
        if (myTarget != null)
        {
            UnhidePlayer(myTarget, allowVoided: true, isEmote: false);
        }

        var myFocus = Service.TargetManager.FocusTarget as IPlayerCharacter;
        if (myFocus != null)
        {
            UnhidePlayer(myFocus, allowVoided: true, isEmote: false);
        }

        var config = GetVisibilityConfig();
        var invisibleFlags = FFXIVClientStructs.FFXIV.Client.Game.Object.VisibilityFlags.Model |
                             FFXIVClientStructs.FFXIV.Client.Game.Object.VisibilityFlags.Nameplate;
        bool isVisibilityDisabled = config != null && !IsVisibilityHidingPlayers(config);

        // When Visibility is disabled/unchecked, release ONLY the players Cordi itself temporarily
        // unhid. We must never touch the whole ObjectTable here: doing so pushes every player into
        // Visibility's whitelist caches and forces everyone visible regardless of the user's config.
        if (config != null && isVisibilityDisabled && !TempUnhiddenPlayers.IsEmpty)
        {
            try
            {
                foreach (var obj in Service.ObjectTable)
                {
                    if (obj is IPlayerCharacter pc && TempUnhiddenPlayers.ContainsKey(pc.GameObjectId))
                    {
                        var charStruct = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)pc.Address;
                        if (charStruct != null && (charStruct->GameObject.RenderFlags & invisibleFlags) != 0)
                        {
                            charStruct->GameObject.RenderFlags &= ~invisibleFlags;
                        }
                    }
                }

                // Stop tracking everyone; Visibility is no longer hiding, so there is nothing to restore.
                TempUnhiddenPlayers.Clear();
            }
            catch (Exception ex)
            {
                Log.Error($"[VisibilityBridge] Error in disabled Visibility cleanup: {ex}");
            }
        }

        if (TempUnhiddenPlayers.IsEmpty) return;
        if (config == null) return;

        try
        {
            var now = DateTime.Now;
            var toRestore = new List<ulong>();

            foreach (var state in TempUnhiddenPlayers.Values)
            {
                bool stillLooking = false;
                
                // For lookers: check if they are still actively targeting us
                if (state.LastTargetedTime.HasValue)
                {
                    // If they targeted us in the last 2 seconds, consider them still looking
                    if ((now - state.LastTargetedTime.Value).TotalSeconds < 2.0)
                    {
                        stillLooking = true;
                    }
                }

                bool stillEmoteActive = false;
                if (state.EmoteExpireTime.HasValue)
                {
                    if (now < state.EmoteExpireTime.Value)
                    {
                        stillEmoteActive = true;
                    }
                }

                // If WE target or focus them, keep them visible!
                if ((myTarget != null && myTarget.GameObjectId == state.ObjectId) ||
                    (myFocus != null && myFocus.GameObjectId == state.ObjectId))
                {
                    stillLooking = true;
                    if (state.LastTargetedTime.HasValue)
                    {
                        state.LastTargetedTime = now;
                    }
                }

                if (!stillLooking && !stillEmoteActive)
                {
                    toRestore.Add(state.ObjectId);
                }
            }

            foreach (var id in toRestore)
            {
                RestorePlayerHiddenState(id);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[VisibilityBridge] Error in OnFrameworkUpdate: {ex}");
        }
    }

    public static void RestorePlayerHiddenState(ulong id)
    {
        if (!TempUnhiddenPlayers.TryRemove(id, out var state)) return;

        try
        {
            Log.Info($"[VisibilityBridge] Restoring hidden state for {state.Name}@{state.World}");

            // Remove our temporary whitelist entry via the official IPC. RemoveFromWhitelist only
            // drops the config entry though - it does NOT re-hide. We additionally invalidate
            // Visibility's runtime "checked" cache so its next frame re-evaluates this player and
            // hides them again (since they're a normally-hidden player no longer whitelisted).
            IpcRemoveFromWhitelist(state.Name, state.HomeworldId);
            ClearVisibilityCache(id);
        }
        catch (Exception ex)
        {
            Log.Error($"[VisibilityBridge] Error in RestorePlayerHiddenState for {id:X}: {ex}");
        }
    }

    private static void ManipulateVisibilityCaches(ulong objectId, bool unhide)
    {
        try
        {
            var pluginInstance = GetVisibilityPluginInstance();
            if (pluginInstance == null) return;

            var frameworkHandlerField = _pluginType!.GetField("frameworkHandler", BindingFlags.NonPublic | BindingFlags.Instance)
                                     ?? _pluginType.GetField("frameworkHandler", BindingFlags.Public | BindingFlags.Instance);
            if (frameworkHandlerField == null) return;

            var frameworkHandler = frameworkHandlerField.GetValue(pluginInstance);
            if (frameworkHandler == null) return;

            var voidListManagerField = frameworkHandler.GetType().GetField("voidListManager", BindingFlags.NonPublic | BindingFlags.Instance)
                                    ?? frameworkHandler.GetType().GetField("voidListManager", BindingFlags.Public | BindingFlags.Instance);
            if (voidListManagerField == null) return;

            var voidListManager = voidListManagerField.GetValue(frameworkHandler);
            if (voidListManager == null) return;

            var checkedVoidedObjectIdsField = voidListManager.GetType().GetField("checkedVoidedObjectIds", BindingFlags.NonPublic | BindingFlags.Instance);
            var checkedWhitelistedObjectIdsField = voidListManager.GetType().GetField("checkedWhitelistedObjectIds", BindingFlags.NonPublic | BindingFlags.Instance);
            var voidedObjectIdsField = voidListManager.GetType().GetField("voidedObjectIds", BindingFlags.NonPublic | BindingFlags.Instance);
            var whitelistedObjectIdsField = voidListManager.GetType().GetField("whitelistedObjectIds", BindingFlags.NonPublic | BindingFlags.Instance);

            var checkedVoided = checkedVoidedObjectIdsField?.GetValue(voidListManager) as System.Collections.IDictionary;
            var checkedWhitelisted = checkedWhitelistedObjectIdsField?.GetValue(voidListManager) as System.Collections.IDictionary;
            var voided = voidedObjectIdsField?.GetValue(voidListManager) as System.Collections.IDictionary;
            var whitelisted = whitelistedObjectIdsField?.GetValue(voidListManager) as System.Collections.IDictionary;

            uint id = (uint)objectId;
            long ticks = Environment.TickCount64;

            if (unhide)
            {
                if (checkedVoided != null) checkedVoided[id] = ticks;
                if (voided != null && voided.Contains(id)) voided.Remove(id);

                if (checkedWhitelisted != null) checkedWhitelisted[id] = ticks;
                if (whitelisted != null) whitelisted[id] = ticks;
            }
            else
            {
                if (checkedVoided != null && checkedVoided.Contains(id)) checkedVoided.Remove(id);
                if (voided != null && voided.Contains(id)) voided.Remove(id);

                if (checkedWhitelisted != null && checkedWhitelisted.Contains(id)) checkedWhitelisted.Remove(id);
                if (whitelisted != null && whitelisted.Contains(id)) whitelisted.Remove(id);
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"[VisibilityBridge] Failed to manipulate caches: {ex.Message}");
        }
    }

    public static void InspectPluginInterface()
    {
        try
        {
            var pi = Service.PluginInterface;
            if (pi == null) return;
            Log.Info($"=== Inspecting PluginInterface Type: {pi.GetType().FullName} ===");
            
            var installedPluginsProp = pi.GetType().GetProperty("InstalledPlugins", BindingFlags.Public | BindingFlags.Instance);
            if (installedPluginsProp != null)
            {
                var installedPlugins = installedPluginsProp.GetValue(pi) as System.Collections.IEnumerable;
                if (installedPlugins != null)
                {
                    Log.Info("=== InstalledPlugins Wrapper Details ===");
                    foreach (var wrapper in installedPlugins)
                    {
                        if (wrapper == null) continue;
                        var wrapperType = wrapper.GetType();
                        Log.Info($"Plugin Wrapper Type: {wrapperType.FullName}");
                        
                        foreach (var prop in wrapperType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            try
                            {
                                var val = prop.GetValue(wrapper);
                                if (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string) || prop.PropertyType.IsEnum)
                                {
                                    Log.Info($"  Property: {prop.Name} ({prop.PropertyType.Name}) = {val ?? "null"}");
                                }
                                else if (val != null)
                                {
                                    Log.Info($"  Property: {prop.Name} ({prop.PropertyType.FullName}) = {val.GetType().FullName}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Info($"  Property: {prop.Name} [Error: {ex.Message}]");
                            }
                        }
                        foreach (var field in wrapperType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            try
                            {
                                var val = field.GetValue(wrapper);
                                if (field.FieldType.IsPrimitive || field.FieldType == typeof(string) || field.FieldType.IsEnum)
                                {
                                    Log.Info($"  Field: {field.Name} ({field.FieldType.Name}) = {val ?? "null"}");
                                }
                                else if (val != null)
                                {
                                    Log.Info($"  Field: {field.Name} ({field.FieldType.FullName}) = {val.GetType().FullName}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Info($"  Field: {field.Name} [Error: {ex.Message}]");
                            }
                        }
                    }
                    Log.Info("=========================================");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error inspecting PluginInterface: {ex}");
        }
    }

    public static void DumpDebugInfo()
    {
        InspectPluginInterface();

        var config = GetVisibilityConfig();
        if (config == null)
        {
            Log.Info("[VisibilityBridge] Visibility plugin config not found or not loaded.");
            return;
        }

        try
        {
            var voidListProp = config.GetType().GetProperty("VoidList");
            var whitelistProp = config.GetType().GetProperty("Whitelist");
            var voidDictField = config.GetType().GetField("VoidDictionary");
            var whitelistDictField = config.GetType().GetField("WhitelistDictionary");

            Log.Info("=== Visibility Plugin Debug Info ===");
            if (voidListProp != null)
            {
                var list = voidListProp.GetValue(config) as System.Collections.IList;
                Log.Info($"- VoidList count: {list?.Count ?? 0}");
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        if (item == null) continue;
                        var name = item.GetType().GetProperty("Name")?.GetValue(item) ?? item.GetType().GetField("Name")?.GetValue(item);
                        var id = item.GetType().GetProperty("Id")?.GetValue(item) ?? item.GetType().GetField("Id")?.GetValue(item);
                        var objId = item.GetType().GetProperty("ObjectId")?.GetValue(item) ?? item.GetType().GetField("ObjectId")?.GetValue(item);
                        Log.Info($"  Voided: {name} (Id: {id:X}, ObjectId: {objId:X})");
                    }
                }
            }

            if (whitelistProp != null)
            {
                var list = whitelistProp.GetValue(config) as System.Collections.IList;
                Log.Info($"- Whitelist count: {list?.Count ?? 0}");
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        if (item == null) continue;
                        var name = item.GetType().GetProperty("Name")?.GetValue(item) ?? item.GetType().GetField("Name")?.GetValue(item);
                        var id = item.GetType().GetProperty("Id")?.GetValue(item) ?? item.GetType().GetField("Id")?.GetValue(item);
                        var objId = item.GetType().GetProperty("ObjectId")?.GetValue(item) ?? item.GetType().GetField("ObjectId")?.GetValue(item);
                        Log.Info($"  Whitelisted: {name} (Id: {id:X}, ObjectId: {objId:X})");
                    }
                }
            }

            if (voidDictField != null)
            {
                var dict = voidDictField.GetValue(config) as System.Collections.IDictionary;
                Log.Info($"- VoidDictionary count: {dict?.Count ?? 0}");
                if (dict != null)
                {
                    foreach (System.Collections.DictionaryEntry entry in dict)
                    {
                        Log.Info($"  Dict Key: {entry.Key:X}");
                    }
                }
            }

            if (whitelistDictField != null)
            {
                var dict = whitelistDictField.GetValue(config) as System.Collections.IDictionary;
                Log.Info($"- WhitelistDictionary count: {dict?.Count ?? 0}");
                if (dict != null)
                {
                    foreach (System.Collections.DictionaryEntry entry in dict)
                    {
                        Log.Info($"  Dict Key: {entry.Key:X}");
                    }
                }
            }
            Log.Info("====================================");
        }
        catch (Exception ex)
        {
            Log.Error($"[VisibilityBridge] Error dumping debug info: {ex}");
        }
    }
}
