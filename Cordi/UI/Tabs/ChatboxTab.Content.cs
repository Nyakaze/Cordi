using System.Numerics;
using Cordi.Configuration;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab
{
    public void DrawContent()
    {
        ConsumeScroll();

        Layout.Draw("Content", "Emotes, the emoji and GIF pickers and link previews.");

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

        Card.Draw("chatbox-animation", innerWidth =>
        {
            DrawToggleRow(
                "chatbox-animate-gifs", FontAwesomeIcon.Film,
                "Animate GIFs", "Plays animated GIFs from links and Discord emotes. Only GIFs on screen are animated.",
                innerWidth,
                () => Cfg.AnimateGifs,
                v =>
                {
                    Cfg.AnimateGifs = v;
                    plugin.Chatbox.ImageCache.ResetTextures();
                });

            if (!Cfg.AnimateGifs) return;

            DrawToggleRow(
                "chatbox-animate-focused", FontAwesomeIcon.Pause,
                "Animate only while focused", "GIFs freeze on the current frame while the window is not focused.",
                innerWidth, () => Cfg.AnimateOnlyWhenFocused, v => Cfg.AnimateOnlyWhenFocused = v);

            DrawIntSliderRow(
                "chatbox-animate-unload", FontAwesomeIcon.Stopwatch,
                "Unload Idle GIFs after",
                $"Seconds off screen before decoded frames are released. 0 keeps them in memory. Currently {plugin.Chatbox.ImageCache.AnimatedTextures} decoded.",
                innerWidth, 0, 300,
                () => Cfg.AnimateIdleUnloadSeconds, v => Cfg.AnimateIdleUnloadSeconds = v, " s");
        }, "Animation");

        Card.Draw("chatbox-picker", innerWidth =>
        {
            DrawToggleRow(
                "chatbox-show-picker", FontAwesomeIcon.SmileWink,
                "Show Picker Button", "Adds a button left of the send button. Right-click an entry to favourite it.",
                innerWidth, () => Cfg.ShowEmojiPicker, v => Cfg.ShowEmojiPicker = v);

            if (Cfg.ShowEmojiPicker)
            {
                DrawOptionRow(
                    "chatbox-picker-position", FontAwesomeIcon.ArrowsAlt,
                    "Picker Position",
                    "Which side of the chatbox the emoji picker opens on. Left and right match the chatbox height, top and bottom its width. Drag the picker corner to resize it.",
                    innerWidth, () => Cfg.EmojiPickerPosition,
                    v =>
                    {
                        Cfg.EmojiPickerPosition = v;
                        Cfg.EmojiPickerSize = Vector2.Zero;
                    },
                    Options(
                        (ChatboxEmojiPickerPosition.Left, "Left"),
                        (ChatboxEmojiPickerPosition.Right, "Right"),
                        (ChatboxEmojiPickerPosition.Top, "Top"),
                        (ChatboxEmojiPickerPosition.Bottom, "Bottom")));

                DrawIntSliderRow(
                    "chatbox-picker-recent", FontAwesomeIcon.History,
                    "Recent Emoji to Keep", "How many recently used emoji the picker remembers.",
                    innerWidth, 0, 128,
                    () => Cfg.EmojiPickerRecentLimit, v => Cfg.EmojiPickerRecentLimit = v);

                DrawToggleRow(
                    "chatbox-picker-seen", FontAwesomeIcon.Eye,
                    "Offer Own Emotes",
                    $"Custom emotes you sent yourself are remembered and can be sent again. Currently {plugin.Chatbox.Emotes.Count} known.",
                    innerWidth, () => Cfg.PickerIncludeSeenEmotes, v => Cfg.PickerIncludeSeenEmotes = v);

                if (Cfg.PickerIncludeSeenEmotes)
                {
                    DrawToggleRow(
                        "chatbox-picker-others", FontAwesomeIcon.Users,
                        "Show Others' Emotes",
                        "Adds an \"Others Emojis\" category with custom emotes other people used in chat.",
                        innerWidth, () => Cfg.ShowOthersEmotes, v => Cfg.ShowOthersEmotes = v);
                }
            }

            DrawToggleRow(
                "chatbox-relay-emotes", FontAwesomeIcon.ExternalLinkAlt,
                "Relay Discord Emotes as Links",
                "Rewrites custom emotes into their image link before forwarding to game chat. Off forwards the shorter :name: form.",
                innerWidth, () => Cfg.RelayEmotesAsUrls, v => Cfg.RelayEmotesAsUrls = v);

            DrawToggleRow(
                "chatbox-emoticons", FontAwesomeIcon.Smile,
                "Send Emoticons to Game Chat",
                "Writes common emoji as :P or :) instead of :stuck_out_tongue: when forwarding to game chat.",
                innerWidth, () => Cfg.UseEmoticonsInGameChat, v => Cfg.UseEmoticonsInGameChat = v);

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
                "Remembered Emotes", $"{plugin.Chatbox.Emotes.Count} custom emotes remembered from chat.",
                "Forget", innerWidth,
                () => plugin.Chatbox.Emotes.Clear());
        }, "Emoji Picker");

        Card.Draw(
            "chatbox-gif-picker",
            DrawGifPicker,
            "GIF Picker",
            anchor => theme.HelpPill("chatbox-giphy-help", anchor, GiphyHelp, "Getting a key"));

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

    private bool giphyKeyActive;

    private void DrawGifPicker(float innerWidth)
    {
        DrawToggleRow(
            "chatbox-show-gif-picker", FontAwesomeIcon.Film,
            "Show GIF Button", "Click a GIF to send it, right-click to favourite it.",
            innerWidth, () => Cfg.ShowGifPicker, v => Cfg.ShowGifPicker = v);

        var key = Cfg.GiphyApiKey;

        Row.Draw(
            id: "chatbox-giphy-key",
            icon: FontAwesomeIcon.Key,
            iconColor: string.IsNullOrWhiteSpace(key) ? theme.MutedText : theme.Accent,
            title: "Giphy API Key",
            subtitle: string.IsNullOrWhiteSpace(key) ? "Free, takes a minute. Follow the steps below." : "Stays on this PC and is only sent to Giphy.",
            controlWidth: 380f,
            drawControl: (pos, width) =>
            {
                var value = key;
                var flags = giphyKeyActive ? ImGuiInputTextFlags.None : ImGuiInputTextFlags.Password;
                var buttonWidth = theme.Scaled(100f);
                var gap = theme.Gap(0.5f);

                theme.PushInputScope();
                if (theme.TextInput("##chatbox-giphy-key-input", pos, width - buttonWidth - gap, ref value, 128, "Paste your API key", flags))
                {
                    Cfg.GiphyApiKey = value.Trim();
                    Save();
                }
                giphyKeyActive = ImGui.IsItemActive();
                theme.PopInputScope();

                ImGui.SetCursorScreenPos(new Vector2(pos.X + width - buttonWidth, pos.Y));
                var size = new Vector2(buttonWidth, theme.Scaled(UiTheme.ControlHeight));
                var open = string.IsNullOrWhiteSpace(key)
                    ? theme.PrimaryButton("Get a key##chatbox-giphy-open", size)
                    : theme.SecondaryButton("Dashboard##chatbox-giphy-open", size);

                if (open) Dalamud.Utility.Util.OpenLink(Services.Chatbox.GiphyClient.DashboardUrl);
                if (ImGui.IsItemHovered()) theme.Tooltip("Opens developers.giphy.com in your browser.");
            },
            rowWidth: innerWidth);

        if (string.IsNullOrWhiteSpace(key))
        {
            theme.SpacerY(0.3f);

            theme.StepStrip(GiphySteps, innerWidth);

            theme.SpacerY(0.5f);
            return;
        }

        var giphy = plugin.Chatbox.Giphy;

        DrawActionRow(
            "chatbox-giphy-usage", FontAwesomeIcon.Stopwatch, theme.Accent,
            "Usage",
            $"{giphy.RequestsLastHour}/{Services.Chatbox.GiphyClient.HourlyLimit} requests this hour, {giphy.CachedQueries} searches cached.",
            "Clear Cache", innerWidth,
            giphy.ClearCache);
    }

    private static readonly string[] GiphySteps =
    [
        "Sign up",
        "Create an app",
        "Choose API",
        "Paste the key",
    ];

    private const string GiphyHelp =
        "1. Open developers.giphy.com and sign up or log in, it is free.\n" +
        "2. Click \"Create an App\" and choose \"API\", not SDK.\n" +
        "3. Give it any name and description, e.g. \"Cordi\", and accept the terms.\n" +
        "4. Copy the API key of the new app into the field.\n\n" +
        "Free keys allow 100 requests per hour. Only new searches, trending and \"Load more\" count.\n" +
        "Searches are cached for 7 days and trending for 2 hours, also across restarts.\n" +
        "The key stays on this PC and is only sent to Giphy.";
}
