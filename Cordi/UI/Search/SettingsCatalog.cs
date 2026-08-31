using System;
using System.Linq;
using Cordi.UI.Windows;
using Dalamud.Interface;

namespace Cordi.UI.Search;

public static class SettingsCatalog
{
    public static void Register(SettingsSearchIndex index)
    {
        RegisterChats(index);
        RegisterChatbox(index);
        RegisterTrackers(index);
        RegisterActivity(index);
        RegisterParty(index);
        RegisterSlashCommands(index);
        RegisterSettings(index);
    }

    private static void Page(
        SettingsSearchIndex index,
        string pageId,
        string pageLabel,
        FontAwesomeIcon icon,
        params (string SubTab, string Label, string Keywords)[] entries)
    {
        foreach (var (subTab, label, keywords) in entries)
            index.RegisterSetting(pageId, pageLabel, label, subTab, keywords, icon);

        var subTabs = entries
            .Select(e => e.SubTab)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var subTab in subTabs)
        {
            if (entries.Any(e => string.Equals(e.Label, subTab, StringComparison.OrdinalIgnoreCase)))
                continue;

            index.RegisterSetting(pageId, pageLabel, subTab, subTab, "section category", icon);
        }
    }

    private static void RegisterChats(SettingsSearchIndex index)
    {
        Page(index, PageIds.ChannelMappings, "Channel Mappings", FontAwesomeIcon.Link,
            ("", "Default Channel", "fallback unmapped discord channel"),
            ("", "Say", "chat type mapping"),
            ("", "Shout", "chat type mapping"),
            ("", "Yell", "chat type mapping"),
            ("", "Party", "chat type mapping"),
            ("", "Alliance", "chat type mapping"),
            ("", "FreeCompany", "free company fc chat type mapping"),
            ("", "Tell", "direct message whisper forum mapping"),
            ("", "Linkshell", "ls chat type mapping"),
            ("", "Cross-World Linkshell", "cwls chat type mapping"),
            ("", "ExtraChat Mappings", "extrachat ecls mapping channel"),
            ("", "Sync from ExtraChat", "extrachat sync import"),
            ("", "Tell notification", "announce incoming tell"),
            ("", "Notification channel", "tell notification target"),
            ("", "Conversation cooldown", "tell notification seconds"));

        Page(index, PageIds.ActiveConversations, "Active Conversations", FontAwesomeIcon.Comments,
            ("", "Tell forum channel", "conversation forum thread mapping"),
            ("", "Conversations", "conversation thread correspondent unlink"),
            ("", "Sort conversations", "sort order name world message count"));

        Page(index, "chats/Custom Avatars", "Custom Avatars", FontAwesomeIcon.UserCircle,
            ("", "Character Avatars", "avatar portrait webhook image"));

        Page(index, "chats/Advertisement Filter", "Advertisement Filter", FontAwesomeIcon.Filter,
            ("", "Detection Threshold", "score advertisement spam"),
            ("", "High-Score Keywords", "advertisement keyword"),
            ("", "High-Score Regex Patterns", "advertisement regex"),
            ("", "Medium-Score Keywords", "advertisement keyword"),
            ("", "Medium-Score Regex Patterns", "advertisement regex"));
    }

    private static void RegisterChatbox(SettingsSearchIndex index)
    {
        Page(index, PageIds.Chatbox, "Chatbox", FontAwesomeIcon.CommentAlt,
            ("General", "Enable Chatbox", "open window toggle"),
            ("General", "Hide Game Chat", "game chat log"),
            ("General", "Open on Login", "startup autostart"),
            ("General", "Hide when not logged in", "login visibility"),
            ("General", "Click Through when unfocused", "mouse passthrough"),
            ("General", "Hide Title Bar", "window chrome"),
            ("General", "Lock Position", "window move"),
            ("General", "Lock Size", "window resize"),
            ("General", "Ignore ESC", "escape key close"),
            ("General", "Background Opacity", "transparency alpha"),
            ("General", "Show Input Bar", "message input"),
            ("General", "Capture Enter Key", "input focus send"),
            ("General", "Keep Focus after Send", "input focus"),
            ("General", "Clear Input after Send", "input reset"),
            ("General", "Max Input Length", "character limit"),
            ("General", "Max Messages per Channel", "history buffer"),
            ("Appearance", "Show Avatars", "profile picture"),
            ("Appearance", "Round Avatars", "circle profile picture"),
            ("Appearance", "Avatar Size", "profile picture scale"),
            ("Appearance", "Use Lodestone Portraits for Game Chat", "lodestone portrait avatar"),
            ("Appearance", "Timestamps", "time clock"),
            ("Appearance", "Name Style", "author display"),
            ("Appearance", "Color Names by Channel", "author color"),
            ("Appearance", "Group consecutive Messages", "compact grouping"),
            ("Appearance", "Line Spacing", "layout density"),
            ("Appearance", "Message Spacing", "layout density"),
            ("Appearance", "Show Hover Toolbar", "message actions"),
            ("Appearance", "Hide Advertisements", "spam filter"),
            ("Appearance", "Navigation Side", "sidebar left right"),
            ("Appearance", "Navigation Width", "sidebar size"),
            ("Appearance", "Drag to resize Navigation", "sidebar resize"),
            ("Appearance", "Rail Width", "navigation rail"),
            ("Appearance", "Rail Icon Size", "navigation rail icon"),
            ("Appearance", "Tab Orientation", "tabs layout"),
            ("Appearance", "Tab Position", "tabs layout"),
            ("Appearance", "Show combined Channel", "merged channel"),
            ("Appearance", "Combined Channel Name", "merged channel label"),
            ("Appearance", "Auto Scroll", "follow newest message"),
            ("Channels", "Channel ID", "discord channel id"),
            ("Mentions", "Enable Mentions", "ping notify"),
            ("Mentions", "My Discord User ID", "ping self id"),
            ("Mentions", "Mention Keywords", "ping trigger word"),
            ("Mentions", "Highlight my Character Name", "ping name"),
            ("Mentions", "Treat Tells as Mention", "ping tell"),
            ("Mentions", "Mention Color", "ping highlight color"),
            ("Mentions", "Mention Highlight", "ping background"),
            ("Mentions", "Show Mention Count Badge", "unread badge"),
            ("Mentions", "Badge Color", "unread badge color"),
            ("Mentions", "Show Count in Window Title", "unread title"),
            ("Mentions", "Show Unread Dot", "unread indicator"),
            ("Mentions", "Play Sound on Mention", "ping sound alert"),
            ("Mentions", "Mention Sound Volume", "ping sound"),
            ("Mentions", "Dalamud Notification on Mention", "toast notification"),
            ("Mentions", "Dalamud Notification on any Message", "toast notification"),
            ("Mentions", "Enable Replies", "reply thread"),
            ("Mentions", "Reply Excerpt Length", "reply preview"),
            ("Mentions", "Show Reply Preview above Message", "reply preview"),
            ("Mentions", "Game Reply Format", "reply in game format"),
            ("Mentions", "Show Link Embeds", "url preview"),
            ("Mentions", "Embed Width", "url preview size"),
            ("Mentions", "Embed Image Height", "url preview size"),
            ("Mentions", "Max Embeds per Message", "url preview limit"),
            ("Mentions", "Show Embed Images", "url preview image"),
            ("Mentions", "Hide Links of shown Media", "url preview cleanup"),
            ("Mentions", "Outline Links", "url style"),
            ("Mentions", "Link Color", "url style"),
            ("Emotes", "Emoji Picker", "emoji selector"),
            ("Emotes", "Show Picker Button", "emoji selector"),
            ("Emotes", "Recent Emoji to Keep", "emoji history"),
            ("Emotes", "Clear Favorites & Recents", "emoji history reset"),
            ("Emotes", "Render Unicode Emoji as Images", "twemoji"),
            ("Emotes", "Twemoji Base URL", "emoji cdn"),
            ("Emotes", "Render Discord Custom Emotes", "guild emote"),
            ("Emotes", "Resolve Guild Emotes by Name", "guild emote"),
            ("Emotes", "Relay Discord Emotes as Links", "emote game chat"),
            ("Emotes", "Emote Scale", "emote size"),
            ("Emotes", "Jumbo Emotes when Message is Emote-only", "big emote"),
            ("Emotes", "Jumbo Emote Scale", "big emote size"),
            ("Emotes", "Animate GIFs", "gif animation"),
            ("Emotes", "Animate only while focused", "gif animation performance"),
            ("Emotes", "Unload Idle GIFs after", "gif memory"),
            ("Emotes", "Enable Image Cache", "cache memory"),
            ("Emotes", "Max Cached Images", "cache memory"),
            ("Emotes", "Offer Emotes seen in Chat", "emote suggestions"),
            ("Emotes", "Forget Seen Emotes", "emote suggestions reset"),
            ("Advanced", "Save Messages to Disk", "persist history"),
            ("Advanced", "Default Messages per Channel", "history buffer"),
            ("Advanced", "Maintenance", "cleanup reset"));
    }

    private static void RegisterTrackers(SettingsSearchIndex index)
    {
        Page(index, PageIds.Trackers, "Trackers", FontAwesomeIcon.ChartBar,
            ("Peeper", "Enable Peeper Detection", "target watcher looker"),
            ("Peeper", "Enable Discord Notifications", "peeper discord"),
            ("Peeper", "Detect when Window is Closed", "peeper background"),
            ("Peeper", "Unhide lookers from Visibility plugin", "visibility plugin"),
            ("Peeper", "Combat targeters only", "peeper filter"),
            ("Peeper", "Include yourself", "peeper filter self"),
            ("Peeper", "Log party members", "peeper filter party"),
            ("Peeper", "Log alliance members", "peeper filter alliance"),
            ("Peeper", "Enable Sound Alert", "peeper audio"),
            ("Peeper", "Primary Sound Driver", "peeper audio device"),
            ("Peeper", "Enable Window", "peeper overlay"),
            ("Peeper", "Overlay Display", "peeper overlay layout"),
            ("Peeper", "Show Distance", "peeper overlay"),
            ("Peeper", "Show Direction Arrow", "peeper overlay"),
            ("Peeper", "Show Dot on Targeting Players", "peeper overlay marker"),
            ("Peeper", "Outline Glow on Targeting Players", "peeper overlay marker"),
            ("Peeper", "Alt-click Examine", "peeper inspect"),
            ("Peeper", "Peeper Blacklist", "ignore player"),
            ("Emote Log", "Enable Emote Detection", "emote tracker"),
            ("Emote Log", "Enable Discord Notifications", "emote discord"),
            ("Emote Log", "Detect when Window is Closed", "emote background"),
            ("Emote Log", "Include Self", "emote filter self"),
            ("Emote Log", "Collapse Duplicates", "emote filter"),
            ("Emote Log", "Enable Window", "emote overlay"),
            ("Emote Log", "Show Reply Button", "emote overlay"),
            ("Emote Log", "Emote Log Blacklist", "ignore player emote"),
            ("Combined Window", "In-Game Overlay", "combined window overlay"),
            ("Combined Window", "Open Window Now", "combined window"));
    }

    private static void RegisterActivity(SettingsSearchIndex index)
    {
        Page(index, PageIds.Activity, "Activity", FontAwesomeIcon.WaveSquare,
            ("General", "Enable Discord Activity Integration", "rich presence toggle"),
            ("General", "Prefix Mode", "presence prefix title"),
            ("General", "Text Replacements", "replace original text"),
            ("General", "Character Limits", "presence length truncate"),
            ("Playing", "Playing", "rich presence playing game status"),
            ("Playing", "Format String", "presence template placeholder"),
            ("Playing", "Cycling Mode", "presence rotate formats"),
            ("Playing", "Priority", "presence order"),
            ("Playing", "Title Colors", "presence color gradient glow"),
            ("Playing", "Game Specific Overrides", "per game format override"),
            ("Listening", "Listening", "rich presence listening to status"),
            ("Listening", "Format String", "presence template placeholder"),
            ("Listening", "Cycling Mode", "presence rotate formats"),
            ("Listening", "Priority", "presence order"),
            ("Listening", "Title Colors", "presence color gradient glow"),
            ("Watching", "Watching", "rich presence watching status"),
            ("Watching", "Format String", "presence template placeholder"),
            ("Watching", "Cycling Mode", "presence rotate formats"),
            ("Watching", "Priority", "presence order"),
            ("Watching", "Title Colors", "presence color gradient glow"),
            ("Custom", "Enable Custom Status", "custom presence status"),
            ("Custom", "Presets", "custom status preset"));
    }

    private static void RegisterParty(SettingsSearchIndex index)
    {
        Page(index, PageIds.PartyAndPlayers, "Party & Players", FontAwesomeIcon.Users,
            ("Party", "Enable Party Tracker", "party tracking"),
            ("Party", "Exclude Alliance Parties", "party filter alliance"),
            ("Party", "Send Party Summary to Discord", "party discord summary"),
            ("Party", "Notify on Party Join", "party trigger"),
            ("Party", "Notify on Party Leave", "party trigger"),
            ("Party", "Include self", "party integration"),
            ("Party", "Show gearlevel", "party item level"),
            ("Party", "Show savage progress", "party progression"),
            ("Remember Me", "Enable Remember Me", "remembered players"),
            ("Remember Me", "Remembered Players", "player notes list"),
            ("Remember Me", "Current Party", "remember party members"),
            ("Remember Me", "Notes", "player note"));
    }

    private static void RegisterSlashCommands(SettingsSearchIndex index)
    {
        Page(index, PageIds.SlashCommands, "Slash Commands", FontAwesomeIcon.Code,
            ("Commands", "Custom Slash Commands", "discord command list"),
            ("Commands", "Add New Command", "create command"),
            ("Commands", "Game Command", "in game command"),
            ("Groups", "Command Groups", "group commands"),
            ("Groups", "Add Group", "create group"),
            ("Groups", "Enable All", "group toggle"),
            ("Groups", "Disable All", "group toggle"),
            ("Settings", "Register Commands", "discord register sync"),
            ("Settings", "Unregister All", "discord remove commands"),
            ("Settings", "Any Channel", "command channel restriction"));
    }

    private static void RegisterSettings(SettingsSearchIndex index)
    {
        Page(index, PageIds.Settings, "Settings", FontAwesomeIcon.Cog,
            ("Discord", "Bot Token", "token login credentials"),
            ("Appearance", "Accent Color", "theme color violet custom"),
            ("Font", "Font Size", "scale text size"),
            ("Font", "Reset to Defaults", "font reset"));
    }
}
