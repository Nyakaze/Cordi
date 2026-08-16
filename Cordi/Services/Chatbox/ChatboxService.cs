using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Cordi.Configuration;
using Cordi.Core;
using Dalamud.Game.Text;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService : IDisposable
{
    public const string CombinedChannelId = "__combined__";

    private readonly CordiPlugin _plugin;
    private readonly Dictionary<string, ChatboxChannelState> _channels = new(StringComparer.Ordinal);
    private readonly ChatboxChannelState _combined;
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
        ImageCache = new ChatboxImageCache(Database, () => plugin.Config.Chatbox.ImageCacheMaxEntries);
        EmbedCache = new ChatboxEmbedCache(Database, () => plugin.Config.Chatbox.EmbedCacheDays);
        _sequence = Store.HighestSeq();

        _combined = new ChatboxChannelState(new ChatboxChannelConfig
        {
            Id = CombinedChannelId,
            Name = plugin.Config.Chatbox.CombinedChannelName,
            ShortLabel = "ALL",
            SendToGame = false,
        });

        RebuildChannels();
        RestoreCombined();
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

    public ChatboxChannelState Combined => _combined;

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
        if (id == CombinedChannelId) return _combined;

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
        ActiveChannelId = first?.Id ?? (Config.ShowCombinedChannel ? CombinedChannelId : string.Empty);
        return ActiveChannelId;
    }

    public long SetActiveChannel(string id)
    {
        ActiveChannelId = id;
        Config.ActiveChannelId = id;

        var active = GetChannel(id);
        var divider = active?.BeginViewing() ?? 0;
        if (active != null) PersistState(active);

        if (id == CombinedChannelId)
        {
            foreach (var channel in Channels)
            {
                channel.BeginViewing();
                PersistState(channel);
            }
        }

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
        if (id != CombinedChannelId) _combined.MarkRead();

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

        _combined.Config.Name = Config.CombinedChannelName;

        foreach (var channel in added)
            Hydrate(channel);
    }

    public static string LabelFor(XivChatType type) => type switch
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
        _ => type.ToString(),
    };

    public void ClearAll()
    {
        foreach (var channel in Channels)
            channel.Clear();
        _combined.Clear();
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
