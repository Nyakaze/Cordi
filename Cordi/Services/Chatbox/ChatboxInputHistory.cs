using System;
using System.Collections.Generic;

namespace Cordi.Services.Chatbox;

public sealed class ChatboxInputHistory
{
    private const int MaxEntries = 50;

    private readonly List<string> _entries = new();

    public int Count => _entries.Count;

    public void Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        if (_entries.Count > 0 && string.Equals(_entries[^1], text, StringComparison.Ordinal)) return;

        _entries.Add(text);

        var excess = _entries.Count - MaxEntries;
        if (excess > 0) _entries.RemoveRange(0, excess);
    }

    public bool TryGet(int offset, out string text)
    {
        if (offset <= 0 || offset > _entries.Count)
        {
            text = string.Empty;
            return false;
        }

        text = _entries[^offset];
        return true;
    }

    public void Clear() => _entries.Clear();
}
