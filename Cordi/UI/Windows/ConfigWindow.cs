using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;

using Cordi.Core;
using Cordi.UI.Components;
using Cordi.UI.Search;
using Cordi.UI.Tabs;
using Cordi.UI.Themes;

namespace Cordi.UI.Windows;

public sealed partial class ConfigWindow : Window, IDisposable
{
    private readonly CordiPlugin plugin;
    private readonly UiTheme theme = new UiTheme();

    private readonly SidebarNav sidebar;
    private readonly StatChips statChips;
    private readonly PageHeader pageHeader;
    private readonly SettingsSearchIndex searchIndex = new();
    private readonly SearchBox searchBox;

    private ChatsTab chatsTab;
#if DEBUG || CORDI_DEV
    private DebugTab debugTab;
#endif
    private ActivityTab activityTab;
    private PartyAndPlayersTab partyAndPlayersTab;
    private SettingsTab settingsTab;
    private WatchersTab watchersTab;
    private SlashCommandsTab slashCommandsTab;
    private ChatboxTab chatboxTab;
    private LogsTab logsTab;

    private string selectedPageId = PageIds.ChannelMappings;

    public ConfigWindow(CordiPlugin plugin)
        : base("Cordi", ImGuiWindowFlags.None)
    {
        this.plugin = plugin;

        chatsTab = new ChatsTab(plugin, theme);
        activityTab = new ActivityTab(plugin, theme);
#if DEBUG || CORDI_DEV
        debugTab = new DebugTab(plugin, theme);
#endif
        partyAndPlayersTab = new PartyAndPlayersTab(plugin, theme);
        settingsTab = new SettingsTab(plugin, theme);
        watchersTab = new WatchersTab(plugin, theme);
        slashCommandsTab = new SlashCommandsTab(plugin, theme);
        chatboxTab = new ChatboxTab(plugin, theme);
        logsTab = new LogsTab(plugin, theme);

        sidebar = new SidebarNav(theme);
        statChips = new StatChips(theme);
        pageHeader = new PageHeader(theme);
        searchBox = new SearchBox(theme, searchIndex);
        SettingsCatalog.Register(searchIndex);

        UiTheme.GlobalFontScale = plugin.Config.Font.GlobalScale;
        UiTheme.GlobalFontBold = plugin.Config.Font.Bold;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(880, 520),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        RespectCloseHotkey = true;
    }

    public Vector2 LastPos { get; private set; }
    public Vector2 LastSize { get; private set; }

    public override void PreDraw() => theme.PushWindow();
    public override void PostDraw() => theme.PopWindow();

    public override void Draw()
    {
        LastPos = ImGui.GetWindowPos();
        LastSize = ImGui.GetWindowSize();

        theme.ApplyFontScale();

        var sections = BuildNavSections();
        searchIndex.Rebuild(sections);

        var page = ResolvePage(sections, selectedPageId);

        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(theme.Gap(), theme.Gap(0.6f))))
        {
            var navigated = sidebar.Draw(sections, selectedPageId, BuildFooterState());
            if (navigated != null)
                selectedPageId = navigated;

            ImGui.SameLine();

            using var right = ImRaii.Child("##cordi-main", new Vector2(0, 0), false);
            if (!right)
                return;

            DrawTopBar();

            using var content = ImRaii.Child("##cordi-content", new Vector2(0, 0), false);
            if (!content)
                return;

            theme.ApplyFontScale();

            if (searchBox.HasQuery)
            {
                var target = searchBox.DrawResults();
                if (target != null)
                {
                    selectedPageId = target.PageId;
                    ResolveTab(target.PageId)?.SelectSubTab(target.SubTab);
                }

                return;
            }

            if (page == null)
            {
                ImGui.TextDisabled("This page is not available right now.");
                return;
            }

            if (!page.OwnHeader)
                pageHeader.Draw(page.HeaderTitle, page.Subtitle);

            page.Draw();
        }
    }

    public void Navigate(string pageId) => selectedPageId = pageId;

    public void Dispose() { }
}
