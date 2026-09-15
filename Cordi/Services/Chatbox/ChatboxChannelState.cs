using System;
using System.Collections.Generic;
using System.Linq;
using Cordi.Configuration;
using Dalamud.Game.Text;

namespace Cordi.Services.Chatbox;

public sealed class ChatboxChannelState
{
    private readonly List<ChatboxMessage> _messages = new();
    private readonly object _gate = new();
    private readonly HashSet<string> _authors = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<long> _crossRead = new();

    private string[]? _authorCache;

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

    public int HistoryWindow { get; set; }
    public bool HasMoreHistory { get; set; }
    public bool TracksHistory { get; set; }

    public int Count
    {
        get
        {
            lock (_gate)
                return _messages.Count;
        }
    }

    public long OldestSeq
    {
        get
        {
            lock (_gate)
                return _messages.Count == 0 ? 0 : _messages[0].Seq;
        }
    }

    public void PrependHistory(IReadOnlyList<ChatboxMessage> older)
    {
        lock (_gate)
        {
            _messages.InsertRange(0, older);

            foreach (var message in older)
                TrackAuthor(message);
        }
    }

    public bool TrimTo(int cap)
    {
        if (cap <= 0) return false;

        lock (_gate)
        {
            if (_messages.Count <= cap) return false;

            _messages.RemoveRange(0, _messages.Count - cap);
            return true;
        }
    }

    private void TrackAuthor(ChatboxMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.AuthorName)) return;
        if (!_authors.Add(message.AuthorName)) return;

        _authorCache = null;
    }

    public void Append(ChatboxMessage message, int cap, bool markUnread)
    {
        lock (_gate)
        {
            _messages.Add(message);
            TrackAuthor(message);

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
            _crossRead.Clear();
            if (_messages.Count > 0) LastReadSeq = _messages[^1].Seq;
        }
    }

    public bool MarkTypesRead(HashSet<XivChatType> types)
    {
        lock (_gate)
        {
            if (UnreadCount == 0) return false;

            var added = false;

            for (var i = FirstUnreadIndex(); i < _messages.Count; i++)
            {
                var message = _messages[i];
                if (message.IsSelf || message.Origin != ChatboxOrigin.Game) continue;
                if (!types.Contains(message.GameChatType)) continue;

                added |= _crossRead.Add(message.Seq);
            }

            return added && RecountUnread();
        }
    }

    private int FirstUnreadIndex()
    {
        var index = _messages.Count;
        while (index > 0 && _messages[index - 1].Seq > LastReadSeq) index--;

        return index;
    }

    private bool RecountUnread()
    {
        var previousFirst = FirstUnreadSeq;

        UnreadCount = 0;
        MentionCount = 0;
        FirstUnreadSeq = 0;

        for (var i = FirstUnreadIndex(); i < _messages.Count; i++)
        {
            var message = _messages[i];
            if (message.IsSelf || _crossRead.Contains(message.Seq)) continue;

            if (UnreadCount == 0) FirstUnreadSeq = message.Seq;
            UnreadCount++;
            if (message.MentionsMe) MentionCount++;
        }

        if (UnreadCount > 0) return false;

        if (DividerSeq == 0) DividerSeq = previousFirst;
        _crossRead.Clear();

        if (_messages.Count == 0) return false;

        var last = _messages[^1].Seq;
        if (last == LastReadSeq) return false;

        LastReadSeq = last;
        return true;
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
            _crossRead.Clear();
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
            _authorCache = null;
            _crossRead.Clear();
            _messages.AddRange(history);

            UnreadCount = 0;
            MentionCount = 0;
            FirstUnreadSeq = 0;
            LastReadSeq = lastReadSeq;
            DividerSeq = dividerSeq;

            foreach (var message in _messages)
            {
                TrackAuthor(message);

                if (message.Seq <= lastReadSeq || message.IsSelf) continue;

                if (UnreadCount == 0) FirstUnreadSeq = message.Seq;
                UnreadCount++;
                if (message.MentionsMe) MentionCount++;
            }

            if (DividerSeq == 0 || DividerSeq <= lastReadSeq) DividerSeq = FirstUnreadSeq;
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
        {
            for (var i = _messages.Count - 1; i >= 0; i--)
            {
                var message = _messages[i];
                if (message.Seq == seq) return message;
                if (message.Seq < seq) return null;
            }

            return null;
        }
    }

    public IReadOnlyCollection<string> RecentAuthors()
    {
        lock (_gate)
            return _authorCache ??= _authors.ToArray();
    }

    public void Clear()
    {
        lock (_gate)
        {
            _messages.Clear();
            _authors.Clear();
            _authorCache = null;
            _crossRead.Clear();
            UnreadCount = 0;
            MentionCount = 0;
            FirstUnreadSeq = 0;
            DividerSeq = 0;
            LastReadSeq = 0;
            HasMoreHistory = false;
        }
    }
}
