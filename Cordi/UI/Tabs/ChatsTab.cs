using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using DiscordChannel = Crovus.Models.DiscordChannel;
using Dalamud.Bindings.ImGui;
using Cordi.Services;
using Cordi.Services.Discord.Projections;
using Cordi.Core;
using Cordi.UI.Themes;
using Cordi.Configuration;

namespace Cordi.UI.Tabs;

public partial class ChatsTab : ConfigTabBase
{
    private bool extraChatExpanded = false;
    private bool existingAvatarsExpanded = false;
    private bool highScoreRegexExpanded;
    private bool highScoreKeywordsExpanded;
    private bool mediumScoreRegexExpanded;
    private bool mediumScoreKeywordsExpanded;
    private bool whitelistExpanded;
    private (string Key, ExtraChatConnection Value)? extraChatAddState = null;
    private Dictionary<ulong, string> cachedAvailableThreads = new();
    private readonly Services.Features.ExtraChatService extraChatService;

    public override string Label => "Chats";

    public ChatsTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
        extraChatService = new Cordi.Services.Features.ExtraChatService(plugin);
    }

    protected override IReadOnlyList<(string Label, Action Draw)> GetSubTabs()
    {
        RefreshThreadCache();

        var tabs = new List<(string Label, Action Draw)>
        {
            ("Channel Mappings", () => DrawChatMappingsPage(plugin.Channels.TextChannels, plugin.Channels.ForumChannels)),
            ("Active Conversations", () => DrawActiveConversationsPage(plugin.Channels.ForumChannels)),
            ("Custom Avatars", () => DrawExistingAvatarsCard(ref existingAvatarsExpanded))
        };

        tabs.Add(("Advertisement Filter", () => DrawAdvertisementFilter(plugin.Config.AdvertisementFilter)));

        return tabs;
    }




    private void RefreshThreadCache()
    {
        cachedAvailableThreads.Clear();
        conversationThreadStates.Clear();

        var tellMap = plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == XivChatType.TellIncoming);
        if (tellMap == null || !ulong.TryParse(tellMap.DiscordChannelId, out var forumId))
            return;

        plugin.Channels.EnsureForumThreadsLoaded(forumId);
        cachedAvailableThreads = new Dictionary<ulong, string>(plugin.Channels.GetThreadsForForum(forumId));

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

    private void DrawAdvertisementFilter(AdvertisementFilterConfig config)
    {
        bool filterEnabled = config.Enabled;

        theme.DrawPluginCardAuto(
            id: "ad-filter",
            enabled: ref filterEnabled,
            showCheckbox: true,
            title: "Advertisement Filter",
            drawContent: (avail) =>
            {
                if (filterEnabled)
                {
                    ImGui.TextDisabled("Filters messages containing Discord links, venue locations, and spam keywords.");
                    theme.SpacerY(0.5f);

                    theme.SpacerY(1f);

                    ImGui.TextColored(theme.Text, "Detection Threshold");
                    int threshold = config.ScoreThreshold;
                    using (ImRaii.ItemWidth(200))
                    {
                        if (ImGui.SliderInt("##threshold", ref threshold, 1, 10))
                        {
                            config.ScoreThreshold = threshold;
                            plugin.Config.Save();
                        }
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Lower = more strict filtering. Default: 3");
                    }

                    ImGui.TextColored(theme.MutedText, "Customize detection patterns below. Patterns are scored: High (2 pts) and Medium (1 pt).");
                }
            }
        );

        theme.SpacerY(1f);

        Action save = () => plugin.Config.Save();

        theme.DrawStringTable("hsregex", "High-Score Regex Patterns ", ref highScoreRegexExpanded,
            config.HighScoreRegexPatterns, save, itemName: "Pattern");

        theme.DrawStringTable("hskw", "High-Score Keywords", ref highScoreKeywordsExpanded,
            config.HighScoreKeywords, save, itemName: "Pattern");

        theme.DrawStringTable("msregex", "Medium-Score Regex Patterns", ref mediumScoreRegexExpanded,
            config.MediumScoreRegexPatterns, save, itemName: "Pattern");

        theme.DrawStringTable("mskw", "Medium-Score Keywords", ref mediumScoreKeywordsExpanded,
            config.MediumScoreKeywords, save, itemName: "Pattern");

        theme.DrawStringTable("wl", "Whitelist", ref whitelistExpanded,
            config.Whitelist, save, itemName: "Pattern");
    }

    private void DrawExistingAvatarsCard(ref bool enabled)
    {
        var avatars = plugin.Config.Chat.CustomAvatars;
        var headers = new[] { "Character", "URL", "Action" };

        Action setupCols = () =>
        {
            ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthFixed, 150f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("URL", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 80f * ImGuiHelpers.GlobalScale);
        };

        theme.DrawDictionaryTable(
            "customAvatars",
            $"Custom Avatars: {avatars.Count}",
            ref existingAvatarsExpanded,
            avatars,
            () =>
            {
                plugin.Config.Save();
                // Invalidate all for safety since we don't know exactly which one changed in this generic callback, 
                // but for avatars it's cheap enough.
                foreach (var key in avatars.Keys) plugin.Lodestone.InvalidateAvatarCache(key);
            },
            headers,
            setupColumns: setupCols,
            allowAdd: true,
            collapsible: false
        );
    }

}
