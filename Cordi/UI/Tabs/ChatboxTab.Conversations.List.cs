using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Cordi.Configuration;
using Cordi.Services.Discord.Projections;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab
{
    private const float ConversationRowControlBand = 220f;
    private const float ConversationRowControlBandWithThread = 268f;
    private static readonly TimeSpan ThreadCacheLifetime = TimeSpan.FromSeconds(5);

    private static readonly Vector4[] ConversationTileColors =
    {
        UiTheme.TilePurple,
        UiTheme.TileBlue,
        UiTheme.TilePink,
        UiTheme.TileGreen,
        UiTheme.TileTeal,
        UiTheme.TileAmber,
    };

    private string conversationFilter = string.Empty;
    private string? pendingConversationRemoveId;

    private readonly HashSet<string> avatarRequests = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> expandedConversationThreads = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ThreadStatus> conversationThreadStates = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<ulong, string> cachedAvailableThreads = new();
    private DateTime threadCacheStamp = DateTime.MinValue;
    private bool tellForumMapped;

    private sealed class ConversationRow
    {
        public required string Key { get; init; }
        public required string Name { get; init; }
        public required string World { get; init; }
        public ConversationConfig? Entry { get; init; }
        public string ThreadId { get; init; } = string.Empty;
    }

    private void DrawConversationListCard()
    {
        RefreshThreadCache();

        var rows = BuildConversationRows();
        var matches = FilterConversationRows(rows);

        Card.Draw(
            "conversation-list",
            innerWidth =>
            {
                if (rows.Count > 0)
                {
                    DrawTextRow(
                        "conversation-filter",
                        FontAwesomeIcon.Search,
                        "Find a conversation",
                        "History is kept forever, so the list grows. Filter it by name or world.",
                        innerWidth,
                        () => conversationFilter,
                        value => conversationFilter = value,
                        64,
                        "Search");

                    DrawOptionRow(
                        "conversation-sort",
                        FontAwesomeIcon.SortAmountDown,
                        "Sort the list",
                        "Pinned people always stay at the top.",
                        innerWidth,
                        () => plugin.Config.Chat.ConversationSort,
                        value => plugin.Config.Chat.ConversationSort = value,
                        Options(
                            (ConversationSort.Recent, "Most recent"),
                            (ConversationSort.Name, "Name (A-Z)"),
                            (ConversationSort.World, "World (A-Z)"),
                            (ConversationSort.MessageCount, "Most messages")));
                }

                if (rows.Count == 0)
                {
                    ImGui.TextColored(theme.FaintText, "No conversations yet. Right-click a player in the game and pick \"Message\", or wait for a tell.");
                    return;
                }

                if (matches.Count == 0)
                {
                    ImGui.TextColored(theme.FaintText, $"No conversation matches \"{conversationFilter.Trim()}\".");
                    return;
                }

                foreach (var row in matches)
                    DrawConversationListRow(row, innerWidth);
            },
            "Conversations",
            anchor => DrawCountChip(anchor, rows.Count == 1 ? "1 person" : $"{rows.Count} people"));
    }

    private void RefreshThreadCache()
    {
        if (DateTime.UtcNow - threadCacheStamp < ThreadCacheLifetime)
            return;

        threadCacheStamp = DateTime.UtcNow;
        cachedAvailableThreads.Clear();
        conversationThreadStates.Clear();

        var tellMap = plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == XivChatType.TellIncoming);
        tellForumMapped = tellMap != null && ulong.TryParse(tellMap.DiscordChannelId, out var forumId) && forumId != 0;

        if (!tellForumMapped)
            return;

        ulong.TryParse(tellMap!.DiscordChannelId, out var forum);
        plugin.Channels.EnsureForumThreadsLoaded(forum);
        cachedAvailableThreads = new Dictionary<ulong, string>(plugin.Channels.GetThreadsForForum(forum));

        foreach (var pair in plugin.Config.Chat.TellThreadMappings)
        {
            if (!ulong.TryParse(pair.Value, out var threadId))
            {
                conversationThreadStates[pair.Key] = ThreadStatus.Missing;
                continue;
            }

            if (cachedAvailableThreads.ContainsKey(threadId))
            {
                conversationThreadStates[pair.Key] = ThreadStatus.Known;
                continue;
            }

            var status = plugin.Channels.ResolveThread(threadId, out var name);
            if (status == ThreadStatus.Known)
                cachedAvailableThreads[threadId] = name;

            conversationThreadStates[pair.Key] = status;
        }
    }

    private List<ConversationRow> BuildConversationRows()
    {
        var byKey = new Dictionary<string, ConversationRow>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in Ccfg.Items)
        {
            byKey[entry.Label] = new ConversationRow
            {
                Key = entry.Label,
                Name = entry.Name,
                World = entry.World,
                Entry = entry,
                ThreadId = plugin.Config.Chat.TellThreadMappings.TryGetValue(entry.Label, out var mapped) ? mapped : string.Empty,
            };
        }

        foreach (var pair in plugin.Config.Chat.TellThreadMappings)
        {
            if (byKey.ContainsKey(pair.Key))
                continue;

            SplitCorrespondent(pair.Key, out var name, out var world);

            byKey[pair.Key] = new ConversationRow
            {
                Key = pair.Key,
                Name = name,
                World = world,
                ThreadId = pair.Value,
            };
        }

        return byKey.Values.ToList();
    }

    private List<ConversationRow> FilterConversationRows(List<ConversationRow> rows)
    {
        var needle = conversationFilter.Trim();

        var visible = needle.Length == 0
            ? rows.AsEnumerable()
            : rows.Where(r => r.Key.Contains(needle, StringComparison.OrdinalIgnoreCase));

        var pinnedFirst = visible.OrderByDescending(r => r.Entry?.Pinned == true);

        return (plugin.Config.Chat.ConversationSort switch
        {
            ConversationSort.World => pinnedFirst
                .ThenBy(r => r.World, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase),
            ConversationSort.MessageCount => pinnedFirst
                .ThenByDescending(r => MessageCountOf(r.Key))
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase),
            ConversationSort.Recent => pinnedFirst
                .ThenByDescending(r => r.Entry?.LastActivityTicks ?? 0L)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase),
            _ => pinnedFirst.ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase),
        }).ToList();
    }

    private long MessageCountOf(string key)
    {
        lock (plugin.Config.Stats)
            return plugin.Config.Stats.TellStats.TryGetValue(key, out var count) ? count : 0L;
    }

    private void DrawConversationListRow(ConversationRow row, float rowWidth)
    {
        var entry = row.Entry;
        bool orphan = conversationThreadStates.TryGetValue(row.Key, out var status) && status == ThreadStatus.Missing;
        bool showThread = tellForumMapped;
        bool expanded = showThread && expandedConversationThreads.Contains(row.Key);

        Row.Draw(
            id: $"conversation-{row.Key}",
            icon: entry?.Pinned == true ? FontAwesomeIcon.Thumbtack : FontAwesomeIcon.Comment,
            iconColor: orphan ? UiTheme.TileAmber : ColorForCorrespondent(row.Key),
            title: row.Name,
            subtitle: DescribeConversationRow(row, orphan),
            controlWidth: showThread ? ConversationRowControlBandWithThread : ConversationRowControlBand,
            drawControl: (pos, width) => DrawConversationRowActions(row, pos, width, showThread, expanded),
            rowWidth: rowWidth,
            iconTexture: orphan ? null : ResolveAvatar(row.Key, row.Name, row.World),
            drawTitleBadge: (pos, lineHeight) => DrawMessageCountChip(row.Key, pos, lineHeight));

        if (expanded)
            DrawConversationThreadRow(row, orphan, rowWidth);
    }

    private string DescribeConversationRow(ConversationRow row, bool orphan)
    {
        var parts = new List<string>(3);

        if (row.World.Length > 0)
            parts.Add(row.World);

        if (row.Entry != null)
            parts.Add($"Last message {DescribeConversationAge(row.Entry.LastActivityTicks)}");

        if (orphan)
            parts.Add("Discord thread no longer exists");

        return parts.Count == 0 ? "No messages yet" : string.Join("  -  ", parts);
    }

    private void DrawConversationRowActions(ConversationRow row, Vector2 pos, float width, bool showThread, bool expanded)
    {
        float size = theme.Scaled(UiTheme.ActionButtonSize);
        int slots = showThread ? 5 : 4;
        float step = (width - size) / (slots - 1);
        int slot = 0;

        Vector2 Next() => new(pos.X + step * slot++, pos.Y);

        if (theme.IconAction(
                $"conversation-pin-{row.Key}",
                Next(),
                FontAwesomeIcon.Thumbtack,
                UiTheme.TileAmber,
                row.Entry?.Pinned == true ? "Unpin" : "Pin to the top",
                restColor: row.Entry?.Pinned == true ? UiTheme.TileAmber : null))
        {
            var entry = row.Entry;

            if (entry == null)
            {
                var state = plugin.Chatbox.OpenConversation(row.Name, row.World, false);
                entry = state == null ? null : plugin.Chatbox.FindConversation(state.Id);
            }

            if (entry != null)
            {
                entry.Pinned = !entry.Pinned;
                Save();
            }
        }

        if (theme.IconAction(
                $"conversation-open-{row.Key}",
                Next(),
                FontAwesomeIcon.ExternalLinkAlt,
                UiTheme.TileGreen,
                "Open the tab"))
        {
            plugin.Chatbox.OpenConversationFor(row.Name, row.World);
        }

        if (showThread && theme.IconAction(
                $"conversation-thread-toggle-{row.Key}",
                Next(),
                FontAwesomeIcon.Hashtag,
                UiTheme.TilePurple,
                expanded ? "Hide the Discord thread" : "Show the Discord thread",
                restColor: row.ThreadId.Length > 0 ? UiTheme.TilePurple : null))
        {
            if (!expandedConversationThreads.Remove(row.Key))
                expandedConversationThreads.Add(row.Key);
        }

        if (theme.IconAction(
                $"conversation-clear-{row.Key}",
                Next(),
                FontAwesomeIcon.Eraser,
                UiTheme.TileAmber,
                "Clear this conversation's history"))
        {
            if (row.Entry != null)
                plugin.Chatbox.ForgetChannel(row.Entry.Id);
        }

        if (theme.DeleteAction(
                $"conversation-del-{row.Key}",
                new Vector2(pos.X + width - size, pos.Y),
                "Forget this person, their history and their Discord thread"))
        {
            pendingConversationRemoveId = row.Key;
        }
    }

    private void DrawConversationThreadRow(ConversationRow row, bool orphan, float rowWidth)
    {
        Row.Draw(
            id: $"conversation-thread-{row.Key}",
            icon: orphan ? FontAwesomeIcon.ExclamationTriangle : FontAwesomeIcon.Hashtag,
            iconColor: orphan ? UiTheme.TileAmber : UiTheme.TilePurple,
            title: "Discord thread",
            subtitle: orphan
                ? "The linked thread is gone. Pick another one or unlink it."
                : "The forum thread this person's tells are relayed into.",
            controlWidth: 320f,
            drawControl: (pos, width) =>
            {
                float gap = theme.Gap(1.6f);
                float deleteWidth = theme.Scaled(UiTheme.ActionButtonSize);
                float pickerWidth = width - deleteWidth - gap;

                ImGui.SetCursorScreenPos(pos);
                theme.ThreadPicker(
                    $"conversation-thread-picker-{row.Key}",
                    row.ThreadId,
                    cachedAvailableThreads,
                    newId =>
                    {
                        plugin.Config.Chat.TellThreadMappings[row.Key] = newId;
                        Save();
                        threadCacheStamp = DateTime.MinValue;
                    },
                    defaultLabel: "Select a thread...",
                    width: pickerWidth);

                if (theme.DeleteAction(
                        $"conversation-thread-unlink-{row.Key}",
                        new Vector2(pos.X + pickerWidth + gap, pos.Y),
                        "Unlink the thread",
                        deleteWidth))
                {
                    plugin.Config.Chat.TellThreadMappings.Remove(row.Key);
                    Save();
                    threadCacheStamp = DateTime.MinValue;
                }
            },
            rowWidth: rowWidth);
    }

    private void DrawMessageCountChip(string key, Vector2 pos, float lineHeight)
    {
        long count = MessageCountOf(key);

        if (count <= 0)
            return;

        string text = count == 1 ? "1 message" : $"{count:N0} messages";

        float height = lineHeight + theme.Scaled(2f);
        var min = new Vector2(pos.X, pos.Y + (lineHeight - height) * 0.5f);
        var max = min + new Vector2(theme.ChipPillWidth(text), height);

        theme.ChipPill(min, max, text);
    }

    private static string DescribeConversationAge(long ticks)
    {
        if (ticks <= 0)
            return "never";

        var age = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);

        if (age < TimeSpan.Zero || age.TotalMinutes < 1)
            return "just now";

        if (age.TotalHours < 1)
            return $"{(int)age.TotalMinutes} min ago";

        if (age.TotalDays < 1)
            return $"{(int)age.TotalHours} h ago";

        if (age.TotalDays < 30)
            return $"{(int)age.TotalDays} d ago";

        return new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("d");
    }

    private void ApplyPendingConversationChanges()
    {
        if (pendingConversationRemoveId == null)
            return;

        var key = pendingConversationRemoveId;
        pendingConversationRemoveId = null;

        var entry = Ccfg.Items.FirstOrDefault(c => string.Equals(c.Label, key, StringComparison.OrdinalIgnoreCase));

        if (entry != null)
        {
            plugin.Chatbox.RemoveConversation(entry.Id);
            plugin.Chatbox.ForgetChannel(entry.Id);
        }

        if (plugin.Config.Chat.TellThreadMappings.Remove(key))
            Save();

        expandedConversationThreads.Remove(key);
        threadCacheStamp = DateTime.MinValue;
    }

    private IDalamudTextureWrap? ResolveAvatar(string key, string name, string world)
    {
        if (plugin.Lodestone.TryGetAvatar(key, out var url) && !string.IsNullOrEmpty(url))
            return plugin.Chatbox.ImageCache.Get(url);

        RequestAvatar(key, name, world);
        return null;
    }

    private void RequestAvatar(string key, string name, string world)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(world))
            return;

        if (!avatarRequests.Add(key))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await plugin.Lodestone.GetAvatarUrlAsync(name, world);
            }
            catch (Exception ex)
            {
                plugin.LogService.Error("UI", $"Failed to resolve avatar for {key}", ex);
            }
        });
    }

    private static Vector4 ColorForCorrespondent(string key)
    {
        int hash = 17;
        foreach (var c in key)
            hash = hash * 31 + char.ToLowerInvariant(c);

        return ConversationTileColors[(hash & int.MaxValue) % ConversationTileColors.Length];
    }

    private static void SplitCorrespondent(string key, out string name, out string world)
    {
        int index = key.IndexOf('@');

        if (index > 0)
        {
            name = key[..index];
            world = key[(index + 1)..];
            return;
        }

        name = key;
        world = string.Empty;
    }
}
