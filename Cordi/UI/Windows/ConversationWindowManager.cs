using System;
using System.Collections.Generic;
using System.Linq;
using Cordi.Configuration;
using Cordi.Core;
using Dalamud.Interface.Windowing;

namespace Cordi.UI.Windows;

public sealed class ConversationWindowManager : IDisposable
{
    private readonly CordiPlugin _plugin;
    private readonly WindowSystem _windows;
    private readonly Dictionary<string, ConversationWindow> _open = new(StringComparer.Ordinal);

    public ConversationWindowManager(CordiPlugin plugin, WindowSystem windows)
    {
        _plugin = plugin;
        _windows = windows;
    }

    private ConversationSettings Settings => _plugin.Config.Chatbox.Conversations;

    public bool IsDetached(ConversationConfig entry) =>
        Settings.Enabled && entry.Open && Settings.OpenInOwnWindow;

    public void Sync()
    {
        foreach (var entry in Settings.Items.ToList())
        {
            if (IsDetached(entry)) Ensure(entry);
            else Close(entry.Id);
        }

        foreach (var id in _open.Keys.ToList())
        {
            if (Settings.Items.Any(e => string.Equals(e.Id, id, StringComparison.Ordinal))) continue;

            Close(id);
        }
    }

    public void Focus(string id)
    {
        Sync();

        if (_open.TryGetValue(id, out var window)) window.Activate();
    }

    public bool TryGet(string id, out ConversationWindow window) => _open.TryGetValue(id, out window!);

    private void Ensure(ConversationConfig entry)
    {
        if (_open.ContainsKey(entry.Id)) return;

        var window = new ConversationWindow(_plugin, entry) { IsOpen = true };

        _open[entry.Id] = window;
        _windows.AddWindow(window);
    }

    private void Close(string id)
    {
        if (!_open.Remove(id, out var window)) return;

        _windows.RemoveWindow(window);
        window.IsOpen = false;
        window.Dispose();
    }

    public void Dispose()
    {
        foreach (var id in _open.Keys.ToList())
            Close(id);
    }
}
