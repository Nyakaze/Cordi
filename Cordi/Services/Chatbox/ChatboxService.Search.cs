using System;
using System.Collections.Generic;
using System.Threading;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
    private const int RevealPageLimit = 400;

    public ChatboxSearchResults ExecuteSearch(ChatboxSearchQuery query, CancellationToken token)
    {
        var results = Store.Search(query, token);
        return Reorder(results, query.Sort);
    }

    public string ChannelDisplayName(string channelId)
    {
        var channel = GetChannel(channelId);
        if (channel != null) return channel.Config.Name;

        foreach (var entry in Config.Conversations.Items)
        {
            if (string.Equals(entry.Id, channelId, StringComparison.Ordinal)) return entry.Label;
        }

        return channelId;
    }

    public IReadOnlyList<ChatboxChannelSummary> ArchivedChannels()
    {
        var archived = new List<ChatboxChannelSummary>();

        foreach (var summary in Store.ChannelSummaries())
        {
            if (!string.Equals(ChannelDisplayName(summary.Id), summary.Id, StringComparison.Ordinal)) continue;

            archived.Add(summary);
        }

        return archived;
    }

    public bool RevealMessage(string channelId, long seq)
    {
        var channel = GetChannel(channelId);
        if (channel == null || seq == 0) return false;

        for (var page = 0; page < RevealPageLimit; page++)
        {
            if (channel.FindBySeq(seq) != null) return true;

            var oldest = channel.OldestSeq;
            if (oldest != 0 && oldest < seq) return false;

            if (!LoadOlderHistory(channel)) return false;
        }

        return channel.FindBySeq(seq) != null;
    }

    private ChatboxSearchResults Reorder(ChatboxSearchResults results, ChatboxSearchSort sort)
    {
        if (results.Items.Count <= 1) return results;
        if (sort is ChatboxSearchSort.Newest or ChatboxSearchSort.Oldest) return results;

        var items = new List<ChatboxMessage>(results.Items);

        var comparison = sort switch
        {
            ChatboxSearchSort.AuthorAscending => AuthorComparison(1),
            ChatboxSearchSort.AuthorDescending => AuthorComparison(-1),
            ChatboxSearchSort.ChannelThenNewest => ChannelComparison(-1),
            ChatboxSearchSort.ChannelThenOldest => ChannelComparison(1),
            ChatboxSearchSort.LongestFirst => LengthComparison(-1),
            ChatboxSearchSort.ShortestFirst => LengthComparison(1),
            _ => (Comparison<ChatboxMessage>)((a, b) => b.Seq.CompareTo(a.Seq)),
        };

        items.Sort(comparison);

        return new ChatboxSearchResults
        {
            Items = items,
            Scanned = results.Scanned,
            Matched = results.Matched,
            LimitReached = results.LimitReached,
            ScanCapReached = results.ScanCapReached,
            Elapsed = results.Elapsed,
            Error = results.Error,
        };
    }

    private static Comparison<ChatboxMessage> AuthorComparison(int direction) => (a, b) =>
    {
        var order = string.Compare(a.AuthorName, b.AuthorName, StringComparison.OrdinalIgnoreCase) * direction;
        return order != 0 ? order : b.Seq.CompareTo(a.Seq);
    };

    private Comparison<ChatboxMessage> ChannelComparison(int seqDirection) => (a, b) =>
    {
        var order = string.Compare(
            ChannelDisplayName(a.ChannelId),
            ChannelDisplayName(b.ChannelId),
            StringComparison.OrdinalIgnoreCase);

        if (order != 0) return order;

        return seqDirection > 0 ? a.Seq.CompareTo(b.Seq) : b.Seq.CompareTo(a.Seq);
    };

    private static Comparison<ChatboxMessage> LengthComparison(int direction) => (a, b) =>
    {
        var order = a.RawContent.Length.CompareTo(b.RawContent.Length) * direction;
        return order != 0 ? order : b.Seq.CompareTo(a.Seq);
    };
}
