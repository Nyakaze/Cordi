using Cordi.UI.Themes;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab
{
    public void DrawContent()
    {
        ConsumeScroll();

        Layout.Draw("Content", "Emotes, the emoji picker and link previews.");

        Card.Draw("chatbox-emotes", innerWidth =>
        {
            DrawToggleRow(
                "chatbox-custom-emotes", FontAwesomeIcon.Smile,
                "Render Discord Custom Emotes", "Draws server emotes as images.",
                innerWidth, () => Cfg.RenderCustomEmotes, v => Cfg.RenderCustomEmotes = v);

            DrawToggleRow(
                "chatbox-unicode-emoji", FontAwesomeIcon.GrinBeam,
                "Render Unicode Emoji as Images", "Replaces plain emoji with Twemoji graphics.",
                innerWidth, () => Cfg.RenderUnicodeEmoji, v => Cfg.RenderUnicodeEmoji = v);

            DrawToggleRow(
                "chatbox-shortcodes", FontAwesomeIcon.Code,
                "Render :shortcodes:", "Turns :smile: into the matching emoji.",
                innerWidth, () => Cfg.RenderShortcodes, v => Cfg.RenderShortcodes = v);

            DrawToggleRow(
                "chatbox-guild-emotes", FontAwesomeIcon.Search,
                "Resolve Guild Emotes by Name", "Looks up known server emotes written as :name:.",
                innerWidth, () => Cfg.RenderGuildEmotesByName, v => Cfg.RenderGuildEmotesByName = v);

            DrawToggleRow(
                "chatbox-jumbo", FontAwesomeIcon.Expand,
                "Jumbo Emotes when Message is Emote-only", "Enlarges emotes in messages without text.",
                innerWidth, () => Cfg.JumboLoneEmotes, v => Cfg.JumboLoneEmotes = v);

            DrawSliderRow(
                "chatbox-emote-scale", FontAwesomeIcon.ExpandArrowsAlt,
                "Emote Scale", "Size of inline emotes relative to the text.",
                innerWidth, 0.8f, 3f, 0.05f,
                () => Cfg.EmoteScale, v => Cfg.EmoteScale = v, decimals: 2);

            DrawSliderRow(
                "chatbox-jumbo-scale", FontAwesomeIcon.ExpandArrowsAlt,
                "Jumbo Emote Scale", "Size of emotes in emote-only messages.",
                innerWidth, 1.5f, 6f, 0.05f,
                () => Cfg.JumboEmoteScale, v => Cfg.JumboEmoteScale = v, decimals: 2);

            DrawTextRow(
                "chatbox-twemoji", FontAwesomeIcon.Link,
                "Twemoji Base URL", "Source used to fetch unicode emoji images.",
                innerWidth, () => Cfg.TwemojiBaseUrl, v => Cfg.TwemojiBaseUrl = v, 260, "https://...", 320f);
        }, "Emotes");

        Card.Draw("chatbox-picker", innerWidth =>
        {
            DrawToggleRow(
                "chatbox-show-picker", FontAwesomeIcon.SmileWink,
                "Show Picker Button", "Adds a button left of the send button. Right-click an entry to favourite it.",
                innerWidth, () => Cfg.ShowEmojiPicker, v => Cfg.ShowEmojiPicker = v);

            if (Cfg.ShowEmojiPicker)
            {
                DrawIntSliderRow(
                    "chatbox-picker-recent", FontAwesomeIcon.History,
                    "Recent Emoji to Keep", "How many recently used emoji the picker remembers.",
                    innerWidth, 0, 128,
                    () => Cfg.EmojiPickerRecentLimit, v => Cfg.EmojiPickerRecentLimit = v);

                DrawToggleRow(
                    "chatbox-picker-seen", FontAwesomeIcon.Eye,
                    "Offer Emotes seen in Chat",
                    $"Custom emotes arriving through Discord are remembered and can be sent again. Currently {plugin.Chatbox.Emotes.Count} known.",
                    innerWidth, () => Cfg.PickerIncludeSeenEmotes, v => Cfg.PickerIncludeSeenEmotes = v);
            }

            DrawToggleRow(
                "chatbox-relay-emotes", FontAwesomeIcon.ExternalLinkAlt,
                "Relay Discord Emotes as Links",
                "Rewrites custom emotes into their image link before forwarding to game chat. Off forwards the shorter :name: form.",
                innerWidth, () => Cfg.RelayEmotesAsUrls, v => Cfg.RelayEmotesAsUrls = v);

            DrawActionRow(
                "chatbox-clear-favorites", FontAwesomeIcon.Star, UiTheme.TileAmber,
                "Favourites & Recents", $"{Cfg.FavoriteEmojis.Count} favourites, {Cfg.RecentEmojis.Count} recent.",
                "Clear", innerWidth,
                () =>
                {
                    Cfg.FavoriteEmojis.Clear();
                    Cfg.RecentEmojis.Clear();
                    Save();
                });

            DrawActionRow(
                "chatbox-forget-emotes", FontAwesomeIcon.EyeSlash, UiTheme.TileRed,
                "Seen Emotes", $"{plugin.Chatbox.Emotes.Count} emotes remembered from chat.",
                "Forget", innerWidth,
                () => plugin.Chatbox.Emotes.Clear());
        }, "Emoji Picker");

        Card.Draw("chatbox-embeds", innerWidth =>
        {
            DrawToggleRow(
                "chatbox-enable-embeds", FontAwesomeIcon.Link,
                "Show Link Embeds", "Fetches a title and preview for links.",
                innerWidth, () => Cfg.EnableLinkEmbeds, v => Cfg.EnableLinkEmbeds = v);

            DrawToggleRow(
                "chatbox-embed-images", FontAwesomeIcon.Image,
                "Show Embed Images", "Draws the preview image of an embed.",
                innerWidth, () => Cfg.EmbedImages, v => Cfg.EmbedImages = v);

            DrawToggleRow(
                "chatbox-embed-filtered", FontAwesomeIcon.Filter,
                "Also embed filtered Advertisements", "Builds embeds even for messages the ad filter hid.",
                innerWidth, () => Cfg.EmbedFilteredMessages, v => Cfg.EmbedFilteredMessages = v);

            DrawToggleRow(
                "chatbox-hide-media-links", FontAwesomeIcon.EyeSlash,
                "Hide Links of shown Media",
                "Images and GIFs replace their link entirely. Works for direct files and for pages like Giphy or Klipy.",
                innerWidth, () => Cfg.HideMediaLinks, v => Cfg.HideMediaLinks = v);

            DrawIntSliderRow(
                "chatbox-max-embeds", FontAwesomeIcon.Sort,
                "Max Embeds per Message", "0 disables embeds for that message.",
                innerWidth, 0, 5,
                () => Cfg.MaxEmbedsPerMessage, v => Cfg.MaxEmbedsPerMessage = v);

            DrawSliderRow(
                "chatbox-embed-width", FontAwesomeIcon.Ruler,
                "Embed Width", "Maximum width of an embed card.",
                innerWidth, 200f, 720f, 1f,
                () => Cfg.EmbedMaxWidth, v => Cfg.EmbedMaxWidth = v);

            DrawSliderRow(
                "chatbox-embed-height", FontAwesomeIcon.RulerVertical,
                "Embed Image Height", "Maximum height of an embed image.",
                innerWidth, 80f, 480f, 1f,
                () => Cfg.EmbedImageMaxHeight, v => Cfg.EmbedImageMaxHeight = v);

            DrawIntSliderRow(
                "chatbox-embed-days", FontAwesomeIcon.CalendarAlt,
                "Keep Embeds for", "How long a fetched preview stays cached.",
                innerWidth, 1, 90,
                () => Cfg.EmbedCacheDays, v => Cfg.EmbedCacheDays = v, " d");

            DrawActionRow(
                "chatbox-clear-embeds", FontAwesomeIcon.Trash, UiTheme.TileRed,
                "Embed Cache", "Cordi requests each linked page directly to read its preview, which exposes your IP to that site.",
                "Clear Cache", innerWidth,
                () => plugin.Chatbox.EmbedCache.Clear());
        }, "Link Embeds");
    }
}
