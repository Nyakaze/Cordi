using Cordi.UI.Themes;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab
{
    public void DrawStorage()
    {
        ConsumeScroll();

        Layout.Draw("Storage", "History limits, the image cache and database maintenance.");

        Card.Draw("chatbox-history", innerWidth =>
        {
            DrawIntSliderRow(
                "chatbox-default-limit", FontAwesomeIcon.Database,
                "Default Messages per Channel",
                "Used for channels that do not set their own limit, and for the combined view.",
                innerWidth, 100, 50000,
                () => Cfg.MaxMessagesPerChannel, v => Cfg.MaxMessagesPerChannel = v);

            DrawInfoRow(
                "chatbox-storage-summary", FontAwesomeIcon.Hdd,
                "Stored History", plugin.Chatbox.StorageSummary(),
                innerWidth);

            DrawInfoRow(
                "chatbox-db-path", FontAwesomeIcon.FolderOpen,
                "Database", plugin.Chatbox.Database.FilePath,
                innerWidth);
        }, "History");

        Card.Draw("chatbox-image-cache", innerWidth =>
        {
            DrawToggleRow(
                "chatbox-cache-enabled", FontAwesomeIcon.Images,
                "Enable Image Cache", "Stored inside the chatbox database, deduplicated by content.",
                innerWidth, () => Cfg.ImageCacheEnabled, v => Cfg.ImageCacheEnabled = v);

            DrawIntSliderRow(
                "chatbox-cache-max", FontAwesomeIcon.Sort,
                "Max Cached Images", plugin.Chatbox.ImageCache.InspectSummary(),
                innerWidth, 100, 5000,
                () => Cfg.ImageCacheMaxEntries, v => Cfg.ImageCacheMaxEntries = v);

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

            if (Cfg.AnimateGifs)
            {
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
            }

            DrawActionRow(
                "chatbox-cache-clear", FontAwesomeIcon.Trash, UiTheme.TileRed,
                "Clear Cache", "Removes every stored image.",
                "Clear", innerWidth,
                () => plugin.Chatbox.ImageCache.Clear());

            DrawActionRow(
                "chatbox-cache-prune", FontAwesomeIcon.Cut, UiTheme.TileAmber,
                "Prune Cache", "Trims the cache down to the configured maximum.",
                "Prune", innerWidth,
                () => plugin.Chatbox.ImageCache.PruneStored(Cfg.ImageCacheMaxEntries));
        }, "Image Cache");

        Card.Draw("chatbox-maintenance", innerWidth =>
        {
            DrawActionRow(
                "chatbox-clear-all", FontAwesomeIcon.TrashAlt, UiTheme.TileRed,
                "Clear all Messages", "Empties every channel, in memory and on disk.",
                "Clear All", innerWidth,
                () => plugin.Chatbox.ClearAll());

            DrawActionRow(
                "chatbox-reload-channels", FontAwesomeIcon.SyncAlt, UiTheme.TileBlue,
                "Reload Channels", "Rebuilds channel state from the configuration.",
                "Reload", innerWidth,
                () => plugin.Chatbox.RebuildChannels());

            DrawActionRow(
                "chatbox-apply-retention", FontAwesomeIcon.Sort, UiTheme.TileBlue,
                "Apply Limits now", "Trims every channel to its message limit.",
                "Apply", innerWidth,
                () => plugin.Chatbox.ApplyRetention());

            DrawActionRow(
                "chatbox-prune-orphans", FontAwesomeIcon.Broom, UiTheme.TileAmber,
                "Remove Orphaned History", "Deletes stored messages of channels that no longer exist.",
                "Remove", innerWidth,
                () => plugin.Chatbox.PruneOrphanedHistory());

            DrawActionRow(
                "chatbox-compact-db", FontAwesomeIcon.Compress, UiTheme.TileTeal,
                "Compact Database", "Reclaims disk space after large deletions.",
                "Compact", innerWidth,
                () => plugin.Chatbox.Database.Compact());
        }, "Maintenance");
    }
}
