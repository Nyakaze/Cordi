using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Cordi.Core;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Cordi.Configuration.External;

namespace Cordi.Services.Features
{
    public class ExtraChatService
    {
        private readonly CordiPlugin _plugin;

        private DateTime _lastSyncTime = DateTime.MinValue;

        public ExtraChatService(CordiPlugin plugin)
        {
            _plugin = plugin;
        }

        private string GetExtraChatConfigPath()
        {
            // Assuming ExtraChat.json is in the same pluginConfigs folder as Cordi's config
            // C:\Users\User\AppData\Roaming\XivLauncher\pluginConfigs\ExtraChat.json
            var configDir = CordiPlugin.PluginInterface.GetPluginConfigDirectory();
            // Up one level to pluginConfigs if Cordi is in a subdir, or same dir?
            // User moved config to pluginConfigs/Cordi, so up one level to pluginConfigs root.
            var parentDir = Directory.GetParent(configDir)?.FullName;
            if (parentDir != null)
            {
                var targetPath = Path.Combine(parentDir, "ExtraChat.json");
                if (File.Exists(targetPath)) return targetPath;
            }
            // Fallback: Check if configDir IS pluginConfigs (if not moved yet or structure differs)
            var directPath = Path.Combine(configDir, "ExtraChat.json");
            if (File.Exists(directPath)) return directPath;

            return "";
        }

        public bool IsExtraChatInstalled()
        {
            // Check if the plugin is actually loaded in Dalamud
            var isLoaded = CordiPlugin.PluginInterface.InstalledPlugins
                .Any(p => p.InternalName == "ExtraChat" && p.IsLoaded);

            // Also verify config exists for safety
            return isLoaded && !string.IsNullOrEmpty(GetExtraChatConfigPath());
        }

        public int SyncFromExtraChat()
        {
            var path = GetExtraChatConfigPath();
            if (string.IsNullOrEmpty(path)) return 0;

            try
            {
                var json = File.ReadAllText(path);
                // ExtraChat uses "$type" metadata which JSON.NET handles automatically if TypeNameHandling is Auto,
                // but we defined custom POCOs for mapping, so default deserialization should ignore unknown properties.
                // However, the root object structure in the user sample is:
                // { "$type": ..., "Configs": { ... } }

                // We need to be careful with type handling if the types in JSON don't match our assembly.
                // Safer to use JObject or robust settings.
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None, // Ignore $type metadata to use our POCOs source
                    NullValueHandling = NullValueHandling.Ignore
                };

                // The root object has "Configs" property which is a Dictionary<ulong, ConfigInfo>
                // We'll deserialize to JObject first to navigate easier or try direct mapping.

                var root = JsonConvert.DeserializeObject<ExtraChatRoot>(json, settings);
                if (root?.Configs == null) return 0;

                // Find current character config
                ulong contentId = CordiPlugin.ClientState.LocalContentId;
                if (contentId == 0) return 0; // Not logged in

                if (!root.Configs.TryGetValue(contentId, out var charConfig))
                {
                    // Fallback: Check if there's a default or match by name? Usually CID based.
                    return 0;
                }

                if (charConfig.ChannelOrder == null) return 0;

                int changes = 0;
                var currentMappings = _plugin.Config.Chat.ExtraChatMappings;

                foreach (var kvp in charConfig.ChannelOrder)
                {
                    // key is index, value is GUID
                    int orderIndex = kvp.Key;
                    Guid channelGuid = kvp.Value;

                    // Formula: (Key + 1) / 2
                    // Example: 3 -> (3+1)/2 = 2. 5 -> (5+1)/2 = 3. 9 -> (9+1)/2 = 5.
                    int extraChatNum = orderIndex + 1;
                    Service.Log.Info($"ExtraChatNum: {extraChatNum}");
                    if (extraChatNum < 1) extraChatNum = 1;

                    // Get Channel Info
                    string label = "";
                    if (charConfig.Channels != null && charConfig.Channels.TryGetValue(channelGuid, out var chInfo))
                    {
                        label = chInfo.Name ?? "";
                    }

                    // Check for custom Marker (Label Override)
                    if (charConfig.ChannelMarkers != null && charConfig.ChannelMarkers.TryGetValue(channelGuid, out var marker))
                    {
                        if (!string.IsNullOrEmpty(marker)) label = marker;
                    }

                    if (string.IsNullOrEmpty(label)) continue;

                    // Update or Add to Cordi Mappings
                    // Strategy: Find by GUID first to handle renames
                    var guidStr = channelGuid.ToString();
                    var existingByGuid = currentMappings.FirstOrDefault(m => m.Value?.ExtraChatGuid == guidStr);

                    if (existingByGuid.Value != null) // Check if a mapping with this GUID was found
                    {
                        var connection = existingByGuid.Value;

                        // If label changed, we must rename the key
                        if (existingByGuid.Key != label)
                        {
                            currentMappings.Remove(existingByGuid.Key);
                            currentMappings[label] = connection;
                            changes++;
                        }

                        if (connection.ExtraChatNumber != extraChatNum)
                        {
                            connection.ExtraChatNumber = extraChatNum;
                            changes++;
                        }
                    }
                    else if (currentMappings.TryGetValue(label, out var connectionByLabel))
                    {
                        // Found by label but no GUID stored yet? Update GUID and Number
                        connectionByLabel.ExtraChatGuid = guidStr;
                        if (connectionByLabel.ExtraChatNumber != extraChatNum)
                        {
                            connectionByLabel.ExtraChatNumber = extraChatNum;
                            changes++;
                        }
                        else
                        {
                            // Just GUID update doesn't count as a "change" worth notifying user about usually,
                            // but we'll count it to ensure Save() is called.
                            changes++;
                        }
                    }
                    else
                    {
                        // Brand new mapping
                        currentMappings[label] = new Configuration.ExtraChatConnection
                        {
                            ExtraChatNumber = extraChatNum,
                            ExtraChatGuid = guidStr
                        };
                        changes++;
                    }
                }

                if (changes > 0)
                {
                    _plugin.Config.Save();
                }

                _lastSyncTime = DateTime.Now;
                return changes;
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "Failed to sync ExtraChat config");
                return 0;
            }
        }
    }
}
