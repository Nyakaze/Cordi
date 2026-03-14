using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using DSharpPlus.Entities;
using Dalamud.Bindings.ImGui;
using Cordi.Services;
using Cordi.Core;
using Cordi.UI.Themes;
using Cordi.Configuration;

namespace Cordi.UI.Tabs;

public class ChatsTab : ConfigTabBase
{
    private bool activeTellsExpanded = false;
    private bool extraChatExpanded = false;
    private bool existingAvatarsExpanded = false;
    private bool highScoreRegexExpanded;
    private bool highScoreKeywordsExpanded;
    private bool mediumScoreRegexExpanded;
    private bool mediumScoreKeywordsExpanded;
    private bool whitelistExpanded;
    private Dictionary<string, (string Key, ExtraChatConnection Value)> extraChatEditStates = new();
    private (string Key, ExtraChatConnection Value)? extraChatAddState = null;
    private Dictionary<ulong, string> cachedAvailableThreads = new();
    private readonly Services.Features.ExtraChatService extraChatService;


    private readonly XivChatType[] supportedChatTypes = new[]
    {
        XivChatType.Say, XivChatType.Shout, XivChatType.Yell,
        XivChatType.Party, XivChatType.Alliance, XivChatType.FreeCompany,
        XivChatType.TellIncoming
    };

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
            ("Channel Mappings", () => DrawChatMappingsCard(plugin.ChannelCache.TextChannels, plugin.ChannelCache.ForumChannels, ref activeTellsExpanded)),
            ("Active Conversations", () => DrawActiveTellsCard(plugin.ChannelCache.ForumChannels, ref activeTellsExpanded)),
            ("Custom Avatars", () => DrawExistingAvatarsCard(ref existingAvatarsExpanded))
        };

        if (extraChatService.IsExtraChatInstalled())
        {
            tabs.Add(("ExtraChat Mappings", () =>
            {
                DrawExtraChatMappingsCard(plugin.ChannelCache.TextChannels, ref extraChatExpanded, extraChatService);
            }
            ));
        }

        tabs.Add(("Advertisement Filter", () => DrawAdvertisementFilter(plugin.Config.AdvertisementFilter)));

        return tabs;
    }




    // public override void Draw()
    // {
    //     theme.SpacerY(2f);
    //     bool enabled = true;
    //
    //
    //     RefreshThreadCache();
    //
    //     var textChannels = plugin.ChannelCache.TextChannels;
    //     var forumChannels = plugin.ChannelCache.ForumChannels;
    //
    //
    //     DrawDefaultChannelCard(textChannels, ref enabled);
    //
    //     theme.SpacerY(2f);
    //
    //     ImGui.Separator();
    //     theme.SpacerY(2f);
    //
    //     DrawActiveTellsCard(forumChannels, ref enabled);
    //
    //     theme.SpacerY(1f);
    //
    //
    //     DrawExistingAvatarsCard(ref enabled);
    //
    //     theme.SpacerY(1f);
    //
    //     ImGui.Separator();
    //     theme.SpacerY(2f);
    //
    //     DrawChatMappingsCard(textChannels, forumChannels, ref enabled);
    //
    //
    //     if (extraChatService.IsExtraChatInstalled())
    //     {
    //
    //         theme.SpacerY(2f);
    //         ImGui.Separator();
    //         theme.SpacerY(2f);
    //
    //         DrawExtraChatMappingsCard(textChannels, ref enabled, extraChatService);
    //     }
    // }

    private void RefreshThreadCache()
    {
        cachedAvailableThreads.Clear();
        var tellMap = plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == XivChatType.TellIncoming);
        if (tellMap != null && ulong.TryParse(tellMap.DiscordChannelId, out var forumId))
        {
            cachedAvailableThreads = plugin.ChannelCache.GetThreadsForForum(forumId);
        }
    }

    private void DrawDefaultChannelCard(IReadOnlyList<DiscordChannel>? textChannels, ref bool enabled)
    {
        string defaultChannelId = plugin.Config.Discord.DefaultChannelId;

        theme.DrawPluginCardAuto(
            id: "dsc-channel-mappings",
            enabled: ref enabled,
            showCheckbox: false,
            title: "Default Channel",
            drawContent: (avail) =>
            {
                var style = ImGui.GetStyle();
                float availWidth = avail;

                if (textChannels != null)
                {
                    theme.ChannelPicker(
                        "dsc-default-channel-combo",
                        defaultChannelId,
                        textChannels,
                        (newId) =>
                        {
                            plugin.Config.Discord.DefaultChannelId = newId;
                            plugin.Config.Save();
                            plugin.ChannelCache.Invalidate();
                        },
                        defaultLabel: "Select a Channel..."
                    );
                }
                else
                {

                    using (ImRaii.ItemWidth(availWidth))
                    {
                        bool changed = ImGui.InputText("##dsc-default-channel-id", ref defaultChannelId, 32);
                        if (changed)
                        {
                            plugin.Config.Discord.DefaultChannelId = defaultChannelId;
                            plugin.Config.Save();
                        }
                    }
                }
            }
        );
    }

    private void DrawExtraChatMappingsCard(IReadOnlyList<DiscordChannel>? textChannels, ref bool enabled, Services.Features.ExtraChatService extraChatService)
    {
        var mappings = plugin.Config.Chat.ExtraChatMappings;
        var headers = new[] { "Label", "Num", "Discord Channel", "Action" };

        Action setupCols = () =>
        {
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 120f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Num", ImGuiTableColumnFlags.WidthFixed, 50f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Discord Channel", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 80f * ImGuiHelpers.GlobalScale);
        };

        Action<KeyValuePair<string, ExtraChatConnection>, int> drawRow = (kvp, idx) =>
        {
            var key = kvp.Key;
            var connection = kvp.Value;
            bool isEditing = extraChatEditStates.TryGetValue("extra", out var state) && state.Key == key;

            if (isEditing)
            {
                string tempKey = state.Key;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText($"##edit-key-{key}", ref tempKey, 64))
                {
                    extraChatEditStates["extra"] = (tempKey, state.Value);
                }
            }
            else
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(key);
            }

            ImGui.TableNextColumn();
            if (isEditing)
            {
                int num = state.Value.ExtraChatNumber;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputInt($"##edit-num-{key}", ref num, 0))
                {
                    if (num < 0) num = 0;
                    if (num > 8) num = 8;
                    state.Value.ExtraChatNumber = num;
                }
            }
            else
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(connection.ExtraChatNumber.ToString());
            }

            ImGui.TableNextColumn();
            if (isEditing)
            {
                theme.ChannelPicker(
                    $"extra-combo-{key}",
                    state.Value.DiscordChannelId ?? "",
                    textChannels ?? new List<DiscordChannel>(),
                    (newId) => state.Value.DiscordChannelId = newId,
                    showLabel: false,
                    width: -1
                );
            }
            else
            {
                ImGui.AlignTextToFramePadding();
                string channelName = "None";
                if (!string.IsNullOrEmpty(connection.DiscordChannelId))
                {
                    var ch = textChannels?.FirstOrDefault(c => c.Id.ToString() == connection.DiscordChannelId);
                    channelName = ch != null ? $"#{ch.Name}" : connection.DiscordChannelId;
                }
                ImGui.TextUnformatted(channelName);
            }

            ImGui.TableNextColumn();
            if (isEditing)
            {
                if (theme.SuccessIconButton($"##save-{key}", FontAwesomeIcon.Check, "Save"))
                {
                    if (!string.IsNullOrWhiteSpace(state.Key))
                    {
                        mappings.Remove(key);
                        mappings[state.Key] = state.Value;
                        extraChatEditStates.Remove("extra");
                        plugin.Config.Save();
                    }
                }
                ImGui.SameLine();
                if (theme.SecondaryIconButton($"##cancel-{key}", FontAwesomeIcon.Times, "Cancel"))
                {
                    extraChatEditStates.Remove("extra");
                }
            }
            else
            {
                if (theme.SecondaryIconButton($"##edit-{key}", FontAwesomeIcon.Pen, "Edit"))
                {
                    var editConn = new ExtraChatConnection
                    {
                        DiscordChannelId = connection.DiscordChannelId,
                        ExtraChatNumber = connection.ExtraChatNumber,
                        ExtraChatGuid = connection.ExtraChatGuid
                    };
                    extraChatEditStates["extra"] = (key, editConn);
                    extraChatAddState = null;
                }
                ImGui.SameLine();
                if (theme.DangerIconButton($"##del-{key}", FontAwesomeIcon.Trash, "Delete"))
                {
                    mappings.Remove(key);
                    plugin.Config.Save();
                }
            }
        };

        theme.PushInputScope();
        theme.DrawCollapsableCardWithTable(
            id: "extraChatMappings",
            title: "ExtraChat Mappings",
            expanded: ref extraChatExpanded,
            collection: mappings.ToList(),
            drawRow: drawRow,
            headers: headers,
            setupColumns: setupCols,
            showCount: true,
            showHeaders: true,
            mutedText: "Map ExtraChat Channels (like 'ECLS1') to specific channels.",
            collapsible: false,
            drawTopContent: (width) =>
            {
                float btnWidth = width * 0.95f;
                float avail = ImGui.GetContentRegionAvail().X;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail - btnWidth) * 0.5f);
                if (theme.SecondaryButton("Sync from ExtraChat", new Vector2(btnWidth, 0)))
                {
                    int count = extraChatService.SyncFromExtraChat();
                    plugin.NotificationManager.Add("ExtraChat Sync", $"Synced {count} channels from ExtraChat.", CordiNotificationType.Success);
                }
            },
            drawFooter: (width) =>
            {
                if (extraChatAddState != null)
                {
                    var state = extraChatAddState.Value;
                    float spacing = ImGui.GetStyle().ItemSpacing.X;
                    float btnSize = ImGui.GetFrameHeight();
                    float buttonsWidth = btnSize * 2 + spacing;
                    float numWidth = 50f * ImGuiHelpers.GlobalScale;
                    float keyWidth = width * 0.25f;
                    float chanWidth = width - keyWidth - numWidth - buttonsWidth - spacing * 3;

                    string nKey = state.Key;
                    ImGui.SetNextItemWidth(keyWidth);
                    ImGui.InputTextWithHint($"##add-key-extra", "Label", ref nKey, 64);

                    ImGui.SameLine();
                    int nNum = state.Value.ExtraChatNumber;
                    ImGui.SetNextItemWidth(numWidth);
                    if (ImGui.InputInt($"##add-num-extra", ref nNum, 0))
                    {
                        if (nNum < 0) nNum = 0;
                        if (nNum > 8) nNum = 8;
                        state.Value.ExtraChatNumber = nNum;
                    }

                    ImGui.SameLine();
                    theme.ChannelPicker("add-chan-extra", state.Value.DiscordChannelId ?? "", textChannels ?? new List<DiscordChannel>(), (id) => state.Value.DiscordChannelId = id, showLabel: false, width: chanWidth);

                    extraChatAddState = (nKey, state.Value);

                    ImGui.SameLine();
                    if (theme.SuccessIconButton("##add-save-extra", FontAwesomeIcon.Check, "Add"))
                    {
                        if (!string.IsNullOrWhiteSpace(nKey) && !mappings.ContainsKey(nKey))
                        {
                            mappings[nKey] = state.Value;
                            extraChatAddState = null;
                            plugin.Config.Save();
                        }
                    }
                    ImGui.SameLine();
                    if (theme.SecondaryIconButton("##add-cancel-extra", FontAwesomeIcon.Times, "Cancel"))
                    {
                        extraChatAddState = null;
                    }
                    theme.SpacerY(1f);
                }

                float btnWidth = width * 0.95f;
                float avail = ImGui.GetContentRegionAvail().X;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail - btnWidth) * 0.5f);
                if (theme.SecondaryButton("Add New ExtraChat Mapping", new Vector2(btnWidth, 0)))
                {
                    extraChatAddState = ("", new ExtraChatConnection());
                    extraChatEditStates.Remove("extra");
                }
            },
            drawHeaderRight: () =>
            {
                ImGui.TextDisabled("(?)");
                if (ImGui.IsItemHovered())
                {
                    string tip = "Info:\n";
                    tip += "- Chats get automatically added once a message is sent in that chat.\n";
                    tip += "- You can manually add chats by using the Add button.\n";
                    tip += "\n";
                    tip += "- Labels are the Names in game chat, for example: [ECLS1]\n";
                    tip += "- As Label you would write ECLS1 in this case.\n";
                    tip += "- The Number is the Channel number, for example /ecl1 (1 is the Channel)\n";
                    ImGui.SetTooltip(tip);
                }
            }
        );
        theme.PopInputScope();
    }

    private void DrawChatMappingsCard(IReadOnlyList<DiscordChannel>? textChannels, IReadOnlyList<DiscordChannel>? forumChannels, ref bool enabled)
    {
        DrawDefaultChannelCard(textChannels, ref enabled);

        theme.SpacerY();

        theme.DrawPluginCardAuto(
           id: "chat-mappings-card",
           enabled: ref enabled,
           showCheckbox: false,
           title: "Chat Mappings",
           drawContent: (avail) =>
           {
               ImGui.TextColored(theme.MutedText, "Assign Discord channels to Game Chat types.");
               theme.SpacerY(1f);

               using (var mappingsTable = ImRaii.Table("##mappingsTable", 3, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp))
                   if (mappingsTable)
                   {
                       ImGui.TableSetupColumn("Chat Type", ImGuiTableColumnFlags.WidthFixed, 150f);
                       ImGui.TableSetupColumn("Discord Channel / Forum", ImGuiTableColumnFlags.WidthStretch);
                       ImGui.TableSetupColumn("Filter Ads", ImGuiTableColumnFlags.WidthFixed, 30f);

                       Action<XivChatType> drawRow = (chatType) =>
                       {
                           ImGui.TableNextRow();
                           ImGui.TableNextColumn();
                           ImGui.AlignTextToFramePadding();

                           string label = chatType == XivChatType.TellIncoming ? "Tell" : chatType.ToString();
                           ImGui.Text(label);

                           ImGui.TableNextColumn();
                           string currentId = "";
                           if (plugin.Config.MappingCache.TryGetValue(chatType, out var cachedId)) currentId = cachedId;

                           bool isTell = chatType == XivChatType.TellIncoming;
                           var targetChannels = isTell ? forumChannels : textChannels;

                           theme.ChannelPicker(
                               $"combo-{chatType}",
                               currentId,
                               targetChannels,
                               (newId) =>
                               {
                                   if (string.IsNullOrEmpty(newId))
                                   {
                                       var map = plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == chatType);
                                       if (map != null) plugin.Config.Chat.Mappings.Remove(map);
                                       if (isTell)
                                       {
                                           var mapOut = plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == XivChatType.TellOutgoing);
                                           if (mapOut != null) plugin.Config.Chat.Mappings.Remove(mapOut);
                                       }
                                   }
                                   else
                                   {
                                       var map = plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == chatType);

                                       if (map != null) map.DiscordChannelId = newId;
                                       else plugin.Config.Chat.Mappings.Add(new ChannelMapping { GameChatType = chatType, DiscordChannelId = newId });

                                       if (isTell)
                                       {
                                           var mapOut = plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == XivChatType.TellOutgoing);
                                           if (mapOut != null) mapOut.DiscordChannelId = newId;
                                           else plugin.Config.Chat.Mappings.Add(new ChannelMapping { GameChatType = XivChatType.TellOutgoing, DiscordChannelId = newId });
                                       }
                                   }
                                   plugin.Config.Save();
                                   plugin.ChannelCache.Invalidate();
                               },
                               showLabel: false
                           );

                           // Advertisement Filter checkbox
                           ImGui.TableNextColumn();
                           var mapping = plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == chatType);
                           if (mapping != null && !string.IsNullOrEmpty(mapping.DiscordChannelId))
                           {
                               bool filterEnabled = mapping.EnableAdvertisementFilter;
                               theme.ConfigCheckbox($"##filter-{chatType}", ref filterEnabled, () =>
                               {
                                   mapping.EnableAdvertisementFilter = filterEnabled;
                                   plugin.Config.Save();
                               });
                               if (ImGui.IsItemHovered())
                               {
                                   ImGui.SetTooltip("Enable advertisement filter for this channel");
                               }
                           }
                       };

                       foreach (var chatType in supportedChatTypes)
                       {
                           drawRow(chatType);
                       }

                       ImGui.TableNextRow();
                       ImGui.TableNextColumn();
                       using (var lsTree = ImRaii.TreeNode("Linkshell"))
                       {
                           ImGui.TableNextColumn();
                           if (lsTree)
                           {
                               drawRow(XivChatType.Ls1);
                               drawRow(XivChatType.Ls2);
                               drawRow(XivChatType.Ls3);
                               drawRow(XivChatType.Ls4);
                               drawRow(XivChatType.Ls5);
                               drawRow(XivChatType.Ls6);
                               drawRow(XivChatType.Ls7);
                               drawRow(XivChatType.Ls8);
                           }
                       }

                       ImGui.TableNextRow();
                       ImGui.TableNextColumn();
                       using (var cwlsTree = ImRaii.TreeNode("Cross-World Linkshells"))
                       {
                           ImGui.TableNextColumn();
                           if (cwlsTree)
                           {
                               drawRow(XivChatType.CrossLinkShell1);
                               drawRow(XivChatType.CrossLinkShell2);
                               drawRow(XivChatType.CrossLinkShell3);
                               drawRow(XivChatType.CrossLinkShell4);
                               drawRow(XivChatType.CrossLinkShell5);
                               drawRow(XivChatType.CrossLinkShell6);
                               drawRow(XivChatType.CrossLinkShell7);
                               drawRow(XivChatType.CrossLinkShell8);
                           }
                       }
                   }

               theme.SpacerY(1f);
               ImGui.Separator();
               theme.SpacerY(1f);

               bool tellNotif = plugin.Config.Chat.EnableTellNotification;
               if (theme.ConfigCheckbox("Send Tell notification to other channel", ref tellNotif, () =>
               {
                   plugin.Config.Chat.EnableTellNotification = tellNotif;
                   plugin.Config.Save();
               }))
               {
               }

               if (tellNotif)
               {
                   using (ImRaii.PushIndent())
                   {
                       ImGui.TextDisabled("This will send a notification to the selected channel when you receive a tell.");
                       theme.SpacerY(0.5f);
                       ImGui.Text("Notification Channel:");
                       string currentNotifId = plugin.Config.Chat.TellNotificationChannelId;
                       theme.ChannelPicker(
                           "tell-notif-channel",
                           currentNotifId,
                           textChannels,
                           (newId) =>
                           {
                               plugin.Config.Chat.TellNotificationChannelId = newId;
                               plugin.Config.Save();
                           },
                           defaultLabel: "Select a Channel..."
                       );

                       theme.SpacerY(0.5f);
                       int cooldown = plugin.Config.Chat.TellNotificationCooldownSeconds;
                       ImGui.SetNextItemWidth(100f);
                       if (ImGui.InputInt("Conversation Cooldown (seconds)", ref cooldown))
                       {
                           if (cooldown < 0) cooldown = 0;
                           plugin.Config.Chat.TellNotificationCooldownSeconds = cooldown;
                           plugin.Config.Save();
                       }
                       if (ImGui.IsItemHovered())
                           ImGui.SetTooltip("Time in seconds to wait before sending another notification for the same active conversation.");
                   }
               }
           },
            drawHeaderRight: () =>
            {
                ImGui.TextDisabled("(?)");
                if (ImGui.IsItemHovered())
                {
                    string tip = "Info:\n";
                    tip += "- Every Chat Type can be mapped to a Discord Channel\n";
                    tip += "- You can also enable an advertisement filter for each chat type.\n";
                    tip += "\n";
                    tip += "- Tell needs to be mapped to a Forum to be sent to Discord.\n";
                    ImGui.SetTooltip(tip);
                }
            }
       );
    }

    private void DrawActiveTellsCard(IReadOnlyList<DiscordChannel>? forumChannels, ref bool enabled)
    {
        var tells = plugin.Config.Chat.TellThreadMappings;
        var availableThreads = cachedAvailableThreads;

        var headers = new[] { "Correspondent", "Thread ID / Name", "Action" };

        Action setupCols = () =>
        {
            ImGui.TableSetupColumn("Correspondent", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.NoReorder);
            ImGui.TableSetupColumn("Thread ID / Name", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.NoReorder);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.NoReorder, 80f * ImGuiHelpers.GlobalScale);
        };

        Func<string, string, string> getDisplayValue = (key, value) =>
        {
            if (ulong.TryParse(value, out var cid))
            {
                var name = plugin.ChannelCache.GetThreadName(cid);
                if (ulong.TryParse(name, out _)) return value; // It's still just the ID
                return $"#{name}";
            }
            return value;
        };

        UiTheme.DrawDictionaryEditUI drawEdit = (string key, ref string currentValue, Action cancel) =>
        {
            if (availableThreads.Count > 0)
            {
                string preview = currentValue;
                if (ulong.TryParse(currentValue, out var cid))
                {
                    var name = plugin.ChannelCache.GetThreadName(cid);
                    if (!ulong.TryParse(name, out _)) preview = $"#{name}";
                }

                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 50f * ImGuiHelpers.GlobalScale);
                using (var combo = ImRaii.Combo($"##threadSelect-{key}", preview))
                {
                    if (combo)
                    {
                        if (ulong.TryParse(currentValue, out var selectedId) && !availableThreads.ContainsKey(selectedId))
                        {
                            var name = plugin.ChannelCache.GetThreadName(selectedId);
                            if (ImGui.Selectable($"#{name}##{selectedId}", true)) { }
                            ImGui.Separator();
                        }

                        foreach (var thread in availableThreads)
                        {
                            bool isSelected = thread.Key.ToString() == currentValue;
                            if (ImGui.Selectable($"#{thread.Value}##{thread.Key}", isSelected))
                            {
                                currentValue = thread.Key.ToString();
                                plugin.NotificationManager.Add("Conversation Updated", $"Changed thread for {key}", CordiNotificationType.Success);
                            }
                            if (isSelected) ImGui.SetItemDefaultFocus();
                        }
                    }
                }
            }
            else
            {
                ImGui.TextColored(theme.MutedText, "No threads found.");
            }
        };

        bool dummy = true;
        theme.DrawDictionaryTable(
            "activeTells",
            $"Active Private Conversation: {tells.Count}",
            ref dummy,
            tells,
            () => plugin.Config.Save(),
            headers,
            setupColumns: setupCols,
            getDisplayValue: getDisplayValue,
            drawEditUI: drawEdit,
            collapsible: false
        );
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
