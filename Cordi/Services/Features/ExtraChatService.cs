using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Cordi.Core;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Cordi.Configuration.External;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Cordi.Services.Features
{
    public class ExtraChatService
    {
        private readonly Configuration.Configuration _config;

        private DateTime _lastSyncTime = DateTime.MinValue;

        public ExtraChatService(Configuration.Configuration config)
        {
            _config = config;
        }

        private string GetExtraChatConfigPath()
        {
            var configDir = CordiPlugin.PluginInterface.GetPluginConfigDirectory();
            var parentDir = Directory.GetParent(configDir)?.FullName;
            if (parentDir != null)
            {
                var targetPath = Path.Combine(parentDir, "ExtraChat.json");
                if (File.Exists(targetPath)) return targetPath;
            }
            var directPath = Path.Combine(configDir, "ExtraChat.json");
            if (File.Exists(directPath)) return directPath;

            return "";
        }

        public static bool IsLoaded() =>
            CordiPlugin.PluginInterface.InstalledPlugins.Any(p => p.InternalName == "ExtraChat" && p.IsLoaded);

        public bool IsExtraChatInstalled()
        {
#if DEBUG
            var isLoaded = true;
#else
            var isLoaded = IsLoaded();
#endif
            return isLoaded && !string.IsNullOrEmpty(GetExtraChatConfigPath());
        }

        public int SyncFromExtraChat()
        {
            var path = GetExtraChatConfigPath();
            if (string.IsNullOrEmpty(path)) return 0;

            try
            {
                var json = File.ReadAllText(path);
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    NullValueHandling = NullValueHandling.Ignore
                };

                var root = JsonConvert.DeserializeObject<ExtraChatRoot>(json, settings);
                if (root?.Configs == null) return 0;

                ulong contentId;
                unsafe
                {
                    var playerState = PlayerState.Instance();
                    contentId = playerState != null ? playerState->ContentId : 0UL;
                }
                if (contentId == 0) return 0;
                if (!root.Configs.TryGetValue(contentId, out var charConfig))
                {
                    return 0;
                }

                if (charConfig.ChannelOrder == null) return 0;

                int changes = 0;
                var currentMappings = _config.Chat.ExtraChatMappings;

                foreach (var kvp in charConfig.ChannelOrder)
                {
                    int orderIndex = kvp.Key;
                    Guid channelGuid = kvp.Value;

                    int extraChatNum = orderIndex + 1;
                    Service.Log.Info($"ExtraChatNum: {extraChatNum}");
                    if (extraChatNum < 1) extraChatNum = 1;

                    string label = "";
                    if (charConfig.Channels != null && charConfig.Channels.TryGetValue(channelGuid, out var chInfo))
                    {
                        label = chInfo.Name ?? "";
                    }

                    if (charConfig.ChannelMarkers != null && charConfig.ChannelMarkers.TryGetValue(channelGuid, out var marker))
                    {
                        if (!string.IsNullOrEmpty(marker)) label = marker;
                    }

                    if (string.IsNullOrEmpty(label)) continue;

                    var guidStr = channelGuid.ToString();
                    var existingByGuid = currentMappings.FirstOrDefault(m => m.Value?.ExtraChatGuid == guidStr);

                    if (existingByGuid.Value != null)
                    {
                        var connection = existingByGuid.Value;

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
                        connectionByLabel.ExtraChatGuid = guidStr;
                        if (connectionByLabel.ExtraChatNumber != extraChatNum)
                        {
                            connectionByLabel.ExtraChatNumber = extraChatNum;
                            changes++;
                        }
                        else
                        {
                            changes++;
                        }
                    }
                    else
                    {
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
                    _config.Save();
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
