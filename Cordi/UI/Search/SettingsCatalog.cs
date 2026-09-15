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
        RegisterWatchers(index);
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

        Page(index, PageIds.Conversations, "Conversations", FontAwesomeIcon.Envelope,
            ("", "Conversations", "tell dm direct message whisper per person"),
            ("", "Find a conversation", "filter search correspondent"),
            ("", "Sort the list", "sort order recent name world message count"),
            ("", "Discord thread", "conversation forum thread mapping unlink"),
            ("Routing", "Where tells go", "conversations only channels both routing"),
            ("Routing", "Open on an incoming tell", "auto open tab"),
            ("Routing", "Open on an outgoing tell", "auto open tab"),
            ("Routing", "Focus the input after an outgoing tell", "keyboard cursor"),
            ("Routing", "Reopen tabs on login", "restore conversations startup"),
            ("Routing", "Hide tells from the game chat log", "suppress game log"),
            ("Alerts", "Play a sound on a new tell", "conversation sound effect custom wav mp3"),
            ("Alerts", "Mute the game's own tell sound", "silence tell"),
            ("Alerts", "Flash the game in the Windows taskbar", "taskbar flash alert"),
            ("Alerts", "Bring the game to the foreground", "focus game window"),
            ("Appearance", "Every conversation gets its own window", "pop out separate window"),
            ("Appearance", "Section label", "channel list heading"),
            ("Appearance", "Conversation colour", "tab rail tile colour"),
            ("Appearance", "Flash the tab while unread", "pulse blink accent"),
            ("History", "Messages shown on open", "history window"),
            ("History", "Messages per \"Load older\"", "history page size"),
            ("XIVInstantMessenger", "Import when a conversation opens", "xivim messenger migrate old chatlog"),
            ("XIVInstantMessenger", "Log folder", "xivim messenger path folder"),
            ("XIVInstantMessenger", "Import everything now", "xivim messenger bulk port history"));

        Page(index, "chats/Custom Avatars", "Custom Avatars", FontAwesomeIcon.UserCircle,
            ("", "Character Avatars", "avatar portrait webhook image"));

        Page(index, PageIds.AdvertisementFilter, "Advertisement Filter", FontAwesomeIcon.Filter,
            ("", "Filtered chat types", "advertisement scope channel mapping"),
            ("Detection", "Detection threshold", "score advertisement spam strict"),
            ("Patterns", "Patterns", "keyword regex weight high medium points"),
            ("Whitelist", "Whitelist", "never filter allow phrase"),
            ("Test a message", "Test a message", "score preview evaluate advertisement"));
    }

    private static void RegisterChatbox(SettingsSearchIndex index)
    {
        Page(index, PageIds.ChatboxOverview, "Overview", FontAwesomeIcon.CommentAlt,
            ("", "Enable Chatbox", "open window toggle master"),
            ("", "Open on Login", "startup autostart"),
            ("", "Hide when not logged in", "login visibility"),
            ("", "Hide Title Bar", "window chrome"),
            ("", "Lock Position", "window move"),
            ("", "Lock Size", "window resize"),
            ("", "Ignore ESC", "escape key close"),
            ("", "Click Through when unfocused", "mouse passthrough"),
            ("", "Hide in Gpose", "group pose camera visibility"),
            ("", "Hide in Cutscene", "cutscene scene visibility"),
            ("", "Background Opacity", "transparency alpha"),
            ("", "Hide Game Chat", "game chat log"),
            ("", "Keep Focus after Send", "input focus"),
            ("", "Split long Messages", "character limit truncate 480"));

        Page(index, PageIds.ChatboxChannels, "Channels", FontAwesomeIcon.Hashtag,
            ("", "Channels", "add channel game chat reorder drag"),
            ("", "Game Chats", "chat type say shout linkshell party"),
            ("", "Send as", "outgoing chat type linkshell pin"),
            ("", "Rail Label", "short label navigation"),
            ("", "Icon URL", "channel icon image"),
            ("", "Mute Notifications", "channel silent"),
            ("", "Treat every Message as Mention", "channel ping"),
            ("", "Hide Advertisements", "spam filter per channel"),
            ("", "Jump to first unread Message", "scroll unread divider per channel"),
            ("", "Save History", "persist history per channel"),
            ("", "Messages loaded", "history buffer window per channel"),
            ("", "Delete History", "clear channel messages"));

        Page(index, PageIds.ChatboxAppearance, "Appearance", FontAwesomeIcon.PaintRoller,
            ("", "Message Design", "cozy compact layout"),
            ("", "Navigation Style", "sidebar rail tabs"),
            ("", "Navigation Side", "sidebar left right"),
            ("", "Navigation Width", "sidebar size"),
            ("", "Drag to resize Navigation", "sidebar resize"),
            ("", "Rail Width", "navigation rail"),
            ("", "Rail Icon Size", "navigation rail icon"),
            ("", "Tab Position", "tabs layout"),
            ("", "Tab Width", "tabs layout"),
            ("", "Show Avatars", "profile picture"),
            ("", "Round Avatars", "circle profile picture"),
            ("", "Avatar Size", "profile picture scale"),
            ("", "Lodestone Portraits for Game Chat", "lodestone portrait avatar"),
            ("", "Group consecutive Messages", "compact grouping"),
            ("", "Grouping Window", "compact grouping seconds"),
            ("", "Message Spacing", "layout density"),
            ("", "Line Spacing", "layout density"),
            ("", "Timestamps", "time clock"),
            ("", "Name Style", "author display"),
            ("", "Colour Names by Channel", "author color"),
            ("", "Show New Messages Divider", "unread divider"),
            ("", "Show Hover Toolbar", "message actions"),
            ("", "Auto Scroll", "follow newest message"),
            ("", "Mention Colour", "ping highlight color"),
            ("", "Mention Highlight", "ping background"),
            ("", "Badge Colour", "unread badge color"),
            ("", "Link Colour", "url style"),
            ("", "Outline Links", "url style"));

        Page(index, PageIds.ChatboxMentions, "Mentions", FontAwesomeIcon.At,
            ("", "Enable Mentions", "ping notify"),
            ("", "Highlight @everyone", "ping everyone"),
            ("", "Highlight @here", "ping here"),
            ("", "Highlight my Character Name", "ping name"),
            ("", "Highlight single Name Parts", "ping name first last"),
            ("", "Treat Tells as Mention", "ping tell"),
            ("", "Mention Keywords", "ping trigger word"),
            ("", "Show Unread Dot", "unread indicator"),
            ("", "Show Mention Count Badge", "unread badge"),
            ("", "Global Read", "shared read state across tabs"),
            ("", "Show Count in Window Title", "unread title"),
            ("", "Dalamud Notification on Mention", "toast notification"),
            ("", "Dalamud Notification on any Message", "toast notification"),
            ("", "Play Sound on Mention", "ping sound alert"),
            ("", "Mention Sound", "ping sound file wav"),
            ("", "Mention Sound Volume", "ping sound"),
            ("", "Enable Replies", "reply thread"),
            ("", "Show Reply Preview above Message", "reply preview"),
            ("", "Reply Excerpt Length", "reply preview"),
            ("", "Game Reply Format", "reply in game format"));

        Page(index, PageIds.ChatboxContent, "Content", FontAwesomeIcon.Smile,
            ("", "Render Discord Custom Emotes", "guild emote"),
            ("", "Render Unicode Emoji as Images", "twemoji"),
            ("", "Render Shortcodes", "emoji shortcode"),
            ("", "Resolve Guild Emotes by Name", "guild emote"),
            ("", "Jumbo Emotes when Message is Emote-only", "big emote"),
            ("", "Emote Scale", "emote size"),
            ("", "Jumbo Emote Scale", "big emote size"),
            ("", "Twemoji Base URL", "emoji cdn"),
            ("", "Show Picker Button", "emoji selector"),
            ("", "Recent Emoji to Keep", "emoji history"),
            ("", "Offer Emotes seen in Chat", "emote suggestions"),
            ("", "Relay Discord Emotes as Links", "emote game chat"),
            ("", "Favourites & Recents", "emoji history reset"),
            ("", "Seen Emotes", "emote suggestions reset"),
            ("", "Show Link Embeds", "url preview"),
            ("", "Show Embed Images", "url preview image"),
            ("", "Also embed filtered Advertisements", "url preview spam"),
            ("", "Hide Links of shown Media", "url preview cleanup"),
            ("", "Max Embeds per Message", "url preview limit"),
            ("", "Embed Width", "url preview size"),
            ("", "Embed Image Height", "url preview size"),
            ("", "Keep Embeds for", "url preview cache days"),
            ("", "Embed Cache", "url preview cache reset"));

        Page(index, PageIds.ChatboxTranslation, "Translation", FontAwesomeIcon.Language,
            ("", "Auto Translate", "translate foreign chat off automatic manual both"),
            ("", "Service", "provider deepl llm bing google machine translator"),
            ("", "DeepL API Key", "deepl key free pro"),
            ("", "Pro Account", "deepl pro endpoint"),
            ("", "Endpoint", "llm openai compatible url"),
            ("", "API Key", "llm bearer token"),
            ("", "Model", "llm model name"),
            ("", "Custom Prompt", "llm prompt"),
            ("", "Send recent Chat as Context", "llm context history"),
            ("", "Test the Service", "translation test sample"),
            ("", "Translate into", "target language"),
            ("", "What to translate", "source mode foreign selected languages"),
            ("", "Language Detection", "local online lingua detector"),
            ("", "Detection Confidence", "lingua reliability threshold local detection"),
            ("", "Local Detector", "lingua models phrases status"),
            ("", "Chat Types", "translation channels say shout party tell"),
            ("", "Translate my own Messages", "own macros translation"),
            ("", "Translate in Duties", "instance duty translation"),
            ("", "Skip Macro Spam", "macro spam translation"),
            ("", "Skip filtered Advertisements", "advertisement translation"),
            ("", "Skip Chat Noise", "hahaha lol phrase filter noise laughter emotes"),
            ("", "Translate Discord Messages", "discord relay translation"),
            ("", "Minimum Length", "short messages translation"),
            ("", "Show Translation", "display below replace tooltip"),
            ("", "Show detected Language", "source language chip"),
            ("", "Translation Colour", "translated text color"),
            ("", "Translate Button in the Chatbox", "outgoing translate button"),
            ("", "Auto Translate my Messages", "outgoing automatic translation"),
            ("", "Translate my Message into", "outgoing language"),
            ("", "Remember Translations", "translation cache"),
            ("", "Cache Size", "translation cache entries"),
            ("", "Parallel Requests", "translation concurrency"),
            ("", "Request Timeout", "translation timeout"),
            ("", "This Session", "translation statistics"),
            ("", "Clear Cache", "translation cache reset"));

        Page(index, PageIds.ChatboxSearch, "Search", FontAwesomeIcon.Search,
            ("", "Message Search", "find history keyword player date"),
            ("", "Search in", "field message text player name"),
            ("", "Matching", "contains whole word starts with exact regex"),
            ("", "Match case", "case sensitive"),
            ("", "Player", "author name sender"),
            ("", "Channels", "channel selection filter archived older removed history"),
            ("", "Sort", "order newest oldest player channel length"),
            ("", "Date range", "today yesterday last 7 days last 30 days this year custom"),
            ("", "From date", "between dates start"),
            ("", "To date", "between dates end"),
            ("", "From time", "between times start"),
            ("", "To time", "between times end"),
            ("", "Max results", "result limit"),
            ("", "Mentions me", "mention filter"),
            ("", "Sent by me", "own messages filter"),
            ("", "Attachments", "files images filter"),
            ("", "Links", "urls filter"),
            ("", "Filtered ads", "advertisement spam filter"),
            ("", "Hide types", "exclude system debug errors battle log loot emotes announcements noise"),
            ("", "Copy", "clipboard copy lines bulk"),
            ("", "Export file", "bulk export txt csv json"));

        Page(index, PageIds.ChatboxStorage, "Storage", FontAwesomeIcon.Database,
            ("", "Default Messages loaded", "history buffer window"),
            ("", "Stored History", "database size"),
            ("", "Database", "sqlite file path"),
            ("", "Enable Image Cache", "cache memory"),
            ("", "Max Cached Images", "cache memory"),
            ("", "Animate GIFs", "gif animation"),
            ("", "Animate only while focused", "gif animation performance"),
            ("", "Unload Idle GIFs after", "gif memory"),
            ("", "Clear Cache", "image cache reset"),
            ("", "Prune Cache", "image cache trim"),
            ("", "Clear all Messages", "cleanup reset"),
            ("", "Reload Channels", "rebuild channels"),
            ("", "Unload extra History", "release loaded messages memory"),
            ("", "Prune Caches", "image cache embed trim"),
            ("", "Remove Orphaned History", "cleanup orphan"),
            ("", "Compact Database", "vacuum sqlite"));
    }

    private static void RegisterWatchers(SettingsSearchIndex index)
    {
        Page(index, PageIds.Peeper, "Peeper", FontAwesomeIcon.Eye,
            ("", "Peeper detection", "target watcher looker"),
            ("", "Detect while the overlay is closed", "peeper background"),
            ("", "Log party members", "peeper filter party"),
            ("", "Log alliance members", "peeper filter alliance"),
            ("", "Combat targeters only", "peeper filter"),
            ("", "Include yourself", "peeper filter self"),
            ("", "Skip repeated alerts", "peeper cooldown"),
            ("", "Repeat cooldown", "peeper cooldown seconds"),
            ("", "Discord notifications", "peeper discord"),
            ("", "Notification channel", "peeper discord channel"),
            ("", "Sound alert", "peeper audio"),
            ("", "Alert sound", "peeper audio file"),
            ("", "Volume", "peeper audio volume"),
            ("", "Test alert", "peeper audio preview"),
            ("", "In-game overlay", "peeper overlay window"),
            ("", "Background opacity", "peeper overlay transparency"),
            ("", "Show direction arrow", "peeper overlay"),
            ("", "Show distance", "peeper overlay"),
            ("", "Show peeper's target", "peeper overlay"),
            ("", "Targeting highlight", "peeper overlay color"),
            ("", "Outline glow", "peeper overlay marker"),
            ("", "Targeting dot", "peeper overlay marker"),
            ("", "Alt-click examine", "peeper inspect"),
            ("", "Blacklist", "ignore player peeper"));

        Page(index, PageIds.EmoteLog, "Emote Log", FontAwesomeIcon.TheaterMasks,
            ("", "Emote logging", "emote tracker detection"),
            ("", "Detect while the overlay is closed", "emote background"),
            ("", "Include yourself", "emote filter self"),
            ("", "Collapse duplicates", "emote filter"),
            ("", "Discord notifications", "emote discord"),
            ("", "Notification channel", "emote discord channel"),
            ("", "In-game overlay", "emote overlay window"),
            ("", "Show reply button", "emote overlay"),
            ("", "Background opacity", "emote overlay transparency"),
            ("", "Blacklist", "ignore player emote"));

        Page(index, PageIds.CombinedOverlay, "Combined Overlay", FontAwesomeIcon.Columns,
            ("", "Open the overlay", "combined window"),
            ("", "Swap panels", "combined window layout"),
            ("", "Open on login", "combined window"),
            ("", "Lock position", "combined window"),
            ("", "Lock size", "combined window"),
            ("", "Background opacity", "combined window transparency"));
    }
    private static void RegisterActivity(SettingsSearchIndex index)
    {
        Page(index, PageIds.ActivityOverview, "Overview", FontAwesomeIcon.WaveSquare,
            ("", "Broadcast title", "activity live preview current"),
            ("", "Prefix mode", "title prefix above name"),
            ("", "Text Replacements", "replace original text"));

        Page(index, PageIds.ActivityPlaying, "Playing", FontAwesomeIcon.Gamepad,
            ("", "Report this activity", "playing game status toggle"),
            ("", "Title Format", "template placeholder format string"),
            ("", "Priority", "activity order"),
            ("", "Cycling", "rotate formats interval"),
            ("", "Character Limits", "truncate placeholder length"),
            ("", "Appearance", "title colour glow gradient"),
            ("", "Blacklist Filters", "ignore activity rule"),
            ("", "Game Overrides", "per game format override"));

        Page(index, PageIds.ActivityListening, "Listening", FontAwesomeIcon.Music,
            ("", "Report this activity", "listening music status toggle"),
            ("", "Title Format", "template placeholder format string"),
            ("", "Priority", "activity order"),
            ("", "Cycling", "rotate formats interval"),
            ("", "Character Limits", "truncate track artist length"),
            ("", "Appearance", "title colour glow gradient"),
            ("", "Blacklist Filters", "ignore activity rule"));

        Page(index, PageIds.ActivityWatching, "Watching", FontAwesomeIcon.Video,
            ("", "Report this activity", "watching video status toggle"),
            ("", "Title Format", "template placeholder format string"),
            ("", "Priority", "activity order"),
            ("", "Cycling", "rotate formats interval"),
            ("", "Character Limits", "truncate placeholder length"),
            ("", "Appearance", "title colour glow gradient"),
            ("", "Blacklist Filters", "ignore activity rule"));

        Page(index, PageIds.ActivityCustom, "Custom", FontAwesomeIcon.CommentDots,
            ("", "Report this activity", "custom status toggle"),
            ("", "Presets", "custom status preset"),
            ("", "Title Format", "template placeholder format string"),
            ("", "Cycling", "rotate formats interval"),
            ("", "Appearance", "title colour glow gradient"));
    }

    private static void RegisterParty(SettingsSearchIndex index)
    {
        Page(index, PageIds.PartyRadar, "Party Radar", FontAwesomeIcon.Crosshairs,
            ("", "Track my party", "party tracking radar"),
            ("", "Current Party", "party roster members"),
            ("", "Announce party changes", "party discord notifications"),
            ("", "Notification channel", "party discord channel"),
            ("", "Send summary now", "party discord summary"),
            ("", "Someone joins", "party trigger join"),
            ("", "Someone leaves", "party trigger leave"),
            ("", "Party fills up", "party trigger full"),
            ("", "Summary when full", "party auto summary"),
            ("", "Ignore alliance parties", "party filter alliance"),
            ("", "Include yourself", "party include self"),
            ("", "Show item level", "party gear level tomestone"),
            ("", "Show savage progress", "party progression tomestone"),
            ("", "Remember everyone I party with", "remember me players notes"),
            ("", "Remembered Players", "player notes list archive"));
    }

    private static void RegisterSlashCommands(SettingsSearchIndex index)
    {
        Page(index, PageIds.SlashCommands, "Slash Commands", FontAwesomeIcon.Code,
            ("", "Expose Cordi commands in Discord", "slash commands enable"),
            ("", "Restrict to a channel", "command channel restriction"),
            ("", "Sync everything with Discord", "register sync commands"),
            ("", "Remove every command", "unregister discord commands"),
            ("", "/cordi", "manage commands screenshot builtin"),
            ("", "/emote", "emote autocomplete builtin"),
            ("", "Your Commands", "custom slash command list"),
            ("", "Add Command", "create new slash command"),
            ("", "Add Group", "create command group"),
            ("", "Game command", "in game command mapping"),
            ("", "Parameters", "slash command options arguments type"));
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
