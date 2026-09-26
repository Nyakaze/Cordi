using Dalamud.Configuration;
using System;
using Dalamud.Plugin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Dalamud.Game.Text;
using System.IO;
using System.Threading;

namespace Cordi.Configuration;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;


    public PeeperConfig Peeper { get; set; } = new();
    public EmoteLogConfig EmoteLog { get; set; } = new();
    public DiscordConfig Discord { get; set; } = new();
    public ChatConfig Chat { get; set; } = new();
    public ChatboxConfig Chatbox { get; set; } = new();
    public TranslationConfig Translation { get; set; } = new();
    public DiscordActivityConfig ActivityConfig { get; set; } = new();
    public PartyConfig Party { get; set; } = new();
    public RememberMeConfig RememberMe { get; set; } = new();
    public AdvertisementFilterConfig AdvertisementFilter { get; set; } = new();
    public LodestoneConfig Lodestone { get; set; } = new();
    public CombinedWindowConfig CombinedWindow { get; set; } = new();
    public FontConfig Font { get; set; } = new();
    public AppearanceConfig Appearance { get; set; } = new();
    public AudioConfig Audio { get; set; } = new();
    public ThroughputStats Stats { get; set; } = new();
    public SlashCommandConfig SlashCommands { get; set; } = new();
    public bool LogsTabVisible { get; set; }


    private const int CurrentVersion = 3;
    private const int SaveRetryAttempts = 5;
    private const int SaveRetryDelayMs = 25;

    [JsonIgnore] private static readonly object SaveGate = new();
    [JsonIgnore] private IDalamudPluginInterface pluginInterface = null!;
    [JsonIgnore] public string ConfigFilePath => Path.Combine(pluginInterface.ConfigDirectory.FullName, "Config.json");
    [JsonExtensionData] private IDictionary<string, JToken>? _additionalData;

    [JsonIgnore] public Dictionary<XivChatType, string> MappingCache { get; private set; } = new();

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        MigrateConfig();
        MigrateAudioDevice();
        MigrateCombinedLayout();
        MigrateRowLayouts();
        MigrateChannelSendTypes();
        MigrateVersion();
        Translation.Normalize();
        BuildCache();

        AdvertisementFilter.InitializeDefaults();
    }

    private void MigrateConfig()
    {
        if (_additionalData == null || _additionalData.Count == 0) return;

        bool needsSave = false;


        if (_additionalData.TryGetValue("BotToken", out var botToken))
        {
            Discord.BotToken = botToken.ToObject<string>() ?? string.Empty;
            needsSave = true;
        }
        if (_additionalData.TryGetValue("BotStarted", out var botStarted))
        {
            Discord.BotStarted = botStarted.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("DefaultDiscordChannelId", out var defaultChan))
        {
            Discord.DefaultChannelId = defaultChan.ToObject<string>() ?? string.Empty;
            needsSave = true;
        }


        if (_additionalData.TryGetValue("Mappings", out var mappings))
        {
            Chat.Mappings = mappings.ToObject<List<ChannelMapping>>() ?? new();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("TellThreadMappings", out var tellMappings))
        {
            Chat.TellThreadMappings = tellMappings.ToObject<Dictionary<string, string>>() ?? new();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CustomAvatars", out var avatars))
        {
            Chat.CustomAvatars = avatars.ToObject<Dictionary<string, string>>() ?? new();
            needsSave = true;
        }


        if (_additionalData.TryGetValue("EmoteLogWindowLocked", out var elLocked))
        {
            EmoteLog.WindowLockPosition = elLocked.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("EmoteLogChannelId", out var elChan))
        {
            EmoteLog.ChannelId = elChan.ToObject<string>() ?? string.Empty;
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
            EmoteLog.LegacyShowReplyButton = elReply.ToObject<bool>();
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
            EmoteLog.Blacklist = elBlacklist.ToObject<List<EmoteLogBlacklistEntry>>() ?? new();
            needsSave = true;
        }


        if (_additionalData.TryGetValue("CordiPeepEnabled", out var cpEnabled))
        {
            Peeper.Enabled = cpEnabled.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepDiscordEnabled", out var cpDiscord))
        {
            Peeper.DiscordEnabled = cpDiscord.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepDetectWhenClosed", out var cpDetect))
        {
            Peeper.DetectWhenClosed = cpDetect.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepSoundPath", out var cpSoundPath))
        {
            Peeper.SoundPath = cpSoundPath.ToObject<string>() ?? string.Empty;
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepDiscordChannelId", out var cpChan))
        {
            Peeper.DiscordChannelId = cpChan.ToObject<string>() ?? string.Empty;
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepWindowEnabled", out var cpWinEnabled))
        {
            Peeper.WindowEnabled = cpWinEnabled.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("OpenCordiPeepOnLogin", out var cpOpenLogin))
        {
            Peeper.OpenOnLogin = cpOpenLogin.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepWindowLocked", out var cpWinLocked))
        {
            Peeper.WindowLocked = cpWinLocked.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepWindowNoResize", out var cpNoResize))
        {
            Peeper.WindowNoResize = cpNoResize.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepFocusOnHover", out var cpFocus))
        {
            Peeper.FocusOnHover = cpFocus.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepAltClickExamine", out var cpAlt))
        {
            Peeper.AltClickExamine = cpAlt.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepIncludeSelf", out var cpSelf))
        {
            Peeper.IncludeSelf = cpSelf.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepLogParty", out var cpParty))
        {
            Peeper.LogParty = cpParty.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepLogAlliance", out var cpAli))
        {
            Peeper.LogAlliance = cpAli.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepLogCombat", out var cpCombat))
        {
            Peeper.LogCombat = cpCombat.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepSoundEnabled", out var cpSoundEnabled))
        {
            Peeper.SoundEnabled = cpSoundEnabled.ToObject<bool>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepSoundVolume", out var cpVol))
        {
            Peeper.SoundVolume = cpVol.ToObject<float>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepSoundDevice", out var cpDevice))
        {
            Peeper.LegacySoundDevice = cpDevice.ToObject<Guid>();
            needsSave = true;
        }
        if (_additionalData.TryGetValue("CordiPeepBlacklist", out var cpBlacklist))
        {
            Peeper.Blacklist = cpBlacklist.ToObject<List<PeeperBlacklistEntry>>() ?? new();
            needsSave = true;
        }

        if (_additionalData.TryGetValue("CordiPeep", out var cpSection))
        {
            var legacy = cpSection.ToObject<PeeperConfig>();
            if (legacy != null) Peeper = legacy;
            needsSave = true;
        }

        if (needsSave)
        {
            _additionalData = null;
            Save();
        }
    }

    private void MigrateAudioDevice()
    {
        if (Peeper.LegacySoundDevice == Guid.Empty)
            return;

        if (Audio.OutputDevice == Guid.Empty)
            Audio.OutputDevice = Peeper.LegacySoundDevice;

        Peeper.LegacySoundDevice = Guid.Empty;
        Save();
    }

    private void MigrateCombinedLayout()
    {
        if (CombinedWindow.LegacySwapPanels is not { } swapped) return;

        CombinedWindow.EmoteLogSlot = swapped ? OverlaySlot.TopRight : OverlaySlot.TopLeft;
        CombinedWindow.PeeperSlot = swapped ? OverlaySlot.TopLeft : OverlaySlot.TopRight;
        CombinedWindow.Enabled = true;
        CombinedWindow.LegacySwapPanels = null;
        Save();
    }

    private void MigrateRowLayouts()
    {
        bool changed = Peeper.RowLayout.Fields.Count == 0 || EmoteLog.RowLayout.Fields.Count == 0;

        Peeper.RowLayout.Normalize(RowFieldCatalog.PeeperDefaults());
        EmoteLog.RowLayout.Normalize(RowFieldCatalog.EmoteLogDefaults());

        changed |= Apply(Peeper.RowLayout, RowFieldCatalog.Direction, Peeper.LegacyShowDirection, Peeper.LegacyShowDirectionInHistory);
        changed |= Apply(Peeper.RowLayout, RowFieldCatalog.Distance, Peeper.LegacyShowDistance, Peeper.LegacyShowDistanceInHistory);
        changed |= Apply(Peeper.RowLayout, RowFieldCatalog.Target, Peeper.LegacyShowCurrentTarget, null);
        changed |= Apply(EmoteLog.RowLayout, RowFieldCatalog.Reply, EmoteLog.LegacyShowReplyButton, null);

        Peeper.LegacyShowDirection = null;
        Peeper.LegacyShowDistance = null;
        Peeper.LegacyShowDirectionInHistory = null;
        Peeper.LegacyShowDistanceInHistory = null;
        Peeper.LegacyShowCurrentTarget = null;
        EmoteLog.LegacyShowReplyButton = null;

        if (changed) Save();

        static bool Apply(RowLayoutConfig layout, string key, bool? enabled, bool? inHistory)
        {
            if (enabled == null && inHistory == null) return false;

            var field = layout.Find(key);
            if (field == null) return false;

            if (enabled is { } on) field.Enabled = on;
            if (inHistory is { } history) field.InHistory = history;

            return true;
        }
    }

    private void MigrateVersion()
    {
        if (Version >= CurrentVersion) return;

        if (Version < 2)
            Translation.DetectionSource = TranslationDetectionSource.Local;

        if (Version < 3)
            AdvertisementFilter.RestoreDefaults();

        Version = CurrentVersion;
        Save();
    }

    private void MigrateChannelSendTypes()
    {
        var changed = false;

        foreach (var channel in Chatbox.Channels)
        {
            if (channel.SendGameChatType == XivChatType.None) continue;

            if (channel.SendGameChatTypes.Count == 0)
                channel.SendGameChatTypes.Add(channel.SendGameChatType);

            channel.SendGameChatType = XivChatType.None;
            changed = true;
        }

        if (changed) Save();
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
        lock (SaveGate)
        {
            BuildCache();

            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            var path = ConfigFilePath;
            var tempPath = path + ".tmp";

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    File.WriteAllText(tempPath, json);

                    if (File.Exists(path))
                        File.Replace(tempPath, path, null);
                    else
                        File.Move(tempPath, path);

                    return;
                }
                catch (IOException) when (attempt < SaveRetryAttempts)
                {
                    Thread.Sleep(SaveRetryDelayMs);
                }
            }
        }
    }
}
