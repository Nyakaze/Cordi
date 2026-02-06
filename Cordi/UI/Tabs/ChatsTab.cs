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







    private DateTime _lastChannelFetch = DateTime.MinValue;
    private readonly TimeSpan _cacheInterval = TimeSpan.FromSeconds(5);
    private List<DiscordChannel> _cachedTextChannels = new();
    private List<DiscordChannel> _cachedForumChannels = new();
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


        if (DateTime.Now - _lastChannelFetch > _cacheInterval)
        {
            RefreshChannelCache();
        }

        var textChannels = _cachedTextChannels;
        var forumChannels = _cachedForumChannels;


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

    private void RefreshChannelCache()
    {
        _lastChannelFetch = DateTime.Now;
        var allChannels = plugin.Discord.Client?.Guilds.Values
            .SelectMany(g => g.Channels.Values)
            .ToList();

        if (allChannels == null)
        {
            _cachedTextChannels.Clear();
            _cachedForumChannels.Clear();
        }
        else
        {
            _cachedTextChannels = allChannels.Where(c => c.Type == ChannelType.Text).ToList();
            _cachedForumChannels = allChannels.Where(c => c.Type == ChannelType.GuildForum).ToList();
        }


        _cachedAvailableThreads.Clear();
        var tellMap = plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == XivChatType.TellIncoming);
        if (tellMap != null && ulong.TryParse(tellMap.DiscordChannelId, out var forumId) && plugin.Discord.Client != null)
        {
            foreach (var guild in plugin.Discord.Client.Guilds.Values)
            {
                if (guild.Channels.ContainsKey(forumId))
                {
                    foreach (var thread in guild.Threads.Values)
                    {
                        if (thread.ParentId == forumId)
                        {
                            _cachedAvailableThreads[thread.Id] = thread.Name;
                        }
                    }
                    break;
                }
            }
        }
    }

    private void DrawDefaultChannelCard(List<DiscordChannel>? textChannels, ref bool enabled)
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
                    string preview = "Select a Channel...";
                    if (!string.IsNullOrEmpty(defaultChannelId))
                    {
                        var current = textChannels.FirstOrDefault(c => c.Id.ToString() == defaultChannelId);
                        if (current != null) preview = $"#{current.Name}";
                        else preview = defaultChannelId;
                    }

                    ImGui.PushItemWidth(availWidth);
                    if (ImGui.BeginCombo("##dsc-default-channel-combo", preview))
                    {
                        if (ImGui.Selectable("None", string.IsNullOrEmpty(defaultChannelId)))
                        {
                            defaultChannelId = string.Empty;
                            plugin.Config.Discord.DefaultChannelId = defaultChannelId;
                            plugin.Config.Save();
                        }

                        foreach (var channel in textChannels)
                        {
                            bool isSelected = channel.Id.ToString() == defaultChannelId;
                            if (ImGui.Selectable($"#{channel.Name}", isSelected))
                            {
                                defaultChannelId = channel.Id.ToString();
                                plugin.Config.Discord.DefaultChannelId = defaultChannelId;
                                plugin.Config.Save();
                                _lastChannelFetch = DateTime.MinValue;
                            }
                            if (isSelected) ImGui.SetItemDefaultFocus();
                        }
                        ImGui.EndCombo();
                    }
                    ImGui.PopItemWidth();
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

    private void DrawChatMappingsCard(List<DiscordChannel>? textChannels, List<DiscordChannel>? forumChannels, ref bool enabled)
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
                       ImGui.SetNextItemWidth(-1);

                       string currentId = "";
                       if (plugin.Config.MappingCache.TryGetValue(chatType, out var cachedId)) currentId = cachedId;

                       string preview = "None";

                       bool isTell = chatType == XivChatType.TellIncoming;
                       var targetChannels = isTell ? forumChannels : textChannels;

                       if (!string.IsNullOrEmpty(currentId) && targetChannels != null)
                       {
                           var ch = targetChannels.FirstOrDefault(c => c.Id.ToString() == currentId);
                           if (ch != null) preview = $"#{ch.Name}";
                           else preview = currentId;
                       }

                       if (ImGui.BeginCombo($"##combo-{chatType}", preview))
                       {
                           if (ImGui.Selectable("None", string.IsNullOrEmpty(currentId)))
                           {
                               var map = plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == chatType);
                               if (map != null) plugin.Config.Chat.Mappings.Remove(map);
                               if (isTell)
                               {
                                   var mapOut = plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == XivChatType.TellOutgoing);
                                   if (mapOut != null) plugin.Config.Chat.Mappings.Remove(mapOut);
                               }
                               plugin.Config.Save();
                           }

                           if (targetChannels != null)
                           {
                               foreach (var channel in targetChannels)
                               {
                                   bool isSelected = channel.Id.ToString() == currentId;
                                   if (ImGui.Selectable($"#{channel.Name}", isSelected))
                                   {
                                       string newId = channel.Id.ToString();

                                       var map = plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == chatType);

                                       if (map != null) map.DiscordChannelId = newId;
                                       else plugin.Config.Chat.Mappings.Add(new ChannelMapping { GameChatType = chatType, DiscordChannelId = newId });

                                       if (isTell)
                                       {
                                           var mapOut = plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == XivChatType.TellOutgoing);
                                           if (mapOut != null) mapOut.DiscordChannelId = newId;
                                           else plugin.Config.Chat.Mappings.Add(new ChannelMapping { GameChatType = XivChatType.TellOutgoing, DiscordChannelId = newId });
                                       }
                                       plugin.Config.Save();
                                       _lastChannelFetch = DateTime.MinValue;
                                   }
                                   if (isSelected) ImGui.SetItemDefaultFocus();
                               }
                           }
                           ImGui.EndCombo();
                       }
                       theme.HoverHandIfItem();

                       // Advertisement Filter checkbox
                       ImGui.TableNextColumn();
                       var mapping = plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == chatType);
                       if (mapping != null && !string.IsNullOrEmpty(mapping.DiscordChannelId))
                       {
                           bool filterEnabled = mapping.EnableAdvertisementFilter;
                           if (ImGui.Checkbox($"##filter-{chatType}", ref filterEnabled))
                           {
                               mapping.EnableAdvertisementFilter = filterEnabled;
                               plugin.Config.Save();
                           }
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
           }
       );
    }

    private void DrawActiveTellsCard(List<DiscordChannel>? forumChannels, ref bool enabled)
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
