using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Services.Features;
using Dalamud.Game.Text;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService : IDisposable
{
    private readonly CordiPlugin _plugin;
    private readonly Dictionary<string, ChatboxChannelState> _channels = new(StringComparer.Ordinal);
    private readonly ChatboxContentParser _parser;
    private readonly ReaderWriterLockSlim _channelLock = new(LockRecursionPolicy.SupportsRecursion);
    private long _sequence;
    private bool _disposed;

    public ChatboxService(CordiPlugin plugin)
    {
        _plugin = plugin;
        _parser = new ChatboxContentParser(() => plugin.Config.Chatbox);

        var configDirectory = CordiPlugin.PluginInterface.ConfigDirectory.FullName;
        ChatboxImageCache.RemoveLegacyDirectory(configDirectory);

        Database = new ChatboxDatabase(configDirectory);
        Store = new ChatboxMessageStore(Database, LimitFor);
        ImageCache = new ChatboxImageCache(
            Database,
            () => plugin.Config.Chatbox.ImageCacheMaxEntries,
            () => plugin.Config.Chatbox.AnimateGifs,
            () => plugin.Config.Chatbox.AnimateIdleUnloadSeconds);
        EmbedCache = new ChatboxEmbedCache(Database, () => plugin.Config.Chatbox.EmbedCacheDays);
        Emotes = new ChatboxEmoteLibrary(Database, () => plugin.Config.Chatbox.SeenEmoteLimit);
        _sequence = Store.HighestSeq();

        RebuildChannels();
        LoadHiddenEmbeds();
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<(long Seq, string Url), byte> _hiddenEmbeds = new();

    private void LoadHiddenEmbeds()
    {
        Database.Read(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT seq, url FROM hidden_embeds;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var seq = reader.GetInt64(0);
                var url = reader.GetString(1);
                _hiddenEmbeds[(seq, url)] = 1;
            }
            return true;
        }, false, "load hidden embeds");
    }

    public bool IsEmbedHidden(long seq, string url) =>
        _hiddenEmbeds.ContainsKey((seq, url));

    public void HideEmbed(long seq, string url)
    {
        _hiddenEmbeds[(seq, url)] = 1;
        Database.Write(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR IGNORE INTO hidden_embeds(seq, url) VALUES ($seq, $url);";
            command.Parameters.AddWithValue("$seq", seq);
            command.Parameters.AddWithValue("$url", url);
            command.ExecuteNonQuery();
        }, "hide embed");
    }

    public ChatboxImageCache ImageCache { get; }

    public ChatboxEmbedCache EmbedCache { get; }

    public ChatboxEmoteLibrary Emotes { get; }

    private ChatboxConfig Config => _plugin.Config.Chatbox;

    public string ActiveChannelId { get; private set; } = string.Empty;

    public bool WindowFocused { get; set; }

    public event Action<ChatboxMessage>? MessageAdded;

    public IReadOnlyList<ChatboxChannelState> Channels
    {
        get
        {
            _channelLock.EnterReadLock();
            try
            {
                return _channels.Values
                    .OrderBy(c => c.Config.Order)
                    .ThenBy(c => c.Config.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            finally
            {
                _channelLock.ExitReadLock();
            }
        }
    }

    public int TotalUnread => Channels.Where(c => !c.Config.MuteNotifications).Sum(c => c.UnreadCount);

    public int TotalMentions => Channels.Where(c => !c.Config.MuteNotifications).Sum(c => c.MentionCount);

    public ChatboxChannelState? GetChannel(string id)
    {
        _channelLock.EnterReadLock();
        try
        {
            return _channels.TryGetValue(id, out var state) ? state : null;
        }
        finally
        {
            _channelLock.ExitReadLock();
        }
    }

    public ChatboxChannelState? ActiveChannel => GetChannel(ResolveActiveChannelId());

    public string ResolveActiveChannelId()
    {
        if (!string.IsNullOrEmpty(ActiveChannelId) && GetChannel(ActiveChannelId) != null)
            return ActiveChannelId;

        if (!string.IsNullOrEmpty(Config.ActiveChannelId) && GetChannel(Config.ActiveChannelId) != null)
        {
            ActiveChannelId = Config.ActiveChannelId;
            return ActiveChannelId;
        }

        var first = Channels.FirstOrDefault(c => c.Config.Enabled);
        ActiveChannelId = first?.Id ?? string.Empty;
        return ActiveChannelId;
    }

    public long SetActiveChannel(string id)
    {
        ActiveChannelId = id;
        Config.ActiveChannelId = id;

        var active = GetChannel(id);
        var divider = active?.BeginViewing() ?? 0;
        if (active != null) PersistState(active);

        _plugin.Config.Save();
        return divider;
    }

    public void MarkActiveRead()
    {
        var id = ResolveActiveChannelId();
        var active = GetChannel(id);
        if (active is null) return;

        var before = active.LastReadSeq;
        active.MarkRead();

        if (active.LastReadSeq != before) PersistState(active);
    }

    public static bool NormalizeTellTypes(ChatboxChannelConfig config)
    {
        var changed = false;

        if (config.SendGameChatType is XivChatType.TellIncoming or XivChatType.TellOutgoing)
        {
            config.SendGameChatType = XivChatType.None;
            changed = true;
        }

        var hasIncoming = config.GameChatTypes.Contains(XivChatType.TellIncoming);
        var hasOutgoing = config.GameChatTypes.Contains(XivChatType.TellOutgoing);
        if (hasIncoming == hasOutgoing) return changed;

        config.GameChatTypes.Add(hasIncoming ? XivChatType.TellOutgoing : XivChatType.TellIncoming);
        return true;
    }

    public void RebuildChannels()
    {
        var added = new List<ChatboxChannelState>();

        var normalized = false;
        foreach (var config in Config.Channels)
            normalized |= NormalizeTellTypes(config);

        if (normalized) _plugin.Config.Save();

        _channelLock.EnterWriteLock();
        try
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var config in Config.Channels)
            {
                seen.Add(config.Id);
                if (_channels.TryGetValue(config.Id, out var existing))
                {
                    existing.Config = config;
                    continue;
                }

                var created = new ChatboxChannelState(config);
                _channels[config.Id] = created;
                added.Add(created);
            }

            foreach (var stale in _channels.Keys.Where(k => !seen.Contains(k)).ToList())
                _channels.Remove(stale);
        }
        finally
        {
            _channelLock.ExitWriteLock();
        }

        foreach (var channel in added)
            Hydrate(channel);
    }

    public static readonly XivChatType[] SendableChatTypes =
    {
        XivChatType.Say,
        XivChatType.Shout,
        XivChatType.Yell,
        XivChatType.Party,
        XivChatType.Alliance,
        XivChatType.FreeCompany,
        XivChatType.Ls1,
        XivChatType.Ls2,
        XivChatType.Ls3,
        XivChatType.Ls4,
        XivChatType.Ls5,
        XivChatType.Ls6,
        XivChatType.Ls7,
        XivChatType.Ls8,
        XivChatType.CrossLinkShell1,
        XivChatType.CrossLinkShell2,
        XivChatType.CrossLinkShell3,
        XivChatType.CrossLinkShell4,
        XivChatType.CrossLinkShell5,
        XivChatType.CrossLinkShell6,
        XivChatType.CrossLinkShell7,
        XivChatType.CrossLinkShell8,
    };

    public static string SendGroupFor(XivChatType type)
    {
        if (LinkshellNameService.LinkshellSlot(type) >= 0)
            return "Linkshells";

        return LinkshellNameService.CrossWorldLinkshellSlot(type) >= 0 ? "Cross-world Linkshells" : "Chat";
    }

    public static bool IsGameMasterChatType(XivChatType type) =>
        type is XivChatType.GmTell
            or XivChatType.GmSay
            or XivChatType.GmShout
            or XivChatType.GmYell
            or XivChatType.GmParty
            or XivChatType.GmFreeCompany
            or XivChatType.GmNoviceNetwork
            or XivChatType.GmLinkshell1 or XivChatType.GmLinkshell2 or XivChatType.GmLinkshell3 or XivChatType.GmLinkshell4
            or XivChatType.GmLinkshell5 or XivChatType.GmLinkshell6 or XivChatType.GmLinkshell7 or XivChatType.GmLinkshell8;

    public static bool IsSendTargetAvailable(XivChatType type) =>
        Array.IndexOf(SendableChatTypes, type) >= 0 && LinkshellNameService.IsJoined(type);

    public static string LabelFor(XivChatType type) => LinkshellNameService.LabelFor(type) ?? type switch
    {
        XivChatType.Say => "Say",
        XivChatType.Shout => "Shout",
        XivChatType.Yell => "Yell",
        XivChatType.Party => "Party",
        XivChatType.CrossParty => "Cross Party",
        XivChatType.Alliance => "Alliance",
        XivChatType.FreeCompany => "Free Company",
        XivChatType.TellIncoming => "Tells",
        XivChatType.TellOutgoing => "Tells",
        XivChatType.NoviceNetwork => "Novice Network",
        XivChatType.PvPTeam => "PvP Team",
        XivChatType.CustomEmote => "Custom Emotes",
        XivChatType.StandardEmote => "Standard Emotes",
        XivChatType.Damage => "Damage Dealt",
        XivChatType.Miss => "Missed Attacks",
        XivChatType.Action => "Actions Used",
        XivChatType.Item => "Items Used",
        XivChatType.Healing => "HP Recovery",
        XivChatType.GainBuff => "Beneficial Effects Granted",
        XivChatType.LoseBuff => "Beneficial Effects Lost",
        XivChatType.GainDebuff => "Detrimental Effects Inflicted",
        XivChatType.LoseDebuff => "Detrimental Effects Lost",
        XivChatType.FreeCompanyAnnouncement => "Free Company Announcements",
        XivChatType.FreeCompanyLoginLogout => "Free Company Login and Logout",
        XivChatType.PvpTeamAnnouncement => "PvP Team Announcements",
        XivChatType.PvpTeamLoginLogout => "PvP Team Login and Logout",
        XivChatType.NoviceNetworkSystem => "Novice Network Notices",
        XivChatType.NPCDialogue => "NPC Dialogue",
        XivChatType.NPCDialogueAnnouncements => "NPC Announcements",
        XivChatType.LootNotice => "Loot Messages",
        XivChatType.LootRoll => "Loot Rolls",
        XivChatType.Progress => "Progression Messages",
        XivChatType.Crafting => "Synthesis Messages",
        XivChatType.Gathering => "Gathering Messages",
        XivChatType.Sign => "Sign Messages",
        XivChatType.RandomNumber => "Random Number Messages",
        XivChatType.Orchestrion => "Orchestrion Track Messages",
        XivChatType.MessageBook => "Message Book Alerts",
        XivChatType.PeriodicRecruitmentNotification => "Recruitment Notices",
        XivChatType.GlamourNotifications => "Glamour Messages",
        XivChatType.RetainerSale => "Retainer Sales",
        XivChatType.Alarm => "Alarm Notifications",
        XivChatType.Echo => "Echo",
        XivChatType.SystemMessage => "System Messages",
        XivChatType.SystemError => "Battle System Messages",
        XivChatType.GatheringSystemMessage => "Gathering System Messages",
        XivChatType.ErrorMessage => "Error Messages",
        XivChatType.Notice => "Notices",
        XivChatType.Urgent => "Urgent Messages",
        XivChatType.Debug => "Debug Messages",
        XivChatType.GmTell => "GM Tells",
        XivChatType.GmSay => "GM Say",
        XivChatType.GmShout => "GM Shout",
        XivChatType.GmYell => "GM Yell",
        XivChatType.GmParty => "GM Party",
        XivChatType.GmFreeCompany => "GM Free Company",
        XivChatType.GmLinkshell1 => "GM Linkshell 1",
        XivChatType.GmLinkshell2 => "GM Linkshell 2",
        XivChatType.GmLinkshell3 => "GM Linkshell 3",
        XivChatType.GmLinkshell4 => "GM Linkshell 4",
        XivChatType.GmLinkshell5 => "GM Linkshell 5",
        XivChatType.GmLinkshell6 => "GM Linkshell 6",
        XivChatType.GmLinkshell7 => "GM Linkshell 7",
        XivChatType.GmLinkshell8 => "GM Linkshell 8",
        XivChatType.GmNoviceNetwork => "GM Novice Network",
        _ => type.ToString(),
    };

    public void ClearAll()
    {
        foreach (var channel in Channels)
            channel.Clear();
        Store.DeleteAll();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        RestoreGameChat();
        PersistAllState();
        EmbedCache.Dispose();
        ImageCache.Dispose();
        Store.Dispose();
        Database.Dispose();
        _channelLock.Dispose();
    }
}
