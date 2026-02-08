using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using DSharpPlus;
using DSharpPlus.Entities;
using Dalamud.Bindings.ImGui;
using Cordi.Services;

using Cordi.Core;
using Cordi.UI.Themes;
using Cordi.Configuration;

namespace Cordi.UI.Tabs;

public class ChatsTab
{
    private readonly CordiPlugin plugin;
    private readonly UiTheme theme;



    private bool _activeTellsExpanded = false;


    private bool _existingAvatarsExpanded = false;







    private Dictionary<ulong, string> _cachedAvailableThreads = new();


    private readonly XivChatType[] supportedChatTypes = new[]
    {
        XivChatType.Say, XivChatType.Shout, XivChatType.Yell,
        XivChatType.Party, XivChatType.Alliance, XivChatType.FreeCompany,
        XivChatType.TellIncoming
    };

    public ChatsTab(CordiPlugin plugin, UiTheme theme)
    {
        this.plugin = plugin;
        this.theme = theme;
    }

    public void Draw()
    {
        theme.SpacerY(2f);
        bool enabled = true;


        plugin.ChannelCache.RefreshIfNeeded();
        RefreshThreadCache();

        var textChannels = plugin.ChannelCache.TextChannels;
        var forumChannels = plugin.ChannelCache.ForumChannels;


        DrawDefaultChannelCard(textChannels, ref enabled);

        theme.SpacerY(2f);

        ImGui.Separator();
        theme.SpacerY(2f);

        DrawActiveTellsCard(forumChannels, ref enabled);

        theme.SpacerY(1f);


        DrawExistingAvatarsCard(ref enabled);

        theme.SpacerY(1f);

        ImGui.Separator();
        theme.SpacerY(2f);

        DrawChatMappingsCard(textChannels, forumChannels, ref enabled);



        theme.SpacerY(2f);
    }

    private void RefreshThreadCache()
    {
        _cachedAvailableThreads.Clear();
        var tellMap = plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == XivChatType.TellIncoming);
        if (tellMap != null && ulong.TryParse(tellMap.DiscordChannelId, out var forumId))
        {
            _cachedAvailableThreads = plugin.ChannelCache.GetThreadsForForum(forumId);
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

                    ImGui.PushItemWidth(availWidth);
                    bool changed = ImGui.InputText("##dsc-default-channel-id", ref defaultChannelId, 32);
                    ImGui.PopItemWidth();
                    if (changed)
                    {
                        plugin.Config.Discord.DefaultChannelId = defaultChannelId;
                        plugin.Config.Save();
                    }
                }
                theme.HoverHandIfItem();
            }
        );
    }

    private void DrawChatMappingsCard(IReadOnlyList<DiscordChannel>? textChannels, IReadOnlyList<DiscordChannel>? forumChannels, ref bool enabled)
    {
        theme.DrawPluginCardAuto(
           id: "chat-mappings-card",
           enabled: ref enabled,
           showCheckbox: false,
           title: "Chat Mappings",
           drawContent: (avail) =>
           {
               ImGui.TextColored(theme.MutedText, "Assign Discord channels to Game Chat types.");
               theme.SpacerY(1f);

               if (ImGui.BeginTable("##mappingsTable", 3, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp))
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
                   bool expandLS = ImGui.TreeNode("Linkshell");
                   ImGui.TableNextColumn();
                   if (expandLS)
                   {
                       drawRow(XivChatType.Ls1);
                       drawRow(XivChatType.Ls2);
                       drawRow(XivChatType.Ls3);
                       drawRow(XivChatType.Ls4);
                       drawRow(XivChatType.Ls5);
                       drawRow(XivChatType.Ls6);
                       drawRow(XivChatType.Ls7);
                       drawRow(XivChatType.Ls8);
                       ImGui.TreePop();
                   }

                   ImGui.TableNextRow();
                   ImGui.TableNextColumn();
                   bool expandCWLS = ImGui.TreeNode("Cross-World Linkshells");
                   ImGui.TableNextColumn();
                   if (expandCWLS)
                   {
                       drawRow(XivChatType.CrossLinkShell1);
                       drawRow(XivChatType.CrossLinkShell2);
                       drawRow(XivChatType.CrossLinkShell3);
                       drawRow(XivChatType.CrossLinkShell4);
                       drawRow(XivChatType.CrossLinkShell5);
                       drawRow(XivChatType.CrossLinkShell6);
                       drawRow(XivChatType.CrossLinkShell7);
                       drawRow(XivChatType.CrossLinkShell8);
                       ImGui.TreePop();
                   }

                   ImGui.EndTable();
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
                    tip += "- Tell needs to be mapped to a thread to be sent to Discord.\n";
                    ImGui.SetTooltip(tip);
                }
            }
       );
    }

    private void DrawActiveTellsCard(IReadOnlyList<DiscordChannel>? forumChannels, ref bool enabled)
    {
        var tells = plugin.Config.Chat.TellThreadMappings;
        var availableThreads = _cachedAvailableThreads;

        var headers = new[] { "Correspondent", "Thread ID / Name", "Action" };

        Action setupCols = () =>
        {
            ImGui.TableSetupColumn("Correspondent", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.NoReorder);
            ImGui.TableSetupColumn("Thread ID / Name", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.NoReorder);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.NoReorder, 80f * ImGuiHelpers.GlobalScale);
        };

        Func<string, string, string> getDisplayValue = (key, value) =>
        {
            if (ulong.TryParse(value, out var cid) && availableThreads.TryGetValue(cid, out var name))
                return $"#{name}";
            return value;
        };

        UiTheme.DrawDictionaryEditUI drawEdit = (string key, ref string currentValue, Action cancel) =>
        {
            if (availableThreads.Count > 0)
            {
                string preview = currentValue;
                if (ulong.TryParse(currentValue, out var cid) && availableThreads.TryGetValue(cid, out var name))
                {
                    preview = $"#{name}";
                }

                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 50f * ImGuiHelpers.GlobalScale);
                if (ImGui.BeginCombo($"##threadSelect-{key}", preview))
                {
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
                    ImGui.EndCombo();
                }
            }
            else
            {
                ImGui.TextColored(theme.MutedText, "No threads found.");
            }
        };

        theme.DrawDictionaryTable(
            "activeTells",
            $"Active Private Conversation: {tells.Count}",
            ref _activeTellsExpanded,
            tells,
            () => plugin.Config.Save(),
            headers,
            setupColumns: setupCols,
            getDisplayValue: getDisplayValue,
            drawEditUI: drawEdit
        );
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
            ref _existingAvatarsExpanded,
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
            allowAdd: true
        );
    }

}
