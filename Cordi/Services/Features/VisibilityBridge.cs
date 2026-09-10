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

    private class TempUnhideState
    {
        public string Name = string.Empty;
        public string World = string.Empty;
        public uint HomeworldId;
        public ulong ObjectId;
        public ulong ContentId;
        public bool WasVoided;
        public DateTime? LastTargetedTime;
        public DateTime? EmoteExpireTime;
    }

    private static readonly ConcurrentDictionary<ulong, TempUnhideState> TempUnhiddenPlayers = new();

    private const string WhitelistReason = "Cordi Peeper";
    private const string VisibilityInternalName = "Visibility";

    private static bool _visibilityInstalled;
    private static DateTime _lastInstalledCheckTime = DateTime.MinValue;

    public static bool IsVisibilityInstalled()
    {
        var now = DateTime.Now;
        if ((now - _lastInstalledCheckTime).TotalSeconds < 5.0) return _visibilityInstalled;
        _lastInstalledCheckTime = now;

        var installed = false;

        try
        {
            foreach (var plugin in Service.PluginInterface.InstalledPlugins)
            {
                if (!plugin.IsLoaded) continue;
                if (!string.Equals(plugin.InternalName, VisibilityInternalName, StringComparison.OrdinalIgnoreCase)) continue;

                installed = true;
                break;
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"[VisibilityBridge] Failed to enumerate installed plugins: {ex.Message}");
        }

        if (!installed && _visibilityInstalled)
        {
            _visibilityPluginInstance = null;
            _visibilityAssembly = null;
            _pluginType = null;
            _ipcAddToWhitelist = null;
            _ipcRemoveFromWhitelist = null;
            _lastInstanceCheckTime = DateTime.MinValue;
            ResetReflectionCaches();
            TempUnhiddenPlayers.Clear();
        }

        _visibilityInstalled = installed;
        return installed;
    }

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

    private static object? GetMemberValue(object? obj, MemberInfo? member)
    {
        if (member is PropertyInfo prop) return prop.GetValue(obj);
        if (member is FieldInfo field) return field.GetValue(obj);
        return null;
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
                                Log.Debug($"[VisibilityBridge] Found plugin instance in Visibility static field: {type.FullName}.{field.Name}");
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
                                Log.Debug($"[VisibilityBridge] Found plugin instance in Visibility static property: {type.FullName}.{prop.Name}");
                                return val;
                            }
                        }
                        catch {}
                    }
                }
            }

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

                            var res = CrawlObject(wrapper, pluginTypeName, visited, 0);
                            if (res != null)
                            {
                                Log.Debug($"[VisibilityBridge] Found plugin instance by crawling wrapper {wrapperType.FullName}");
                                return res;
                            }
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Debug($"[VisibilityBridge] Error in FindPluginInstance: {ex.Message}");
            return null;
        }
    }

    private static object? CrawlObject(object obj, string targetTypeName, HashSet<object> visited, int depth)
    {
        if (obj == null || depth > 5) return null;
        if (!visited.Add(obj)) return null;

        var type = obj.GetType();
        if (type.FullName == targetTypeName) return obj;

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
        if (!IsVisibilityInstalled()) return null;
        if (_visibilityPluginInstance != null) return _visibilityPluginInstance;

        var now = DateTime.Now;
        if ((now - _lastInstanceCheckTime).TotalSeconds < 5.0) return null;
        _lastInstanceCheckTime = now;

        try
        {
            if (_visibilityAssembly == null)
            {
                _visibilityAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == VisibilityInternalName);

                if (_visibilityAssembly == null)
                {
                    Log.Debug("[VisibilityBridge] Visibility assembly not found in AppDomain.");
                    return null;
                }
            }

            if (_pluginType == null)
            {
                _pluginType = _visibilityAssembly.GetType("Visibility.VisibilityPlugin")
                           ?? _visibilityAssembly.GetType("Visibility.Plugin");
                if (_pluginType == null)
                {
                    Log.Debug($"[VisibilityBridge] VisibilityPlugin type not found in assembly {_visibilityAssembly.FullName}");
                    return null;
                }
            }

            var instance = FindPluginInstance(_pluginType.FullName!);
            if (instance != null)
            {
                _visibilityPluginInstance = instance;

                _pluginType = instance.GetType();
                _visibilityAssembly = _pluginType.Assembly;
                ResetReflectionCaches();
            }
            else
            {
                Log.Debug($"[VisibilityBridge] FindPluginInstance returned null for {_pluginType.FullName}");
            }
            return instance;
        }
        catch (Exception ex)
        {
            Log.Debug($"[VisibilityBridge] Failed to get plugin instance: {ex.Message}");
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
            if ((DateTime.Now - _lastConfigErrorLogTime).TotalSeconds > 10.0)
            {
                _lastConfigErrorLogTime = DateTime.Now;
                Log.Debug($"[VisibilityBridge] Failed to get config instance: {ex.Message}");
            }
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

    private static void ResetReflectionCaches()
    {
        _configMember = null;
        _enabledMember = null;
        _currentConfigMember = null;
        _hidePlayerMember = null;
        _disabledMembersResolved = false;
    }

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
            return true;
        }
    }

    private static DateTime _lastHidingDecisionLogTime = DateTime.MinValue;

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
        if (!IsVisibilityInstalled()) return;

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

                if (charStruct != null && (charStruct->GameObject.RenderFlags & invisibleFlags) != 0)
                {
                    IpcAddToWhitelist(name, (uint)homeworldId, WhitelistReason);
                    if (state.WasVoided)
                    {
                        ManipulateVisibilityCaches(objectId, unhide: true);
                    }
                    charStruct->GameObject.RenderFlags &= ~invisibleFlags;
                }
                return;
            }

            var voidListProp = config.GetType().GetProperty("VoidList");
            if (voidListProp == null) return;

            var voidList = voidListProp.GetValue(config) as System.Collections.IList;

            if (voidList == null) return;

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
                return;
            }

            if (!IpcAddToWhitelist(name, (uint)homeworldId, WhitelistReason))
            {
                return;
            }

            if (wasVoided)
            {
                ManipulateVisibilityCaches(objectId, unhide: true);
            }

            TempUnhiddenPlayers[objectId] = new TempUnhideState
            {
                Name = name,
                World = world,
                HomeworldId = (uint)homeworldId,
                ObjectId = objectId,
                ContentId = contentId,
                WasVoided = wasVoided,
                LastTargetedTime = isEmote ? null : DateTime.Now,
                EmoteExpireTime = isEmote ? DateTime.Now.AddSeconds(15) : null
            };

            Log.Info($"[VisibilityBridge] Temporarily {(wasVoided ? "un-voiding + whitelisting" : "whitelisting")} {name}@{world} (ObjectId: {objectId:X}) via Visibility IPC");

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
        if (!CordiPlugin.Plugin.Config.CordiPeep.UnhideFromVisibilityEffective || !IsVisibilityInstalled())
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

        bool allowVoided = CordiPlugin.Plugin.Config.CordiPeep.UnhideVoidedPlayers;

        var myTarget = Service.TargetManager.Target as IPlayerCharacter;
        if (myTarget != null)
        {
            UnhidePlayer(myTarget, allowVoided, isEmote: false);
        }

        var myFocus = Service.TargetManager.FocusTarget as IPlayerCharacter;
        if (myFocus != null)
        {
            UnhidePlayer(myFocus, allowVoided, isEmote: false);
        }

        var config = GetVisibilityConfig();
        var invisibleFlags = FFXIVClientStructs.FFXIV.Client.Game.Object.VisibilityFlags.Model |
                             FFXIVClientStructs.FFXIV.Client.Game.Object.VisibilityFlags.Nameplate;
        bool isVisibilityDisabled = config != null && !IsVisibilityHidingPlayers(config);

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
                
                if (state.LastTargetedTime.HasValue)
                {
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
        if (!IsVisibilityInstalled()) return;

        try
        {
            Log.Info($"[VisibilityBridge] Restoring hidden state for {state.Name}@{state.World}");

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
        if (!IsVisibilityInstalled())
        {
            Log.Info("[VisibilityBridge] Visibility plugin is not installed or not loaded.");
            return;
        }

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
