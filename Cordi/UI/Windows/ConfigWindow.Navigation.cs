using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Cordi.UI.Components;
using Dalamud.Interface;

namespace Cordi.UI.Windows;

public static class PageIds
{
    public const string ChannelMappings = "chats/Channel Mappings";
    public const string Conversations = "chats/Conversations";
    public const string AdvertisementFilter = "chats/Advertisement Filter";
    public const string ChatboxOverview = "chatbox/Overview";
    public const string ChatboxChannels = "chatbox/Channels";
    public const string ChatboxAppearance = "chatbox/Appearance";
    public const string ChatboxMentions = "chatbox/Mentions";
    public const string ChatboxContent = "chatbox/Content";
    public const string ChatboxTranslation = "chatbox/Translation";
    public const string ChatboxStorage = "chatbox/Storage";
    public const string Peeper = "watchers/Peeper";
    public const string EmoteLog = "watchers/Emote Log";
    public const string CombinedOverlay = "watchers/Combined Overlay";
    public const string ActivityOverview = "activity/Overview";
    public const string ActivityPlaying = "activity/Playing";
    public const string ActivityListening = "activity/Listening";
    public const string ActivityWatching = "activity/Watching";
    public const string ActivityCustom = "activity/Custom";
    public const string PartyRadar = "party";
    public const string SlashCommands = "slash";
    public const string Settings = "settings";
    public const string Debug = "debug";
    public const string Logs = "logs";
}

public sealed partial class ConfigWindow
{
    private const string GitHubUrl = "https://github.com/Nyakaze/Cordi";
    private const string DiscordUrl = "https://discord.com/channels/@me";
    private const string DocsUrl = "https://github.com/Nyakaze/Cordi#readme";

    private static readonly HashSet<string> OwnHeaderPages = new()
    {
        PageIds.ChannelMappings,
        PageIds.Conversations,
        PageIds.AdvertisementFilter,
        PageIds.ChatboxOverview,
        PageIds.ChatboxChannels,
        PageIds.ChatboxAppearance,
        PageIds.ChatboxMentions,
        PageIds.ChatboxContent,
        PageIds.ChatboxTranslation,
        PageIds.ChatboxStorage,
        PageIds.Peeper,
        PageIds.EmoteLog,
        PageIds.CombinedOverlay,
        PageIds.ActivityOverview,
        PageIds.ActivityPlaying,
        PageIds.ActivityListening,
        PageIds.ActivityWatching,
        PageIds.ActivityCustom,
        PageIds.PartyRadar,
        PageIds.SlashCommands,
    };

    private static readonly Dictionary<string, FontAwesomeIcon> SubTabIcons = new()
    {
        ["Channel Mappings"] = FontAwesomeIcon.Link,
        ["Custom Avatars"] = FontAwesomeIcon.UserCircle,
        ["Advertisement Filter"] = FontAwesomeIcon.Filter,
        ["Peeper"] = FontAwesomeIcon.Eye,
        ["Emote Log"] = FontAwesomeIcon.TheaterMasks,
        ["Combined Overlay"] = FontAwesomeIcon.Columns,
        ["Overview"] = FontAwesomeIcon.WaveSquare,
        ["Playing"] = FontAwesomeIcon.Gamepad,
        ["Listening"] = FontAwesomeIcon.Music,
        ["Watching"] = FontAwesomeIcon.Video,
        ["Custom"] = FontAwesomeIcon.CommentDots,
    };

    private static readonly Dictionary<string, string> PageSubtitles = new()
    {
        ["Channel Mappings"] = "Map Discord channels to in-game chat types",
        ["Custom Avatars"] = "Per-character avatars used for relayed messages",
        ["Advertisement Filter"] = "Score-based filtering for advertisement spam",
        ["Peeper"] = "Alerts you when another player targets your character",
        ["Emote Log"] = "Records emotes performed around you",
        ["Combined Overlay"] = "Peeper and the Emote Log in a single window",
        [PageIds.ChatboxOverview] = "Turn the chatbox on and shape its window",
        [PageIds.ChatboxChannels] = "Bundle game chat types with Discord channels",
        [PageIds.Conversations] = "Tell threads, their history and their Discord counterparts",
        [PageIds.ChatboxAppearance] = "Layout, avatars, message style and colours",
        [PageIds.ChatboxMentions] = "What counts as a mention and how you hear about it",
        [PageIds.ChatboxContent] = "Emotes, the emoji picker and link previews",
        [PageIds.ChatboxTranslation] = "Automatic translation of foreign chat messages",
        [PageIds.ChatboxStorage] = "History limits, image cache and maintenance",
        ["Overview"] = "What Cordi is reading from Discord and putting on your title",
        ["Playing"] = "Titles built from the game your Discord account is playing",
        ["Listening"] = "Titles built from the track your Discord account is listening to",
        ["Watching"] = "Titles built from what your Discord account is watching",
        ["Custom"] = "A standalone title that does not need a Discord account",
        [PageIds.PartyRadar] = "Your party, their gear and the notes you left them",
        [PageIds.SlashCommands] = "Discord slash commands exposed by Cordi",
        [PageIds.Settings] = "Appearance, fonts and plugin behaviour",
        [PageIds.Debug] = "Diagnostics and internal state",
        [PageIds.Logs] = "Plugin log output",
    };

    private sealed class ResolvedPage
    {
        public required string HeaderTitle { get; init; }
        public required string Subtitle { get; init; }
        public required Action Draw { get; init; }
        public bool OwnHeader { get; init; }
    }

    private IReadOnlyList<NavSection> BuildNavSections()
    {
        var communication = new List<NavItem>();

        foreach (var (label, draw) in chatsTab.SubTabs ?? Array.Empty<(string, Action)>())
        {
            communication.Add(new NavItem
            {
                Id = $"chats/{label}",
                Label = label,
                Icon = SubTabIcons.TryGetValue(label, out var icon) ? icon : FontAwesomeIcon.CommentDots,
                Draw = draw,
                Subtitle = PageSubtitles.TryGetValue(label, out var subtitle) ? subtitle : string.Empty,
                OwnHeader = OwnHeaderPages.Contains($"chats/{label}"),
            });
        }

        communication.Insert(
            Math.Min(1, communication.Count),
            MakeItem(PageIds.Conversations, "Conversations", FontAwesomeIcon.Envelope, chatboxTab.DrawConversations));

        var chatbox = new List<NavItem>
        {
            MakeItem(PageIds.ChatboxOverview, "Overview", FontAwesomeIcon.CommentAlt, chatboxTab.DrawOverview),
            MakeItem(PageIds.ChatboxChannels, "Channels", FontAwesomeIcon.Hashtag, chatboxTab.DrawChannels),
            MakeItem(PageIds.ChatboxAppearance, "Appearance", FontAwesomeIcon.PaintRoller, chatboxTab.DrawAppearance),
            MakeItem(PageIds.ChatboxMentions, "Mentions", FontAwesomeIcon.At, chatboxTab.DrawMentions),
            MakeItem(PageIds.ChatboxContent, "Content", FontAwesomeIcon.Smile, chatboxTab.DrawContent),
            MakeItem(PageIds.ChatboxTranslation, "Translation", FontAwesomeIcon.Language, chatboxTab.DrawTranslation),
            MakeItem(PageIds.ChatboxStorage, "Storage", FontAwesomeIcon.Database, chatboxTab.DrawStorage),
        };

        var watchers = new List<NavItem>();

        foreach (var (label, draw) in watchersTab.SubTabs ?? Array.Empty<(string, Action)>())
        {
            watchers.Add(new NavItem
            {
                Id = $"watchers/{label}",
                Label = label,
                Icon = SubTabIcons.TryGetValue(label, out var icon) ? icon : FontAwesomeIcon.Eye,
                Draw = draw,
                Subtitle = PageSubtitles.TryGetValue(label, out var subtitle) ? subtitle : string.Empty,
                OwnHeader = OwnHeaderPages.Contains($"watchers/{label}"),
            });
        }

        var activity = new List<NavItem>();

        foreach (var (label, draw) in activityTab.SubTabs ?? Array.Empty<(string, Action)>())
        {
            activity.Add(new NavItem
            {
                Id = $"activity/{label}",
                Label = label,
                Icon = SubTabIcons.TryGetValue(label, out var icon) ? icon : FontAwesomeIcon.WaveSquare,
                Draw = draw,
                Subtitle = PageSubtitles.TryGetValue(label, out var subtitle) ? subtitle : string.Empty,
                OwnHeader = OwnHeaderPages.Contains($"activity/{label}"),
            });
        }

        var integration = new List<NavItem>
        {
            MakeItem(PageIds.PartyRadar, "Party Radar", FontAwesomeIcon.Crosshairs, partyRadarTab.Draw),
            MakeItem(PageIds.SlashCommands, "Slash Commands", FontAwesomeIcon.Code, slashCommandsTab.Draw),
        };

        var system = new List<NavItem>
        {
            MakeItem(PageIds.Settings, "Settings", FontAwesomeIcon.Cog, settingsTab.Draw),
        };

#if DEBUG || CORDI_DEV
        system.Add(MakeItem(PageIds.Debug, "Debug", FontAwesomeIcon.Bug, debugTab.Draw));
#endif

        if (plugin.IsLogsTabVisible)
            system.Add(MakeItem(PageIds.Logs, "Logs", FontAwesomeIcon.FileAlt, logsTab.Draw));

        return new List<NavSection>
        {
            new() { Label = "Communication", Items = communication },
            new() { Label = "Chatbox", Items = chatbox },
            new() { Label = "Watchers", Items = watchers },
            new() { Label = "Activity", Items = activity },
            new() { Label = "Integration", Items = integration },
            new() { Label = "System", Items = system },
        };
    }

    private static NavItem MakeItem(string id, string label, FontAwesomeIcon icon, Action draw) => new()
    {
        Id = id,
        Label = label,
        Icon = icon,
        Draw = draw,
        Subtitle = PageSubtitles.TryGetValue(id, out var subtitle) ? subtitle : string.Empty,
        OwnHeader = OwnHeaderPages.Contains(id),
    };

    private ResolvedPage? ResolvePage(IReadOnlyList<NavSection> sections, string pageId)
    {
        var item = sections.SelectMany(s => s.Items).FirstOrDefault(i => i.Id == pageId)
                   ?? sections.SelectMany(s => s.Items).FirstOrDefault();

        if (item == null)
            return null;

        selectedPageId = item.Id;

        return new ResolvedPage
        {
            HeaderTitle = item.HeaderTitle,
            Subtitle = item.Subtitle,
            Draw = item.Draw,
            OwnHeader = item.OwnHeader,
        };
    }

    private Tabs.ConfigTabBase? ResolveTab(string pageId) => pageId switch
    {
        PageIds.ChatboxOverview => chatboxTab,
        PageIds.ChatboxChannels => chatboxTab,
        PageIds.Conversations => chatboxTab,
        PageIds.ChatboxAppearance => chatboxTab,
        PageIds.ChatboxMentions => chatboxTab,
        PageIds.ChatboxContent => chatboxTab,
        PageIds.ChatboxTranslation => chatboxTab,
        PageIds.ChatboxStorage => chatboxTab,
        PageIds.PartyRadar => partyRadarTab,
        PageIds.SlashCommands => slashCommandsTab,
        PageIds.Settings => settingsTab,
#if DEBUG || CORDI_DEV
        PageIds.Debug => debugTab,
#endif
        _ => null,
    };

    private SidebarFooterState BuildFooterState()
    {
        bool busy = plugin.Discord.IsBusy;
        bool connected = plugin.DiscordConnection.IsConnected;

        return new SidebarFooterState
        {
            BotOnline = connected,
            BotStatusText = busy ? "Connecting..." : connected ? "Connected" : "Disconnected",
            OnBotCardClicked = ToggleBot,
            OnDiscordClicked = () => OpenUrl(DiscordUrl),
            OnGitHubClicked = () => OpenUrl(GitHubUrl),
            OnDocsClicked = () => OpenUrl(DocsUrl),
        };
    }

    private void ToggleBot()
    {
        if (plugin.Discord.IsBusy)
            return;

        if (plugin.DiscordConnection.IsConnected)
            _ = plugin.Discord.Stop();
        else
            _ = plugin.Discord.Start();
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            plugin.LogService.Error("UI", $"Failed to open {url}", ex);
        }
    }
}
