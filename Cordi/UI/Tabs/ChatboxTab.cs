using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab : ConfigTabBase
{
    private string newKeyword = string.Empty;

    public ChatboxTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
    }

    public override string Label => "Chatbox";

    private ChatboxConfig Cfg => plugin.Config.Chatbox;

    protected override IReadOnlyList<(string Label, Action Draw)>? GetSubTabs() => new (string, Action)[]
    {
        ("General", DrawGeneral),
        ("Appearance", DrawAppearance),
        ("Channels", DrawChannels),
        ("Mentions", DrawMentions),
        ("Emotes", DrawEmotes),
        ("Advanced", DrawAdvanced),
    };

    private void Save() => plugin.Config.Save();

    private void Card(string id, string title, Action<float> content)
    {
        var enabled = true;
        theme.DrawPluginCardAuto(id: id, title: title, drawContent: content, enabled: ref enabled, showCheckbox: false);
    }

    private void Check(string label, Func<bool> get, Action<bool> set)
    {
        var value = get();
        if (!ImGui.Checkbox(label, ref value)) return;

        set(value);
        Save();
    }

    private void Slider(string label, Func<float> get, Action<float> set, float min, float max, string format = "%.2f")
    {
        var value = get();
        ImGui.SetNextItemWidth(240f * ImGuiHelpers.GlobalScale);
        if (!ImGui.SliderFloat(label, ref value, min, max, format)) return;

        set(value);
        Save();
    }

    private void SliderInt(string label, Func<int> get, Action<int> set, int min, int max)
    {
        var value = get();
        ImGui.SetNextItemWidth(240f * ImGuiHelpers.GlobalScale);
        if (!ImGui.SliderInt(label, ref value, min, max)) return;

        set(value);
        Save();
    }

    private void EnumField<T>(string label, Func<T> get, Action<T> set) where T : struct, Enum
    {
        var value = get();
        ImGui.SetNextItemWidth(240f * ImGuiHelpers.GlobalScale);
        if (!theme.EnumCombo($"{label}##chatbox-{label}", ref value)) return;

        set(value);
        Save();
    }

    private void Color(string label, Func<Vector4> get, Action<Vector4> set)
    {
        var value = get();
        if (!ImGui.ColorEdit4(label, ref value, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar)) return;

        set(value);
        Save();
    }

    private void TextField(string label, Func<string> get, Action<string> set, int maxLength = 128)
    {
        var value = get();
        ImGui.SetNextItemWidth(320f * ImGuiHelpers.GlobalScale);
        if (!ImGui.InputText(label, ref value, maxLength)) return;

        set(value);
        Save();
    }

    private void DrawGeneral()
    {
        Card("chatbox-general", "Chatbox", _ =>
        {
            var enabled = Cfg.Enabled;
            if (ImGui.Checkbox("Enable Chatbox", ref enabled))
            {
                Cfg.Enabled = enabled;
                Save();
                plugin.UpdateCommandVisibility();
            }

            Check("Open on Login", () => Cfg.OpenOnLogin, v => Cfg.OpenOnLogin = v);
            Check("Hide when not logged in", () => Cfg.HideWhenNotLoggedIn, v => Cfg.HideWhenNotLoggedIn = v);

            theme.SpacerY(0.5f);
            if (theme.PrimaryButton(plugin.ChatboxWindow.IsOpen ? "Close Chatbox" : "Open Chatbox"))
                plugin.ChatboxWindow.IsOpen = !plugin.ChatboxWindow.IsOpen;

            ImGui.SameLine();
            ImGui.TextDisabled("Command: /cordichat");
        });

        Card("chatbox-window", "Window Behaviour", _ =>
        {
            Check("Hide Title Bar", () => Cfg.HideTitleBar, v => Cfg.HideTitleBar = v);
            Check("Lock Position", () => Cfg.WindowLockPosition, v => Cfg.WindowLockPosition = v);
            Check("Lock Size", () => Cfg.WindowLockSize, v => Cfg.WindowLockSize = v);
            Check("Ignore ESC", () => Cfg.IgnoreEsc, v => Cfg.IgnoreEsc = v);
            Check("Click Through when unfocused", () => Cfg.ClickThroughWhenUnfocused, v => Cfg.ClickThroughWhenUnfocused = v);
            Slider("Background Opacity", () => Cfg.BackgroundOpacity, v => Cfg.BackgroundOpacity = v, 0.1f, 1f);

            theme.SpacerY(0.5f);
            Check("Hide Game Chat", () => Cfg.HideGameChat, v => Cfg.HideGameChat = v);
            ImGui.TextDisabled("Hides the vanilla chat log while the Chatbox is enabled. It comes back when you turn this off.");

            Check("Capture Enter Key", () => Cfg.CaptureEnterKey, v => Cfg.CaptureEnterKey = v);
            ImGui.TextDisabled("Enter focuses the Chatbox input instead of opening the game chat, as long as the Chatbox window is open.");
        });

        Card("chatbox-input", "Input", _ =>
        {
            Check("Show Input Bar", () => Cfg.ShowInputBar, v => Cfg.ShowInputBar = v);
            Check("Clear Input after Send", () => Cfg.ClearInputAfterSend, v => Cfg.ClearInputAfterSend = v);
            Check("Keep Focus after Send", () => Cfg.KeepFocusAfterSend, v => Cfg.KeepFocusAfterSend = v);
            SliderInt("Max Input Length", () => Cfg.MaxInputLength, v => Cfg.MaxInputLength = v, 64, 2000);
            SliderInt("Max Messages per Channel", () => Cfg.MaxMessagesPerChannel, v => Cfg.MaxMessagesPerChannel = v, 50, 5000);
        });
    }

    private void DrawAppearance()
    {
        Card("chatbox-layout", "Layout", _ =>
        {
            EnumField("Message Design", () => Cfg.Layout, v => Cfg.Layout = v);
            ImGui.TextDisabled("Cozy = Discord default, Compact = one line.");

            theme.SpacerY(0.5f);
            EnumField("Tab Orientation", () => Cfg.NavStyle, v => Cfg.NavStyle = v);
            ImGui.TextDisabled("ServerRail = Discord server icons, ChannelList = DM list, Tabs = tab bar.");

            if (Cfg.NavStyle == ChatboxNavStyle.Hidden) return;

            EnumField("Navigation Side", () => Cfg.NavSide, v => Cfg.NavSide = v);

            switch (Cfg.NavStyle)
            {
                case ChatboxNavStyle.Tabs:
                    EnumField("Tab Position", () => Cfg.TabSide, v => Cfg.TabSide = v);
                    Slider("Tab Width (0 = auto)", () => Cfg.TabWidth, v => Cfg.TabWidth = v, 0f, 260f, "%.0f");
                    break;

                case ChatboxNavStyle.ServerRail:
                    Slider("Rail Width", () => Cfg.RailWidth, v => Cfg.RailWidth = v, 40f, 200f, "%.0f");
                    Slider("Rail Icon Size", () => Cfg.RailIconSize, v => Cfg.RailIconSize = v, 20f, 96f, "%.0f");
                    Check("Drag to resize Navigation", () => Cfg.ResizableNav, v => Cfg.ResizableNav = v);
                    break;

                default:
                    Slider("Navigation Width", () => Cfg.NavWidth, v => Cfg.NavWidth = v, 90f, 360f, "%.0f");
                    Check("Drag to resize Navigation", () => Cfg.ResizableNav, v => Cfg.ResizableNav = v);
                    break;
            }
        });

        Card("chatbox-avatars", "Avatars", _ =>
        {
            Check("Show Avatars", () => Cfg.ShowAvatars, v => Cfg.ShowAvatars = v);
            Check("Round Avatars", () => Cfg.RoundAvatars, v => Cfg.RoundAvatars = v);
            Check("Use Lodestone Portraits for Game Chat", () => Cfg.ShowLodestoneAvatars, v => Cfg.ShowLodestoneAvatars = v);
            Slider("Avatar Size", () => Cfg.AvatarSize, v => Cfg.AvatarSize = v, 16f, 72f, "%.0f");
        });

        Card("chatbox-messages", "Messages", _ =>
        {
            Check("Group consecutive Messages", () => Cfg.GroupConsecutive, v => Cfg.GroupConsecutive = v);
            SliderInt("Grouping Window (seconds)", () => Cfg.GroupWindowSeconds, v => Cfg.GroupWindowSeconds = v, 30, 1800);
            Slider("Message Spacing", () => Cfg.MessageSpacing, v => Cfg.MessageSpacing = v, 0f, 24f, "%.0f");
            Slider("Line Spacing", () => Cfg.LineSpacing, v => Cfg.LineSpacing = v, 0f, 12f, "%.0f");

            theme.SpacerY(0.5f);
            EnumField("Timestamps", () => Cfg.Timestamps, v => Cfg.Timestamps = v);
            EnumField("Name Style", () => Cfg.NameStyle, v => Cfg.NameStyle = v);
            Check("Color Names by Channel", () => Cfg.ColorNamesByChannel, v => Cfg.ColorNamesByChannel = v);
            Check("Show \"New Messages\" Divider", () => Cfg.ShowNewMessageDivider, v => Cfg.ShowNewMessageDivider = v);
            Check("Show Hover Toolbar", () => Cfg.ShowHoverToolbar, v => Cfg.ShowHoverToolbar = v);
            Check("Auto Scroll", () => Cfg.AutoScroll, v => Cfg.AutoScroll = v);

            theme.SpacerY(0.5f);
            Check("Hide Advertisements", () => Cfg.FilterAdvertisements, v => Cfg.FilterAdvertisements = v);
            ImGui.TextDisabled(plugin.Config.AdvertisementFilter.Enabled
                ? "Uses the Advertisement Filter from the Chats tab. Blocked messages collapse into a placeholder you can click to reveal."
                : "The Advertisement Filter is disabled in the Chats tab, so nothing is hidden.");
        });

        Card("chatbox-combined", "Combined Channel", _ =>
        {
            Check("Show combined Channel", () => Cfg.ShowCombinedChannel, v => Cfg.ShowCombinedChannel = v);
            TextField("Combined Channel Name", () => Cfg.CombinedChannelName, v =>
            {
                Cfg.CombinedChannelName = v;
                plugin.Chatbox.RebuildChannels();
            }, 32);
        });
    }

    private void DrawMentions()
    {
        Card("chatbox-mentions", "Mentions", _ =>
        {
            Check("Enable Mentions", () => Cfg.EnableMentions, v => Cfg.EnableMentions = v);
            Check("Highlight @everyone", () => Cfg.MentionEveryone, v => Cfg.MentionEveryone = v);
            Check("Highlight @here", () => Cfg.MentionHere, v => Cfg.MentionHere = v);
            Check("Highlight my Character Name", () => Cfg.MentionOwnName, v => Cfg.MentionOwnName = v);
            Check("Highlight single Name Parts (@First or @Last)", () => Cfg.MentionOwnNameParts, v => Cfg.MentionOwnNameParts = v);
            Check("Treat Tells as Mention", () => Cfg.MentionOnTell, v => Cfg.MentionOnTell = v);
            TextField("My Discord User ID", () => Cfg.DiscordUserId, v => Cfg.DiscordUserId = v, 32);
        });

        Card("chatbox-keywords", "Mention Keywords", _ =>
        {
            ImGui.SetNextItemWidth(240f * ImGuiHelpers.GlobalScale);
            ImGui.InputTextWithHint("##chatbox-new-keyword", "Keyword...", ref newKeyword, 64);
            ImGui.SameLine();

            if (theme.PrimaryButton("Add##chatbox-keyword") && !string.IsNullOrWhiteSpace(newKeyword))
            {
                Cfg.MentionKeywords.Add(newKeyword.Trim());
                newKeyword = string.Empty;
                Save();
            }

            string? remove = null;
            foreach (var keyword in Cfg.MentionKeywords)
            {
                if (theme.DangerIconButton($"##chatbox-kw-{keyword}", FontAwesomeIcon.Times, "Remove")) remove = keyword;
                ImGui.SameLine();
                ImGui.TextUnformatted(keyword);
            }

            if (remove == null) return;

            Cfg.MentionKeywords.Remove(remove);
            Save();
        });

        Card("chatbox-notifications", "Notifications", _ =>
        {
            Check("Show Unread Dot", () => Cfg.ShowUnreadDot, v => Cfg.ShowUnreadDot = v);
            Check("Show Mention Count Badge", () => Cfg.ShowMentionBadge, v => Cfg.ShowMentionBadge = v);
            Check("Show Count in Window Title", () => Cfg.FlashTitleOnMention, v => Cfg.FlashTitleOnMention = v);
            Check("Dalamud Notification on Mention", () => Cfg.NotifyOnMention, v => Cfg.NotifyOnMention = v);
            Check("Dalamud Notification on any Message", () => Cfg.NotifyOnUnread, v => Cfg.NotifyOnUnread = v);
            Check("Play Sound on Mention", () => Cfg.PlaySoundOnMention, v => Cfg.PlaySoundOnMention = v);
            TextField("Mention Sound (.wav)", () => Cfg.MentionSoundPath, v => Cfg.MentionSoundPath = v, 260);
            Slider("Mention Sound Volume", () => Cfg.MentionSoundVolume, v => Cfg.MentionSoundVolume = v, 0f, 1f);

            theme.SpacerY(0.5f);
            Color("Mention Color", () => Cfg.MentionColor, v => Cfg.MentionColor = v);
            Color("Mention Highlight", () => Cfg.MentionHighlightColor, v => Cfg.MentionHighlightColor = v);
            Color("Badge Color", () => Cfg.UnreadBadgeColor, v => Cfg.UnreadBadgeColor = v);
            Color("Link Color", () => Cfg.LinkColor, v => Cfg.LinkColor = v);
            Check("Outline Links", () => Cfg.OutlineLinks, v => Cfg.OutlineLinks = v);
            ImGui.TextDisabled("The outline keeps links readable when the window is transparent.");
        });

        Card("chatbox-embeds", "Link Embeds", _ =>
        {
            Check("Show Link Embeds", () => Cfg.EnableLinkEmbeds, v => Cfg.EnableLinkEmbeds = v);
            Check("Show Embed Images", () => Cfg.EmbedImages, v => Cfg.EmbedImages = v);
            Check("Also embed filtered Advertisements", () => Cfg.EmbedFilteredMessages, v => Cfg.EmbedFilteredMessages = v);
            Check("Hide Links of shown Media", () => Cfg.HideMediaLinks, v => Cfg.HideMediaLinks = v);
            ImGui.TextDisabled("Images and GIFs replace their link entirely. Works for direct file links and for pages like Giphy or Klipy.");
            SliderInt("Max Embeds per Message", () => Cfg.MaxEmbedsPerMessage, v => Cfg.MaxEmbedsPerMessage = v, 0, 5);
            Slider("Embed Width", () => Cfg.EmbedMaxWidth, v => Cfg.EmbedMaxWidth = v, 200f, 720f, "%.0f");
            Slider("Embed Image Height", () => Cfg.EmbedImageMaxHeight, v => Cfg.EmbedImageMaxHeight = v, 80f, 480f, "%.0f");
            SliderInt("Keep Embeds for (days)", () => Cfg.EmbedCacheDays, v => Cfg.EmbedCacheDays = v, 1, 90);

            theme.SpacerY(0.5f);
            if (theme.Button("Clear Embed Cache##chatbox")) plugin.Chatbox.EmbedCache.Clear();
            ImGui.TextDisabled("Cordi requests each linked page directly to read its preview, which exposes your IP to that site.");
        });

        Card("chatbox-replies", "Replies", _ =>
        {
            Check("Enable Replies", () => Cfg.EnableReplies, v => Cfg.EnableReplies = v);
            Check("Show Reply Preview above Message", () => Cfg.ShowReplyPreview, v => Cfg.ShowReplyPreview = v);
            Check("Ping the Author on Discord Replies", () => Cfg.PingOnDiscordReply, v => Cfg.PingOnDiscordReply = v);
            SliderInt("Reply Excerpt Length", () => Cfg.ReplyExcerptLength, v => Cfg.ReplyExcerptLength = v, 16, 200);
            TextField("Game Reply Format", () => Cfg.GameReplyFormat, v => Cfg.GameReplyFormat = v, 128);
            ImGui.TextDisabled("Tokens: {name}, {excerpt}, {message}");
        });
    }

    private void DrawEmotes()
    {
        Card("chatbox-emotes", "Emotes", _ =>
        {
            Check("Render Discord Custom Emotes", () => Cfg.RenderCustomEmotes, v => Cfg.RenderCustomEmotes = v);
            Check("Render Unicode Emoji as Images", () => Cfg.RenderUnicodeEmoji, v => Cfg.RenderUnicodeEmoji = v);
            Check("Render :shortcodes:", () => Cfg.RenderShortcodes, v => Cfg.RenderShortcodes = v);
            Check("Resolve Guild Emotes by Name", () => Cfg.RenderGuildEmotesByName, v => Cfg.RenderGuildEmotesByName = v);
            Check("Jumbo Emotes when Message is Emote-only", () => Cfg.JumboLoneEmotes, v => Cfg.JumboLoneEmotes = v);
            Slider("Emote Scale", () => Cfg.EmoteScale, v => Cfg.EmoteScale = v, 0.8f, 3f);
            Slider("Jumbo Emote Scale", () => Cfg.JumboEmoteScale, v => Cfg.JumboEmoteScale = v, 1.5f, 6f);
            TextField("Twemoji Base URL", () => Cfg.TwemojiBaseUrl, v => Cfg.TwemojiBaseUrl = v, 260);
        });

        Card("chatbox-picker", "Emoji Picker", _ =>
        {
            Check("Show Picker Button", () => Cfg.ShowEmojiPicker, v => Cfg.ShowEmojiPicker = v);
            ImGui.TextDisabled("Adds a button left of the send button. Right-click an entry in the picker to favorite it.");

            using (ImRaii.Disabled(!Cfg.ShowEmojiPicker))
            {
                SliderInt("Recent Emoji to Keep", () => Cfg.EmojiPickerRecentLimit,
                    v => Cfg.EmojiPickerRecentLimit = v, 0, 128);

                Check("Offer Emotes seen in Chat", () => Cfg.PickerIncludeSeenEmotes,
                    v => Cfg.PickerIncludeSeenEmotes = v);
                ImGui.TextDisabled($"Every custom emote that arrives through Discord is remembered and can be sent again, even from servers the bot is not in. Currently {plugin.Chatbox.Emotes.Count} known.");
            }

            theme.SpacerY(0.5f);
            if (ImGui.Button("Clear Favorites & Recents"))
            {
                Cfg.FavoriteEmojis.Clear();
                Cfg.RecentEmojis.Clear();
                Save();
            }

            ImGui.SameLine();
            if (ImGui.Button("Forget Seen Emotes")) plugin.Chatbox.Emotes.Clear();
        });

        Card("chatbox-cache", "Image Cache", _ =>
        {
            Check("Enable Image Cache", () => Cfg.ImageCacheEnabled, v => Cfg.ImageCacheEnabled = v);
            SliderInt("Max Cached Images", () => Cfg.ImageCacheMaxEntries, v => Cfg.ImageCacheMaxEntries = v, 100, 5000);

            ImGui.TextDisabled(plugin.Chatbox.ImageCache.InspectSummary());
            ImGui.TextDisabled("Stored inside the chatbox database, deduplicated by content.");

            theme.SpacerY(0.5f);
            Check("Animate GIFs", () => Cfg.AnimateGifs, v =>
            {
                Cfg.AnimateGifs = v;
                plugin.Chatbox.ImageCache.ResetTextures();
            });
            ImGui.TextDisabled("Plays animated GIFs from links and Discord emotes. Only GIFs currently on screen are animated.");

            using (ImRaii.Disabled(!Cfg.AnimateGifs))
            {
                Check("Animate only while focused", () => Cfg.AnimateOnlyWhenFocused,
                    v => Cfg.AnimateOnlyWhenFocused = v);
                ImGui.TextDisabled("GIFs freeze on the current frame whenever the Chatbox window is not focused.");

                SliderInt("Unload Idle GIFs after", () => Cfg.AnimateIdleUnloadSeconds,
                    v => Cfg.AnimateIdleUnloadSeconds = v, 0, 300);
                ImGui.TextDisabled($"Seconds off screen before decoded frames are released. 0 keeps them in memory. Currently {plugin.Chatbox.ImageCache.AnimatedTextures} decoded.");
            }

            if (theme.Button("Clear Cache##chatbox")) plugin.Chatbox.ImageCache.Clear();

            ImGui.SameLine();
            if (theme.Button("Prune Cache##chatbox")) plugin.Chatbox.ImageCache.PruneStored(Cfg.ImageCacheMaxEntries);
        });
    }

    private void DrawAdvanced()
    {
        Card("chatbox-history", "History", _ =>
        {
            Check("Save Messages to Disk", () => Cfg.PersistHistory, v => Cfg.PersistHistory = v);
            SliderInt("Default Messages per Channel", () => Cfg.MaxMessagesPerChannel,
                v => Cfg.MaxMessagesPerChannel = v, 100, 50000);
            ImGui.TextDisabled("Used for channels that do not set their own limit, and for the combined view.");

            theme.SpacerY(0.5f);
            ImGui.TextDisabled(plugin.Chatbox.StorageSummary());
        });

        Card("chatbox-advanced", "Maintenance", _ =>
        {
            if (theme.Button("Clear all Messages##chatbox")) plugin.Chatbox.ClearAll();

            ImGui.SameLine();
            if (theme.Button("Reload Channels##chatbox")) plugin.Chatbox.RebuildChannels();

            if (theme.Button("Apply Limits now##chatbox")) plugin.Chatbox.ApplyRetention();

            ImGui.SameLine();
            if (theme.Button("Remove Orphaned History##chatbox")) plugin.Chatbox.PruneOrphanedHistory();

            ImGui.SameLine();
            if (theme.Button("Compact Database##chatbox")) plugin.Chatbox.Database.Compact();

            theme.SpacerY(0.5f);
            ImGui.TextDisabled(plugin.Chatbox.Database.FilePath);
        });
    }
}
