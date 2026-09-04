using System;
using System.Collections.Generic;
using System.Linq;
using Cordi.Configuration;

namespace Cordi.Services.Chatbox;

public sealed class ChatboxChannelState
{
    private readonly List<ChatboxMessage> _messages = new();
    private readonly object _gate = new();
    private readonly HashSet<string> _authors = new(StringComparer.OrdinalIgnoreCase);

    public ChatboxChannelState(ChatboxChannelConfig config)
    {
        Config = config;
    }

    public ChatboxChannelConfig Config { get; set; }
    public string Id => Config.Id;

    public int UnreadCount { get; private set; }
    public int MentionCount { get; private set; }
    public long FirstUnreadSeq { get; private set; }
    public long DividerSeq { get; private set; }
    public long LastReadSeq { get; private set; }
    public DateTime LastActivity { get; private set; } = DateTime.MinValue;

    public void Append(ChatboxMessage message, int cap, bool markUnread)
    {
        lock (_gate)
        {
            _messages.Add(message);
            if (!string.IsNullOrWhiteSpace(message.AuthorName))
                _authors.Add(message.AuthorName);

            if (cap > 0 && _messages.Count > cap)
                _messages.RemoveRange(0, _messages.Count - cap);

            LastActivity = message.Timestamp;

            if (!markUnread) return;

            if (UnreadCount == 0) FirstUnreadSeq = message.Seq;
            UnreadCount++;
            if (message.MentionsMe) MentionCount++;
        }
    }

    public void MarkRead()
    {
        lock (_gate)
        {
            if (DividerSeq == 0) DividerSeq = FirstUnreadSeq;
            UnreadCount = 0;
            MentionCount = 0;
            FirstUnreadSeq = 0;
            if (_messages.Count > 0) LastReadSeq = _messages[^1].Seq;
        }
    }

    public void ClearDivider()
    {
        lock (_gate)
        {
            DividerSeq = 0;
        }
    }

    public void SetDivider(long seq)
    {
        lock (_gate)
        {
            if (DividerSeq == 0) DividerSeq = seq;
        }
    }

    public long BeginViewing()
    {
        lock (_gate)
        {
            DividerSeq = FirstUnreadSeq;
            UnreadCount = 0;
            MentionCount = 0;
            FirstUnreadSeq = 0;
            if (_messages.Count > 0) LastReadSeq = _messages[^1].Seq;
            return DividerSeq;
        }
    }

    public void Restore(IReadOnlyList<ChatboxMessage> history, long dividerSeq, long lastReadSeq)
    {
        lock (_gate)
        {
            _messages.Clear();
            _authors.Clear();
            _messages.AddRange(history);

            UnreadCount = 0;
            MentionCount = 0;
            FirstUnreadSeq = 0;
            LastReadSeq = lastReadSeq;
            DividerSeq = dividerSeq;

            foreach (var message in _messages)
            {
                if (!string.IsNullOrWhiteSpace(message.AuthorName))
                    _authors.Add(message.AuthorName);

                if (message.Seq <= lastReadSeq || message.IsSelf) continue;

                if (UnreadCount == 0) FirstUnreadSeq = message.Seq;
                UnreadCount++;
                if (message.MentionsMe) MentionCount++;
            }

            if (DividerSeq == 0) DividerSeq = FirstUnreadSeq;
            if (_messages.Count > 0) LastActivity = _messages[^1].Timestamp;
        }
    }

    public ChatboxMessage[] Snapshot()
    {
        lock (_gate)
            return _messages.ToArray();
    }

    public void SnapshotInto(List<ChatboxMessage> buffer)
    {
        buffer.Clear();

        lock (_gate)
            buffer.AddRange(_messages);
    }

    public ChatboxMessage? FindBySeq(long seq)
    {
        lock (_gate)
            return _messages.FirstOrDefault(m => m.Seq == seq);
    }

    public IReadOnlyCollection<string> RecentAuthors()
    {
        lock (_gate)
            return _authors.ToArray();
    }

    public void Clear()
    {
        lock (_gate)
        {
            _messages.Clear();
            _authors.Clear();
            UnreadCount = 0;
            MentionCount = 0;
            FirstUnreadSeq = 0;
            DividerSeq = 0;
            LastReadSeq = 0;
        }
    }
}
