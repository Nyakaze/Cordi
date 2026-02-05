using Dalamud.Configuration;
using System;
using Dalamud.Plugin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Dalamud.Game.Text;

namespace Cordi.Configuration;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;


    public CordiPeepConfig CordiPeep { get; set; } = new();
    public EmoteLogConfig EmoteLog { get; set; } = new();
    public DiscordConfig Discord { get; set; } = new();
    public ChatConfig Chat { get; set; } = new();
    public DiscordActivityConfig ActivityConfig { get; set; } = new();
    public PartyConfig Party { get; set; } = new();
    public RememberMeConfig RememberMe { get; set; } = new();
    public LodestoneConfig Lodestone { get; set; } = new();
    public ThroughputStats Stats { get; set; } = new();

    [JsonIgnore] private IDalamudPluginInterface pluginInterface;
    [JsonExtensionData] private IDictionary<string, JToken> _additionalData;

    [JsonIgnore] public Dictionary<XivChatType, string> MappingCache { get; private set; } = new();

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        MigrateConfig();
        BuildCache();
    }

    private void MigrateConfig()
    {
        if (_additionalData == null || _additionalData.Count == 0) return;

        bool needsSave = false;


        if (_additionalData.TryGetValue("BotToken", out var botToken))
        {
            Discord.BotToken = botToken.ToObject<string>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("BotStarted", out var botStarted))
        {
            Discord.BotStarted = botStarted.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("DefaultDiscordChannelId", out var defaultChan))
        {
            Discord.DefaultChannelId = defaultChan.ToObject<string>();
            needsSave = true;
        }


        if (_additionalData.TryGetValue("Mappings", out var mappings))
        {
            Chat.Mappings = mappings.ToObject<List<ChannelMapping>>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("TellThreadMappings", out var tellMappings))
        {
            Chat.TellThreadMappings = tellMappings.ToObject<Dictionary<string, string>>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CustomAvatars", out var avatars))
        {
            Chat.CustomAvatars = avatars.ToObject<Dictionary<string, string>>();
            needsSave = true;
        }


        if (_additionalData.TryGetValue("EmoteLogWindowLocked", out var elLocked))
        {
            EmoteLog.WindowLocked = elLocked.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("EmoteLogChannelId", out var elChan))
        {
            EmoteLog.ChannelId = elChan.ToObject<string>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("EmoteLogEnabled", out var elEnabled))
        {
            EmoteLog.Enabled = elEnabled.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("EmoteLogIncludeSelf", out var elSelf))
        {
            EmoteLog.IncludeSelf = elSelf.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("EmoteLogCollapseDuplicates", out var elCollapse))
        {
            EmoteLog.CollapseDuplicates = elCollapse.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("EmoteLogShowReplyButton", out var elReply))
        {
            EmoteLog.ShowReplyButton = elReply.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("EmoteLogDiscordEnabled", out var elDiscord))
        {
            EmoteLog.DiscordEnabled = elDiscord.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("EmoteLogDetectWhenClosed", out var elDetect))
        {
            EmoteLog.DetectWhenClosed = elDetect.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("EmoteLogWindowEnabled", out var elWinEnabled))
        {
            EmoteLog.WindowEnabled = elWinEnabled.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("EmoteLogWindowOpenOnLogin", out var elOpenLogin))
        {
            EmoteLog.WindowOpenOnLogin = elOpenLogin.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("EmoteLogWindowLockPosition", out var elLockPos))
        {
            EmoteLog.WindowLockPosition = elLockPos.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("EmoteLogWindowLockSize", out var elLockSize))
        {
            EmoteLog.WindowLockSize = elLockSize.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("EmoteLogBlacklist", out var elBlacklist))
        {
            EmoteLog.Blacklist = elBlacklist.ToObject<List<EmoteLogBlacklistEntry>>();
            needsSave = true;
        }


        if (_additionalData.TryGetValue("CordiPeepEnabled", out var cpEnabled))
        {
            CordiPeep.Enabled = cpEnabled.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepDiscordEnabled", out var cpDiscord))
        {
            CordiPeep.DiscordEnabled = cpDiscord.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepDetectWhenClosed", out var cpDetect))
        {
            CordiPeep.DetectWhenClosed = cpDetect.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepSoundPath", out var cpSoundPath))
        {
            CordiPeep.SoundPath = cpSoundPath.ToObject<string>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepDiscordChannelId", out var cpChan))
        {
            CordiPeep.DiscordChannelId = cpChan.ToObject<string>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepWindowEnabled", out var cpWinEnabled))
        {
            CordiPeep.WindowEnabled = cpWinEnabled.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("OpenCordiPeepOnLogin", out var cpOpenLogin))
        {
            CordiPeep.OpenOnLogin = cpOpenLogin.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepWindowLocked", out var cpWinLocked))
        {
            CordiPeep.WindowLocked = cpWinLocked.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepWindowNoResize", out var cpNoResize))
        {
            CordiPeep.WindowNoResize = cpNoResize.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepFocusOnHover", out var cpFocus))
        {
            CordiPeep.FocusOnHover = cpFocus.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepAltClickExamine", out var cpAlt))
        {
            CordiPeep.AltClickExamine = cpAlt.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepIncludeSelf", out var cpSelf))
        {
            CordiPeep.IncludeSelf = cpSelf.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepLogParty", out var cpParty))
        {
            CordiPeep.LogParty = cpParty.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepLogAlliance", out var cpAli))
        {
            CordiPeep.LogAlliance = cpAli.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepLogCombat", out var cpCombat))
        {
            CordiPeep.LogCombat = cpCombat.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepSoundEnabled", out var cpSoundEnabled))
        {
            CordiPeep.SoundEnabled = cpSoundEnabled.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepSoundVolume", out var cpVol))
        {
            CordiPeep.SoundVolume = cpVol.ToObject<float>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepSoundDevice", out var cpDevice))
        {
            CordiPeep.SoundDevice = cpDevice.ToObject<Guid>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepBlacklist", out var cpBlacklist))
        {
            CordiPeep.Blacklist = cpBlacklist.ToObject<List<CordiPeepBlacklistEntry>>();
            needsSave = true;
        }

        if (needsSave)
        {
            _additionalData = null;
            Save();
        }
    }

    public void BuildCache()
    {
        MappingCache.Clear();
        foreach (var mapping in Chat.Mappings)
        {
            if (!MappingCache.ContainsKey(mapping.GameChatType))
            {
                MappingCache[mapping.GameChatType] = mapping.DiscordChannelId;
            }
        }
    }

    public void Save()
    {
        BuildCache();
        pluginInterface.SavePluginConfig(this);
    }
}
