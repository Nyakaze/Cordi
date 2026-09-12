using System;
using System.Numerics;
using Cordi.Services.Chatbox;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Panels;

public sealed partial class ChatboxSurface
{
    private ChatboxSearchPanel? _search;
    private bool _searchOpen;

    private ChatboxSearchPanel Search
    {
        get
        {
            if (_search != null) return _search;

            _search = new ChatboxSearchPanel(
                _plugin,
                _theme,
                Detached ? $"chatbox-search-{PinnedChannelId}" : "chatbox-search");

            _search.OnOpenMessage = OpenSearchResult;
            return _search;
        }
    }

    public void ToggleSearch()
    {
        _searchOpen = !_searchOpen;
        if (_searchOpen) Search.FocusInput();
    }

    private void HandleSearchShortcut()
    {
        if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows)) return;
        if (!ImGui.GetIO().KeyCtrl) return;
        if (!ImGui.IsKeyPressed(ImGuiKey.F, false)) return;

        ToggleSearch();
    }

    private void DrawSearchBody(float inputHeight)
    {
        using var child = ImRaii.Child("##chatbox-search-body", new Vector2(0, -inputHeight), false);
        if (!child) return;

        if (ImGui.IsKeyPressed(ImGuiKey.Escape, false) && !Chatbox.InputActive)
        {
            _searchOpen = false;
            return;
        }

        Search.Draw(ImGui.GetContentRegionAvail().X, 0f);
    }

    private void OpenSearchResult(ChatboxMessage message) =>
        JumpToMessage(message.ChannelId, message.Seq);

    public bool JumpToMessage(string channelId, long seq)
    {
        if (string.IsNullOrEmpty(channelId) || seq == 0) return false;
        if (Detached && !string.Equals(channelId, PinnedChannelId, StringComparison.Ordinal)) return false;

        if (!Chatbox.RevealMessage(channelId, seq)) return false;

        var channel = Chatbox.GetChannel(channelId);
        if (channel == null) return false;

        if (!Detached && !string.Equals(Chatbox.ResolveActiveChannelId(), channelId, StringComparison.Ordinal))
            Chatbox.SetActiveChannel(channelId);

        _searchOpen = false;
        JumpTo(channel, seq);
        return true;
    }

    private void DisposeSearch()
    {
        _search?.Dispose();
        _search = null;
    }
}
