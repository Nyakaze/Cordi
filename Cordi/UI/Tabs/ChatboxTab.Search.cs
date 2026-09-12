using Cordi.Services.Chatbox;
using Cordi.UI.Panels;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab
{
    private const float SearchResultsHeight = 420f;

    private ChatboxSearchPanel? searchPanel;

    private ChatboxSearchPanel SearchPanel
    {
        get
        {
            if (searchPanel != null) return searchPanel;

            searchPanel = new ChatboxSearchPanel(plugin, theme, "settings-chatbox-search");
            searchPanel.OnOpenMessage = OpenInChatbox;
            return searchPanel;
        }
    }

    public void DrawSearch()
    {
        ConsumeScroll();

        Layout.Draw("Search", "Search every message Cordi has ever stored.");

        Card.Draw(
            "chatbox-search",
            innerWidth => SearchPanel.Draw(innerWidth, theme.Scaled(SearchResultsHeight)),
            "Message Search");
    }

    private void OpenInChatbox(ChatboxMessage message)
    {
        var window = plugin.ChatboxWindow;
        if (window == null) return;

        window.IsOpen = true;
        window.JumpToMessage(message.ChannelId, message.Seq);
    }

    public void Dispose()
    {
        searchPanel?.Dispose();
        searchPanel = null;
    }
}
