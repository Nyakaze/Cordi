using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
    private static readonly TimeSpan ResolverLifetime = TimeSpan.FromSeconds(2);

    private readonly Dictionary<string, (MentionResolver Resolver, DateTime At)> _resolverCache =
        new(StringComparer.Ordinal);

    private readonly List<string> _staleResolvers = new();

    private readonly object _resolverGate = new();

    public ChatboxDatabase Database { get; }

    public ChatboxMessageStore Store { get; }

    public int ViewLimitFor(string channelId)
    {
        if (IsConversationId(channelId)) return Math.Max(50, Config.Conversations.HistoryWindow);

        var channel = GetChannel(channelId);
        if (channel is null) return Math.Max(50, Config.MaxMessagesPerChannel);

        var limit = channel.Config.MaxMessages;
        return Math.Max(50, limit > 0 ? limit : Config.MaxMessagesPerChannel);
    }

    private int MemoryCapFor(ChatboxChannelState channel) =>
        Math.Max(channel.HistoryWindow, ViewLimitFor(channel.Id));

    public void ReleaseAllHistory()
    {
        foreach (var channel in Channels)
            ReleaseOlderHistory(channel);
    }

    public void ReleaseOlderHistory(ChatboxChannelState channel)
    {
        if (!channel.TracksHistory) return;

        var window = ViewLimitFor(channel.Id);
        if (channel.HistoryWindow <= window && channel.Count <= window) return;

        channel.HistoryWindow = window;

        if (channel.TrimTo(window)) channel.HasMoreHistory = true;
    }

    private void Persist(ChatboxMessage entry, ChatboxChannelState target)
    {
        if (!target.Config.PersistHistory) return;

        Store.Enqueue(entry);
    }

    private void Hydrate(ChatboxChannelState channel)
    {
        if (!channel.Config.PersistHistory) return;

        var window = ViewLimitFor(channel.Id);
        var history = Store.Load(channel.Id, window);

        channel.HistoryWindow = window;
        channel.TracksHistory = true;
        channel.HasMoreHistory = history.Count >= window;

        if (history.Count == 0) return;

        var (divider, lastRead) = Store.LoadState(channel.Id);
        channel.Restore(history, divider, lastRead);
    }

    public bool LoadOlderHistory(ChatboxChannelState channel)
    {
        if (!channel.HasMoreHistory) return false;

        var page = Math.Max(50, Config.Conversations.HistoryPageSize);
        var oldest = channel.OldestSeq;

        if (oldest <= 0)
        {
            channel.HasMoreHistory = false;
            return false;
        }

        var older = Store.LoadBefore(channel.Id, oldest, page);

        if (older.Count == 0)
        {
            channel.HasMoreHistory = false;
            return false;
        }

        channel.PrependHistory(older);
        channel.HistoryWindow += older.Count;
        channel.HasMoreHistory = older.Count >= page;

        return true;
    }

    public void EnsureSegments(ChatboxChannelState channel, ChatboxMessage message)
    {
        if (message.SegmentsReady) return;

        message.SegmentsReady = true;

        var source = RestoreSource(message);
        if (source != null)
        {
            message.Segments = ParseGameContent(source, CachedResolver(channel), out var sourceOnlyEmotes, out _);
            message.OnlyEmotes = sourceOnlyEmotes;
            Emotes.Record(message);
            ReleaseSource(message, dropPayload: true);
            return;
        }

        if (string.IsNullOrEmpty(message.RawContent)) return;

        var parsed = _parser.Parse(message.RawContent, CachedResolver(channel));
        message.Segments = parsed.Segments;
        message.OnlyEmotes = parsed.OnlyEmotes;

        Emotes.Record(message);
    }

    internal static void ReleaseSource(ChatboxMessage message, bool dropPayload)
    {
        if (HoldsPluginLink(message)) return;

        message.Source = null;

        if (dropPayload) message.SourcePayload = null;
    }

    private static bool HoldsPluginLink(ChatboxMessage message)
    {
        var segments = message.Segments;

        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].LinkKind == GameLinkKind.Plugin) return true;
        }

        return false;
    }

    internal static SeString? RestoreSource(ChatboxMessage message)
    {
        if (message.Source != null) return message.Source;
        if (message.SourcePayload == null || message.SourcePayload.Length == 0) return null;

        try
        {
            message.Source = SeString.Parse(message.SourcePayload);
        }
        catch (Exception ex)
        {
            message.SourcePayload = null;
            Service.Log.Debug($"[Chatbox] Failed to restore SeString for seq {message.Seq}: {ex.Message}");
        }

        return message.Source;
    }

    private MentionResolver CachedResolver(ChatboxChannelState channel)
    {
        var now = DateTime.UtcNow;

        lock (_resolverGate)
        {
            if (_resolverCache.TryGetValue(channel.Id, out var cached)
                && now - cached.At < ResolverLifetime)
                return cached.Resolver;

            EvictResolvers(now);

            var resolver = BuildResolver(channel);
            _resolverCache[channel.Id] = (resolver, now);
            return resolver;
        }
    }

    private void EvictResolvers(DateTime now)
    {
        if (_resolverCache.Count < 16) return;

        _staleResolvers.Clear();

        foreach (var pair in _resolverCache)
        {
            if (now - pair.Value.At >= ResolverLifetime) _staleResolvers.Add(pair.Key);
        }

        foreach (var key in _staleResolvers)
            _resolverCache.Remove(key);

        _staleResolvers.Clear();
    }

    internal void PersistState(ChatboxChannelState channel)
    {
        if (!channel.Config.PersistHistory) return;

        Store.QueueState(channel.Id, channel.DividerSeq, channel.LastReadSeq);
    }

    public void ClearDivider(ChatboxChannelState channel)
    {
        channel.ClearDivider();
        PersistState(channel);
    }

    public void PersistAllState()
    {
        foreach (var channel in Channels)
            PersistState(channel);
    }

    public void ApplyRetention()
    {
        ImageCache.PruneStored(Config.ImageCacheMaxEntries);
        EmbedCache.PruneExpired();
    }

    public void ForgetChannel(string channelId)
    {
        Store.DeleteChannel(channelId);
        GetChannel(channelId)?.Clear();
    }

    public void PruneOrphanedHistory()
    {
        var known = new HashSet<string>(Config.Channels.Select(c => c.Id), StringComparer.Ordinal);

        foreach (var entry in Config.Conversations.Items)
            known.Add(entry.Id);

        Store.PruneOrphans(known);
    }

    public string StorageSummary()
    {
        var messages = Store.TotalCount();
        var (images, _, imageBytes) = ImageCache.Inspect();
        var total = Database.FileSizeBytes();

        return string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{messages} message(s), {images} image(s), {EmbedCache.Count()} embed(s), " +
            $"{total / (1024f * 1024f):0.0} MB on disk ({imageBytes / (1024f * 1024f):0.0} MB images)");
    }
}
