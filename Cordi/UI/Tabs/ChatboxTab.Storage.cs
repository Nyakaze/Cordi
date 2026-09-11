using System;
using System.Threading;
using System.Threading.Tasks;
using Cordi.UI.Themes;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab
{
    private static readonly TimeSpan StorageSummaryInterval = TimeSpan.FromSeconds(3);

    private DateTime storageSummaryProbedAt = DateTime.MinValue;
    private int storageSummaryRefreshing;
    private string storageSummary = "Reading...";
    private string imageCacheSummary = "Reading...";

    private void RefreshStorageSummaries(bool force)
    {
        var now = DateTime.UtcNow;

        if (!force && now - storageSummaryProbedAt < StorageSummaryInterval)
            return;

        if (Interlocked.Exchange(ref storageSummaryRefreshing, 1) == 1)
            return;

        storageSummaryProbedAt = now;

        Task.Run(() =>
        {
            try
            {
                storageSummary = plugin.Chatbox.StorageSummary();
                imageCacheSummary = plugin.Chatbox.ImageCache.InspectSummary();
            }
            catch (Exception ex)
            {
                plugin.LogService.Error("UI", "Failed to read chatbox storage summary", ex);
            }
            finally
            {
                Volatile.Write(ref storageSummaryRefreshing, 0);
            }
        });
    }

    private void RunStorageAction(Action action)
    {
        action();
        RefreshStorageSummaries(true);
    }

    public void DrawStorage()
    {
        ConsumeScroll();

        RefreshStorageSummaries(false);

        Layout.Draw("Storage", "History limits, the image cache and database maintenance.");

        Card.Draw("chatbox-history", innerWidth =>
        {
            DrawIntSliderRow(
                "chatbox-default-limit", FontAwesomeIcon.Database,
                "Default Messages loaded",
                "How many messages a channel loads when it has no limit of its own. Nothing is deleted.",
                innerWidth, 100, 50000,
                () => Cfg.MaxMessagesPerChannel, v => Cfg.MaxMessagesPerChannel = v);

            DrawInfoRow(
                "chatbox-storage-summary", FontAwesomeIcon.Hdd,
                "Stored History", storageSummary,
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
                "Max Cached Images", imageCacheSummary,
                innerWidth, 100, 5000,
                () => Cfg.ImageCacheMaxEntries, v => Cfg.ImageCacheMaxEntries = v);

            DrawActionRow(
                "chatbox-cache-clear", FontAwesomeIcon.Trash, UiTheme.TileRed,
                "Clear Cache", "Removes every stored image.",
                "Clear", innerWidth,
                () => RunStorageAction(() => plugin.Chatbox.ImageCache.Clear()));

            DrawActionRow(
                "chatbox-cache-prune", FontAwesomeIcon.Cut, UiTheme.TileAmber,
                "Prune Cache", "Trims the cache down to the configured maximum.",
                "Prune", innerWidth,
                () => RunStorageAction(() => plugin.Chatbox.ImageCache.PruneStored(Cfg.ImageCacheMaxEntries)));
        }, "Image Cache");

        Card.Draw("chatbox-maintenance", innerWidth =>
        {
            DrawActionRow(
                "chatbox-clear-all", FontAwesomeIcon.TrashAlt, UiTheme.TileRed,
                "Clear all Messages", "Empties every channel, in memory and on disk.",
                "Clear All", innerWidth,
                () => RunStorageAction(() => plugin.Chatbox.ClearAll()));

            DrawActionRow(
                "chatbox-reload-channels", FontAwesomeIcon.SyncAlt, UiTheme.TileBlue,
                "Reload Channels", "Rebuilds channel state from the configuration.",
                "Reload", innerWidth,
                () => plugin.Chatbox.RebuildChannels());

            DrawActionRow(
                "chatbox-release-history", FontAwesomeIcon.Sort, UiTheme.TileBlue,
                "Unload extra History", "Drops loaded messages back to each channel's limit. Nothing is deleted.",
                "Unload", innerWidth,
                () => plugin.Chatbox.ReleaseAllHistory());

            DrawActionRow(
                "chatbox-apply-retention", FontAwesomeIcon.Broom, UiTheme.TileBlue,
                "Prune Caches", "Trims the image cache and drops expired link previews.",
                "Prune", innerWidth,
                () => RunStorageAction(() => plugin.Chatbox.ApplyRetention()));

            DrawActionRow(
                "chatbox-prune-orphans", FontAwesomeIcon.Broom, UiTheme.TileAmber,
                "Remove Orphaned History", "Deletes stored messages of channels that no longer exist.",
                "Remove", innerWidth,
                () => RunStorageAction(() => plugin.Chatbox.PruneOrphanedHistory()));

            DrawActionRow(
                "chatbox-compact-db", FontAwesomeIcon.Compress, UiTheme.TileTeal,
                "Compact Database", "Reclaims disk space after large deletions.",
                "Compact", innerWidth,
                () => RunStorageAction(() => plugin.Chatbox.Database.Compact()));
        }, "Maintenance");
    }
}
