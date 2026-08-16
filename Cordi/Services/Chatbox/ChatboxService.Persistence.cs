using System;
using System.Collections.Generic;
using System.Linq;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
    private readonly Dictionary<string, (MentionResolver Resolver, DateTime At)> _resolverCache =
        new(StringComparer.Ordinal);

    private readonly object _resolverGate = new();

    public ChatboxDatabase Database { get; }

    public ChatboxMessageStore Store { get; }

    public int LimitFor(string channelId)
    {
        var channel = GetChannel(channelId);
        if (channel is null) return Config.MaxMessagesPerChannel;

        var limit = channel.Config.MaxMessages;
        return limit > 0 ? limit : Config.MaxMessagesPerChannel;
    }

    private void Persist(ChatboxMessage entry, ChatboxChannelState target)
    {
        if (!Config.PersistHistory || !target.Config.PersistHistory) return;
        if (target.Id == CombinedChannelId) return;

        Store.Enqueue(entry);
    }

    private void Hydrate(ChatboxChannelState channel)
    {
        if (!Config.PersistHistory || !channel.Config.PersistHistory) return;

        var history = Store.Load(channel.Id, LimitFor(channel.Id));
        if (history.Count == 0) return;

        var (divider, lastRead) = Store.LoadState(channel.Id);
        channel.Restore(history, divider, lastRead);
    }

    private void RestoreCombined()
    {
        if (!Config.ShowCombinedChannel) return;

        var merged = Channels
            .SelectMany(c => c.Snapshot())
            .GroupBy(m => m.Seq)
            .Select(g => g.First())
            .OrderBy(m => m.Seq)
            .ToList();

        var directCombined = Store.Load(CombinedChannelId, LimitFor(CombinedChannelId));
        if (directCombined.Count > 0)
        {
            merged = merged.Concat(directCombined)
                .GroupBy(m => m.Seq)
                .Select(g => g.First())
                .OrderBy(m => m.Seq)
                .ToList();
        }

        var cap = Config.MaxMessagesPerChannel;
        if (cap > 0 && merged.Count > cap)
            merged.RemoveRange(0, merged.Count - cap);

        _combined.Restore(merged, 0, merged.Count > 0 ? merged[^1].Seq : 0);
    }

    public void EnsureSegments(ChatboxChannelState channel, ChatboxMessage message)
    {
        if (message.SegmentsReady) return;

        message.SegmentsReady = true;

        if (string.IsNullOrEmpty(message.RawContent)) return;

        var parsed = _parser.Parse(message.RawContent, CachedResolver(channel));
        message.Segments = parsed.Segments;
        message.OnlyEmotes = parsed.OnlyEmotes;

        Emotes.Record(message);
    }

    private MentionResolver CachedResolver(ChatboxChannelState channel)
    {
        var now = DateTime.UtcNow;

        lock (_resolverGate)
        {
            if (_resolverCache.TryGetValue(channel.Id, out var cached)
                && now - cached.At < TimeSpan.FromSeconds(2))
                return cached.Resolver;

            var resolver = BuildResolver(channel);
            _resolverCache[channel.Id] = (resolver, now);
            return resolver;
        }
    }

    internal void PersistState(ChatboxChannelState channel)
    {
        if (!Config.PersistHistory || channel.Id == CombinedChannelId) return;

        Store.QueueState(channel.Id, channel.DividerSeq, channel.LastReadSeq);
    }

    public void ClearDivider(ChatboxChannelState channel)
    {
        channel.ClearDivider();
        PersistState(channel);

        if (channel.Id == CombinedChannelId)
        {
            foreach (var ch in Channels)
            {
                ch.ClearDivider();
                PersistState(ch);
            }
        }
    }

    public void PersistAllState()
    {
        if (!Config.PersistHistory) return;

        foreach (var channel in Channels)
            PersistState(channel);
    }

    public void ApplyRetention()
    {
        foreach (var channel in Channels)
            Store.Trim(channel.Id, LimitFor(channel.Id));

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
