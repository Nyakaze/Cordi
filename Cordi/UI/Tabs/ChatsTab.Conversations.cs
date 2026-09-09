using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Cordi.Configuration;
using Cordi.Services;
using Cordi.Services.Discord.Projections;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Cordi.UI.Windows;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using DiscordChannel = Crovus.Models.DiscordChannel;

namespace Cordi.UI.Tabs;

public partial class ChatsTab
{
    private static readonly Vector4[] ConversationTileColors =
    {
        UiTheme.TilePurple,
        UiTheme.TileBlue,
        UiTheme.TilePink,
        UiTheme.TileGreen,
        UiTheme.TileTeal,
        UiTheme.TileAmber,
    };

    private static readonly DropdownItem[] ConversationSortItems =
    {
        new() { Key = nameof(ConversationSort.Name), Label = "Name (A-Z)" },
        new() { Key = nameof(ConversationSort.World), Label = "World (A-Z)" },
        new() { Key = nameof(ConversationSort.MessageCount), Label = "Most messages" },
    };

    private string conversationFilter = string.Empty;
    private readonly HashSet<string> avatarRequests = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ThreadStatus> conversationThreadStates = new();

    private void DrawActiveConversationsPage(IReadOnlyList<DiscordChannel>? forumChannels)
    {
        Layout.Draw(
            "Active Conversations",
            "Ongoing tell threads and their Discord counterparts",
            innerWidth => DrawForumReferenceRow(forumChannels, innerWidth));

        Card.Draw(
            "conversations",
            innerWidth =>
            {
                theme.PushInputScope();

                var conversations = plugin.Config.Chat.TellThreadMappings;
                var visible = SortConversations(conversations
                        .Where(pair => string.IsNullOrWhiteSpace(conversationFilter)
                                       || pair.Key.Contains(conversationFilter.Trim(), StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (conversations.Count == 0)
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, theme.MutedText))
                        ImGui.TextUnformatted("No conversations yet. A thread is created automatically the first time a tell arrives.");
                }
                else if (visible.Count == 0)
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, theme.MutedText))
                        ImGui.TextUnformatted("No conversation matches the filter.");
                }

                foreach (var pair in visible)
                    DrawConversationRow(pair.Key, pair.Value, innerWidth);

                theme.PopInputScope();
            },
            label: "Conversations",
            drawTrailing: DrawConversationHeaderControls);
    }

    private IEnumerable<KeyValuePair<string, string>> SortConversations(IEnumerable<KeyValuePair<string, string>> source) =>
        plugin.Config.Chat.ConversationSort switch
        {
            ConversationSort.World => source
                .OrderBy(pair => WorldOf(pair.Key), StringComparer.OrdinalIgnoreCase)
                .ThenBy(pair => NameOf(pair.Key), StringComparer.OrdinalIgnoreCase),
            ConversationSort.MessageCount => source
                .OrderByDescending(pair => MessageCountOf(pair.Key))
                .ThenBy(pair => NameOf(pair.Key), StringComparer.OrdinalIgnoreCase),
            _ => source.OrderBy(pair => NameOf(pair.Key), StringComparer.OrdinalIgnoreCase),
        };

    private long MessageCountOf(string key)
    {
        lock (plugin.Config.Stats)
            return plugin.Config.Stats.TellStats.TryGetValue(key, out var count) ? count : 0L;
    }

    private void DrawForumReferenceRow(IReadOnlyList<DiscordChannel>? forumChannels, float rowWidth)
    {
        var mapping = plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == XivChatType.TellIncoming);
        string channelId = mapping?.DiscordChannelId ?? string.Empty;
        bool mapped = !string.IsNullOrEmpty(channelId);

        string? channelName = mapped && forumChannels != null
            ? forumChannels.FirstOrDefault(c => c.Id.ToString() == channelId)?.Name
            : null;

        string subtitle = mapped
            ? channelName != null ? $"#{channelName}" : channelId
            : "Not mapped yet, pick a forum channel for Tell first";

        Row.Draw(
            id: "tell-forum-reference",
            icon: mapped ? FontAwesomeIcon.Comments : FontAwesomeIcon.ExclamationTriangle,
            iconColor: mapped ? UiTheme.TilePink : UiTheme.TileAmber,
            title: "Tell forum channel",
            subtitle: subtitle,
            controlWidth: 210f,
            drawControl: (pos, width) =>
            {
                float buttonHeight = theme.Scaled(32f);
                ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + (theme.Scaled(UiTheme.ControlHeight) - buttonHeight) * 0.5f));

                if (theme.SecondaryButton("Open Channel Mappings", new Vector2(width, buttonHeight)))
                    plugin.MainConfigWindow.Navigate(PageIds.ChannelMappings);
            },
            rowWidth: rowWidth);
    }

    private void DrawConversationRow(string key, string threadId, float rowWidth)
    {
        SplitCorrespondent(key, out var name, out var world);

        bool orphan = conversationThreadStates.TryGetValue(key, out var status) && status == ThreadStatus.Missing;

        string subtitle = orphan
            ? string.IsNullOrEmpty(world) ? "Thread no longer exists" : $"{world}  -  Thread no longer exists"
            : world;

        Row.Draw(
            id: $"conv-{key}",
            icon: orphan ? FontAwesomeIcon.ExclamationTriangle : FontAwesomeIcon.UserCircle,
            iconColor: orphan ? UiTheme.TileAmber : ColorForCorrespondent(key),
            title: name,
            subtitle: subtitle,
            controlWidth: 320f,
            drawControl: (pos, width) =>
            {
                float gap = theme.Gap(1.6f);
                float deleteWidth = theme.Scaled(UiTheme.ActionButtonSize);
                float pickerWidth = width - deleteWidth - gap;

                ImGui.SetCursorScreenPos(pos);
                theme.ThreadPicker(
                    $"conv-thread-{key}",
                    threadId,
                    cachedAvailableThreads,
                    newId =>
                    {
                        plugin.Config.Chat.TellThreadMappings[key] = newId;
                        plugin.Config.Save();
                        plugin.NotificationManager.Add("Conversation Updated", $"Changed thread for {key}", CordiNotificationType.Success);
                    },
                    defaultLabel: "Select a thread...",
                    width: pickerWidth);

                if (theme.DeleteAction(
                        $"conv-del-{key}",
                        new Vector2(pos.X + pickerWidth + gap, pos.Y),
                        "Unlink conversation",
                        deleteWidth))
                {
                    plugin.Config.Chat.TellThreadMappings.Remove(key);
                    plugin.Config.Save();
                }
            },
            rowWidth: rowWidth,
            iconTexture: orphan ? null : ResolveAvatar(key, name, world),
            drawTitleBadge: (pos, lineHeight) => DrawMessageCountChip(key, pos, lineHeight));
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

    private void DrawConversationHeaderControls(Vector2 rightAnchor)
    {
        float filterWidth = theme.Scaled(190f);
        float sortWidth = theme.Scaled(160f);
        float gap = theme.Gap();
        float height = theme.Scaled(UiTheme.ControlHeight);
        float total = filterWidth + sortWidth + gap;
        var min = new Vector2(rightAnchor.X - total, rightAnchor.Y - theme.Scaled(8f));

        theme.PushInputScope();

        theme.TextInput("##conversation-filter", min, filterWidth, ref conversationFilter, 64, "Filter by name...");

        ImGui.SetCursorScreenPos(new Vector2(min.X + filterWidth + gap, min.Y));
        theme.OptionPicker(
            "conversation-sort",
            plugin.Config.Chat.ConversationSort.ToString(),
            ConversationSortItems,
            key =>
            {
                if (!Enum.TryParse<ConversationSort>(key, out var sort))
                    return;

                plugin.Config.Chat.ConversationSort = sort;
                plugin.Config.Save();
            },
            width: sortWidth);

        theme.PopInputScope();

        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(new Vector2(total, height));
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

    private static string NameOf(string key)
    {
        SplitCorrespondent(key, out var name, out _);
        return name;
    }

    private static string WorldOf(string key)
    {
        SplitCorrespondent(key, out _, out var world);
        return world;
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
