using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Domain;
using Cordi.Services.Features;
using Cordi.Services.Translation;
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
        Translator = new TranslationService(configDirectory, () => plugin.Config.Translation, plugin.LogService);

        RebuildChannels();
        RestoreConversations();
        LoadHiddenEmbeds();
        InitializeSourceHook();
        InitializeContextMenu();
        ChatboxAutoTranslate.Preload(plugin.LogService);
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

    public TranslationService Translator { get; }

    private ChatboxConfig Config => _plugin.Config.Chatbox;

    public string ActiveChannelId { get; private set; } = string.Empty;

    public bool WindowFocused { get; set; }

    private readonly HashSet<string> _viewedChannels = new(StringComparer.Ordinal);

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

    public Vector4 ColorFor(ChatboxChannelConfig config, XivChatType type) =>
        config.OverrideChatColor ? config.Color : Config.ChatTypeColor(type) ?? config.Color;

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

    public long BeginViewingActive() => BeginViewing(GetChannel(ResolveActiveChannelId()));

    public long BeginViewing(ChatboxChannelState? channel)
    {
        if (channel is null) return 0;

        var divider = channel.BeginViewing();
        PersistState(channel);
        return divider;
    }

    public void MarkActiveRead() => MarkChannelRead(GetChannel(ResolveActiveChannelId()));

    public void MarkChannelRead(ChatboxChannelState? channel)
    {
        if (channel is null) return;

        var before = channel.LastReadSeq;
        channel.MarkRead();

        if (channel.LastReadSeq != before) PersistState(channel);
    }

    public void SetChannelViewed(string channelId, bool viewed)
    {
        if (string.IsNullOrEmpty(channelId)) return;

        lock (_viewedChannels)
        {
            if (viewed) _viewedChannels.Add(channelId);
            else _viewedChannels.Remove(channelId);
        }
    }

    public bool IsChannelViewed(string channelId)
    {
        lock (_viewedChannels)
        {
            if (_viewedChannels.Contains(channelId)) return true;
        }

        return WindowFocused
               && _plugin.ChatboxWindow?.IsOpen == true
               && string.Equals(ResolveActiveChannelId(), channelId, StringComparison.Ordinal);
    }

    public static bool NormalizeTellTypes(ChatboxChannelConfig config)
    {
        var changed = false;

        if (ChatTypes.IsTell(config.SendGameChatType))
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
                if (config.IsSeparator) continue;

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

            foreach (var stale in _channels.Keys.Where(k => !seen.Contains(k) && !IsConversationId(k)).ToList())
                _channels.Remove(stale);
        }
        finally
        {
            _channelLock.ExitWriteLock();
        }

        foreach (var channel in added)
            Hydrate(channel);
    }

    public static bool IsSendTargetAvailable(XivChatType type) =>
        ChatTypes.IsSendable(type) && LinkshellNameService.IsJoined(type);

    public static string LabelFor(XivChatType type) =>
        LinkshellNameService.LabelFor(type) ?? ChatTypes.Label(type);

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

        DisposeSourceHook();
        DisposeContextMenu();
        RestoreGameChat();
        RestoreGameSounds();
        PersistAllState();
        Translator.Dispose();
        EmbedCache.Dispose();
        ImageCache.Dispose();
        Store.Dispose();
        Database.Dispose();
        _channelLock.Dispose();
    }
}
