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


    private string? _editingTellKey = null;
    private bool _activeTellsExpanded = false;


    private bool _existingAvatarsExpanded = false;
    private string? _editingAvatarKey = null;
    private string _editingAvatarValue = "";


    private bool _showAddAvatarWindow = false;
    private string _addAvatarName = "";
    private string _addAvatarWorld = "";
    private string _addAvatarUrl = "";


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

        if (_showAddAvatarWindow) DrawAddAvatarWindow();

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

               if (ImGui.BeginTable("##mappingsTable", 2, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp))
               {
                   ImGui.TableSetupColumn("Chat Type", ImGuiTableColumnFlags.WidthFixed, 150f);
                   ImGui.TableSetupColumn("Discord Channel / Forum", ImGuiTableColumnFlags.WidthStretch);

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
        int count = plugin.Config.Chat.TellThreadMappings.Count;
        string title = $"Active Private Conversation: {count}";


        float scale = ImGuiHelpers.GlobalScale;
        float headerHeight = 35f * scale;
        if (headerHeight < ImGui.GetFrameHeightWithSpacing()) headerHeight = ImGui.GetFrameHeightWithSpacing();

        float padX = theme.PadX(0.9f);
        float padY = theme.PadY(0.9f);
        float radius = theme.Radius(1.0f);


        var draw = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var availW = ImGui.GetContentRegionAvail().X;

        draw.ChannelsSplit(2);
        draw.ChannelsSetCurrent(1);

        ImGui.BeginGroup();

        float titleCenterY = startPos.Y + (headerHeight - ImGui.GetTextLineHeight()) * 0.5f;
        ImGui.SetCursorScreenPos(new Vector2(startPos.X + padX, titleCenterY));

        ImGui.PushStyleColor(ImGuiCol.Text, theme.Text);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();


        ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
        string icon = _activeTellsExpanded ? FontAwesomeIcon.ChevronUp.ToIconString() : FontAwesomeIcon.ChevronDown.ToIconString();
        var iconSize = ImGui.CalcTextSize(icon);
        ImGui.SetCursorScreenPos(new Vector2(startPos.X + availW - padX - iconSize.X, titleCenterY));
        ImGui.TextUnformatted(icon);
        ImGui.PopFont();

        ImGui.SetCursorScreenPos(startPos);
        if (ImGui.InvisibleButton("##activeTellsHeaderBtn", new Vector2(availW, headerHeight)))
        {
            _activeTellsExpanded = !_activeTellsExpanded;
        }
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);


        if (_activeTellsExpanded)
        {
            ImGui.SetCursorScreenPos(new Vector2(startPos.X + padX, startPos.Y + headerHeight + theme.Gap(0.2f)));

            if (count == 0)
            {
                ImGui.TextColored(theme.MutedText, "No active private conversations.");
            }
            else
            {
                DrawActiveTellsTable(forumChannels, count);
            }


            ImGui.Dummy(new Vector2(0, padY * 0.5f));
        }

        ImGui.EndGroup();
        var itemMin = ImGui.GetItemRectMin();
        var itemMax = ImGui.GetItemRectMax();

        float totalHeight = itemMax.Y - startPos.Y;
        if (totalHeight < headerHeight) totalHeight = headerHeight;

        var endPos = new Vector2(startPos.X + availW, startPos.Y + totalHeight);
        draw.ChannelsSetCurrent(0);

        draw.AddRectFilled(startPos, endPos, ImGui.GetColorU32(theme.CardBg), radius);
        draw.AddRect(startPos, endPos, ImGui.GetColorU32(theme.WindowBorder), radius);

        var headerRectMax = new Vector2(endPos.X, startPos.Y + headerHeight);
        var mousePos = ImGui.GetMousePos();
        bool headerHovered = mousePos.X >= startPos.X && mousePos.X < endPos.X &&
                             mousePos.Y >= startPos.Y && mousePos.Y < headerRectMax.Y;

        if (headerHovered)
        {
            draw.AddRectFilled(startPos, headerRectMax, ImGui.GetColorU32(new Vector4(1, 1, 1, 0.05f)), radius, ImDrawFlags.RoundCornersTop);
        }

        draw.ChannelsMerge();

        theme.SpacerY(0.5f);
    }

    private void DrawActiveTellsTable(List<DiscordChannel>? forumChannels, int count)
    {

        var availableThreads = _cachedAvailableThreads;

        ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, theme.FrameBg);
        if (ImGui.BeginTable("##tellsTable", 3, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Correspondent", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.NoReorder);
            ImGui.TableSetupColumn("Thread ID / Name", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.NoReorder);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.NoReorder, 80f);

            var toRemove = new List<string>();
            var keys = plugin.Config.Chat.TellThreadMappings.Keys.ToList();

            foreach (var key in keys)
            {
                if (!plugin.Config.Chat.TellThreadMappings.ContainsKey(key)) continue;
                string currentThreadId = plugin.Config.Chat.TellThreadMappings[key];

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(key);

                ImGui.TableNextColumn();

                if (_editingTellKey == key)
                {
                    if (availableThreads.Count > 0)
                    {
                        string preview = currentThreadId;
                        if (ulong.TryParse(currentThreadId, out var cid) && availableThreads.TryGetValue(cid, out var name))
                        {
                            preview = $"#{name}";
                        }

                        float btnSize = ImGui.GetFrameHeight();
                        float spacing = ImGui.GetStyle().ItemSpacing.X;

                        float inputWidth = ImGui.GetContentRegionAvail().X - btnSize - spacing;

                        ImGui.SetNextItemWidth(inputWidth);
                        if (ImGui.BeginCombo($"##threadSelect-{key}", preview))
                        {
                            foreach (var kvp in availableThreads)
                            {
                                bool isSelected = kvp.Key.ToString() == currentThreadId;
                                if (ImGui.Selectable($"#{kvp.Value}##{kvp.Key}", isSelected))
                                {
                                    plugin.Config.Chat.TellThreadMappings[key] = kvp.Key.ToString();
                                    plugin.Config.Save();
                                    plugin.NotificationManager.Add("Conversation Updated", $"Changed thread for {key}", CordiNotificationType.Success);
                                    _editingTellKey = null;
                                }
                                if (isSelected) ImGui.SetItemDefaultFocus();
                            }
                            ImGui.EndCombo();
                        }

                        ImGui.SameLine();
                        ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                        if (ImGui.Button($"{FontAwesomeIcon.Times.ToIconString()}##cancel-{key}", new Vector2(btnSize, btnSize)))
                        {
                            _editingTellKey = null;
                        }
                        ImGui.PopFont();
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Cancel");
                    }
                    else
                    {
                        ImGui.TextColored(theme.MutedText, "No threads found.");
                        ImGui.SameLine();
                        ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                        if (ImGui.Button($"{FontAwesomeIcon.Times.ToIconString()}##cancel-{key}"))
                        {
                            _editingTellKey = null;
                        }
                        ImGui.PopFont();
                    }
                }
                else
                {
                    string display = currentThreadId;
                    if (ulong.TryParse(currentThreadId, out var cid) && availableThreads.TryGetValue(cid, out var name))
                    {
                        display = $"#{name}";
                    }

                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(display);

                    ImGui.SameLine();
                    ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
                    if (ImGui.Button($"{FontAwesomeIcon.Pen.ToIconString()}##edit-{key}"))
                    {
                        _editingTellKey = key;
                    }
                    ImGui.PopStyleColor();
                    ImGui.PopFont();
                    theme.HoverHandIfItem();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Change Thread");
                }

                ImGui.TableNextColumn();
                ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.56f, 0f, 0f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.1f, 0.1f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0f, 0f, 1f));
                if (ImGui.Button($"{FontAwesomeIcon.Trash.ToIconString()}##del-{key}"))
                {
                    toRemove.Add(key);
                }
                ImGui.PopStyleColor(3);
                ImGui.PopFont();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Remove Mapping");
            }
            ImGui.EndTable();

            if (toRemove.Count > 0)
            {
                foreach (var k in toRemove)
                {
                    plugin.Config.Chat.TellThreadMappings.Remove(k);
                    plugin.NotificationManager.Add("Conversation Removed", $"Forgot conversation with {k}", CordiNotificationType.Info);
                }
                plugin.Config.Save();
            }
        }
    }

    private void DrawExistingAvatarsCard(ref bool enabled)
    {
        int count = plugin.Config.Chat.CustomAvatars.Count;
        string title = $"Custom Avatars: {count}";


        float scale = ImGuiHelpers.GlobalScale;
        float headerHeight = 35f * scale;
        if (headerHeight < ImGui.GetFrameHeightWithSpacing()) headerHeight = ImGui.GetFrameHeightWithSpacing();

        float padX = theme.PadX(0.9f);
        float padY = theme.PadY(0.9f);
        float radius = theme.Radius(1.0f);

        var draw = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var availW = ImGui.GetContentRegionAvail().X;

        draw.ChannelsSplit(2);
        draw.ChannelsSetCurrent(1);

        ImGui.BeginGroup();

        float titleCenterY = startPos.Y + (headerHeight - ImGui.GetTextLineHeight()) * 0.5f;
        ImGui.SetCursorScreenPos(new Vector2(startPos.X + padX, titleCenterY));

        ImGui.PushStyleColor(ImGuiCol.Text, theme.Text);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();

        ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
        string icon = _existingAvatarsExpanded ? FontAwesomeIcon.ChevronUp.ToIconString() : FontAwesomeIcon.ChevronDown.ToIconString();
        var iconSize = ImGui.CalcTextSize(icon);
        ImGui.SetCursorScreenPos(new Vector2(startPos.X + availW - padX - iconSize.X, titleCenterY));
        ImGui.TextUnformatted(icon);
        ImGui.PopFont();

        ImGui.SetCursorScreenPos(startPos);
        if (ImGui.InvisibleButton("##existingAvatarsHeaderBtn", new Vector2(availW, headerHeight)))
        {
            _existingAvatarsExpanded = !_existingAvatarsExpanded;
        }
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);


        if (_existingAvatarsExpanded)
        {
            ImGui.SetCursorScreenPos(new Vector2(startPos.X + padX, startPos.Y + headerHeight + theme.Gap(0.2f)));

            if (count == 0)
            {
                ImGui.TextColored(theme.MutedText, "No custom avatars defined.");
            }
            else
            {
                if (ImGui.BeginTable("##custAvTable", 3, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("URL", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 60f);

                    var toRemove = new List<string>();
                    var keys = plugin.Config.Chat.CustomAvatars.Keys.ToList();

                    foreach (var key in keys)
                    {
                        var url = plugin.Config.Chat.CustomAvatars[key];

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text(key);

                        ImGui.TableNextColumn();

                        if (_editingAvatarKey == key)
                        {

                            float btnSize = ImGui.GetFrameHeight();
                            float spacing = ImGui.GetStyle().ItemSpacing.X;

                            float inputWidth = ImGui.GetContentRegionAvail().X - (btnSize * 2) - (spacing * 2);

                            ImGui.SetNextItemWidth(inputWidth);
                            ImGui.InputText($"##editUrl-{key}", ref _editingAvatarValue, 512);

                            ImGui.SameLine();
                            ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                            if (ImGui.Button($"{FontAwesomeIcon.Check.ToIconString()}##save-{key}", new Vector2(btnSize, btnSize)))
                            {
                                plugin.Config.Chat.CustomAvatars[key] = _editingAvatarValue;
                                plugin.Avatar.InvalidateAvatarCache(key);
                                plugin.Config.Save();
                                plugin.NotificationManager.Add("Custom Avatar Updated", $"Updated avatar for {key}", CordiNotificationType.Success);
                                _editingAvatarKey = null;
                            }
                            ImGui.PopFont();
                            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Save");

                            ImGui.SameLine();
                            ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                            if (ImGui.Button($"{FontAwesomeIcon.Times.ToIconString()}##cancel-{key}", new Vector2(btnSize, btnSize)))
                            {
                                _editingAvatarKey = null;
                            }
                            ImGui.PopFont();
                            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Cancel");
                        }
                        else
                        {

                            ImGui.Text(url);

                            ImGui.SameLine();
                            ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
                            if (ImGui.Button($"{FontAwesomeIcon.Pen.ToIconString()}##edit-{key}"))
                            {
                                _editingAvatarKey = key;
                                _editingAvatarValue = url;
                            }
                            ImGui.PopStyleColor();
                            ImGui.PopFont();
                            theme.HoverHandIfItem();
                            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Edit URL");
                        }

                        ImGui.TableNextColumn();
                        ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.56f, 0f, 0f, 1f)); // #900000
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.1f, 0.1f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0f, 0f, 1f));
                        if (ImGui.Button($"{FontAwesomeIcon.Trash.ToIconString()}##del-{key}"))
                        {
                            toRemove.Add(key);
                        }
                        ImGui.PopStyleColor(3);
                        ImGui.PopFont();
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Remove Avatar");
                    }
                    ImGui.EndTable();

                    if (toRemove.Count > 0)
                    {
                        foreach (var k in toRemove)
                        {
                            plugin.Config.Chat.CustomAvatars.Remove(k);
                            plugin.Avatar.InvalidateAvatarCache(k);
                            plugin.NotificationManager.Add("Custom Avatar Deleted", $"Removed avatar for {k}", CordiNotificationType.Info);
                        }
                        plugin.Config.Save();
                    }
                }
            }

            theme.SpacerY(0.5f);


            if (ImGui.Button("Add Custom Avatar", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
            {
                _showAddAvatarWindow = true;
                _addAvatarName = "";
                _addAvatarWorld = "";
                _addAvatarUrl = "";
            }

            ImGui.Dummy(new Vector2(0, padY * 0.5f));
        }

        ImGui.EndGroup();
        var itemMin = ImGui.GetItemRectMin();
        var itemMax = ImGui.GetItemRectMax();

        float totalHeight = itemMax.Y - startPos.Y;
        if (totalHeight < headerHeight) totalHeight = headerHeight;

        var endPos = new Vector2(startPos.X + availW, startPos.Y + totalHeight);
        draw.ChannelsSetCurrent(0);

        draw.AddRectFilled(startPos, endPos, ImGui.GetColorU32(theme.CardBg), radius);
        draw.AddRect(startPos, endPos, ImGui.GetColorU32(theme.WindowBorder), radius);

        var headerRectMax = new Vector2(endPos.X, startPos.Y + headerHeight);
        var mousePos = ImGui.GetMousePos();
        bool headerHovered = mousePos.X >= startPos.X && mousePos.X < endPos.X &&
                             mousePos.Y >= startPos.Y && mousePos.Y < headerRectMax.Y;

        if (headerHovered)
        {
            draw.AddRectFilled(startPos, headerRectMax, ImGui.GetColorU32(new Vector4(1, 1, 1, 0.05f)), radius, ImDrawFlags.RoundCornersTop);
        }

        draw.ChannelsMerge();

        theme.SpacerY(0.5f);
    }

    private void DrawAddAvatarWindow()
    {
        bool open = _showAddAvatarWindow;
        if (ImGui.Begin("Add Custom Avatar", ref open, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize))
        {
            float fullWidth = 400f * ImGuiHelpers.GlobalScale;

            ImGui.Text("Character Name");
            ImGui.SetNextItemWidth(fullWidth);
            ImGui.InputText("##add-ca-name", ref _addAvatarName, 64);

            theme.SpacerY(0.5f);

            ImGui.Text("World");
            ImGui.SetNextItemWidth(fullWidth);
            ImGui.InputText("##add-ca-world", ref _addAvatarWorld, 32);

            theme.SpacerY(0.5f);

            ImGui.Text("Image URL (Publicly Accessible)");
            ImGui.SetNextItemWidth(fullWidth);
            ImGui.InputText("##add-ca-url", ref _addAvatarUrl, 512);

            theme.SpacerY(1f);
            ImGui.Separator();
            theme.SpacerY(1f);

            if (ImGui.Button("Add", new Vector2(100f * ImGuiHelpers.GlobalScale, 0)))
            {
                if (!string.IsNullOrEmpty(_addAvatarName) && !string.IsNullOrEmpty(_addAvatarWorld) && !string.IsNullOrEmpty(_addAvatarUrl))
                {
                    string key = $"{_addAvatarName}@{_addAvatarWorld}";
                    plugin.Config.Chat.CustomAvatars[key] = _addAvatarUrl;
                    plugin.Avatar.InvalidateAvatarCache(key);
                    plugin.Config.Save();
                    plugin.NotificationManager.Add("Custom Avatar Created", $"Added avatar for {key}", CordiNotificationType.Success);
                    _showAddAvatarWindow = false;
                }
            }

            ImGui.SameLine();

            if (ImGui.Button("Cancel", new Vector2(100f * ImGuiHelpers.GlobalScale, 0)))
            {
                _showAddAvatarWindow = false;
            }

            ImGui.End();
        }

        if (!open) _showAddAvatarWindow = false;
    }
}
