using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Cordi.Services;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using DSharpPlus;
using DSharpPlus.Entities;
using Dalamud.Bindings.ImGui;
using System.Collections.Generic;

using Cordi.Core;
using Cordi.UI.Themes;
using Cordi.Configuration;

namespace Cordi.UI.Tabs;

public class DebugTab
{
    private readonly CordiPlugin plugin;
    private readonly UiTheme theme;


    private string sendTestMessage = "Test message from Cordi plugin.";
    private string sendUser = "Nya@Alpaha";
    private XivChatType selectedChatType = XivChatType.None;


    private string newAvatarName = "";
    private string newAvatarWorld = "";
    private string newAvatarUrl = "";


    private List<DiscordGuild> _cachedGuilds = new();
    private DateTime _lastGuildFetch = DateTime.MinValue;
    private readonly TimeSpan _cacheInterval = TimeSpan.FromSeconds(5);

    public DebugTab(CordiPlugin plugin, UiTheme theme)
    {
        this.plugin = plugin;
        this.theme = theme;
    }

    public void Draw()
    {
        theme.SpacerY(2f);
        DrawStatusCard();

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);

        DrawThroughputStats();

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);

        DrawLodestoneCache();

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);

        DrawMessageSimulator();

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);

        DrawCordiPeepDebug();

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);

        DrawEmoteLogDebug();

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);

        DrawPartyDebug();

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);

        DrawStateInspector();

    }

    private void DrawPartyDebug()
    {
        bool unused = true;
        theme.DrawPluginCardAuto(
            id: "party-debug-card",
            enabled: ref unused,
            showCheckbox: false,
            title: "Party Tracker Debug",
            drawContent: (avail) =>
            {
                ImGui.TextColored(theme.MutedText, "Simulate Party Events.");

                ImGui.Text("Name:");
                ImGui.SetNextItemWidth(avail / 2);
                ImGui.InputText("##partyDebugName", ref _debugName, 64);

                ImGui.SameLine();
                ImGui.Text("World:");

                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                ImGui.InputText("##partyDebugWorld", ref _debugWorld, 64);

                theme.SpacerY(0.5f);

                if (ImGui.Button("Trigger Join"))
                {
                    // Disabled: PartyService.NotifyJoin now requires IPartyMember parameter
                    // plugin.PartyService?.DebugTriggerJoin(_debugName, _debugWorld);
                }
                theme.HoverHandIfItem();

                ImGui.SameLine();

                if (ImGui.Button("Trigger Leave"))
                {
                    plugin.PartyService?.DebugTriggerLeave(_debugName, _debugWorld);
                }
                theme.HoverHandIfItem();

                ImGui.SameLine();

                if (ImGui.Button("Trigger Full Party"))
                {
                    plugin.PartyService?.DebugTriggerFull();
                }
                theme.HoverHandIfItem();
            }
        );
    }


    private string _debugName = "Test Player";
    private string _debugWorld = "Test World";

    private void DrawCordiPeepDebug()
    {
        bool unused = true;
        theme.DrawPluginCardAuto(
            id: "cordi-peep-debug-card",
            enabled: ref unused,
            showCheckbox: false,
            title: "Peeper Debugging",
            drawContent: (avail) =>
            {
                ImGui.TextColored(theme.MutedText, "Simulate players targeting you.");

                ImGui.Text("Name:");
                ImGui.SetNextItemWidth(avail / 2);
                ImGui.InputText("##debugName", ref _debugName, 64);

                ImGui.SameLine();
                ImGui.Text("World:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                ImGui.InputText("##debugWorld", ref _debugWorld, 64);

                theme.SpacerY(0.5f);

                if (ImGui.Button("Add Test Peeper"))
                {
                    plugin.CordiPeep?.AddSimulatedPeeper(_debugName, _debugWorld);
                }
                theme.HoverHandIfItem();

                ImGui.SameLine();

                if (ImGui.Button("Remove Test Peeper"))
                {
                    plugin.CordiPeep?.RemoveSimulatedPeeper(_debugName);
                }
                theme.HoverHandIfItem();

                theme.SpacerY(0.5f);

                ImGui.TextColored(theme.MutedText, "Note: Targeting/Examining simulated players will not work as they don't exist.");
            }
        );
    }

    private void DrawStatusCard()
    {
        bool botStarted = plugin.Config.Discord.BotStarted;
        bool unused = true;

        theme.DrawPluginCardAuto(
            id: "debug-status-card",
            enabled: ref unused,
            showCheckbox: false,
            title: "System Status",
            drawContent: (avail) =>
            {
                var client = plugin.Discord.Client;
                bool isConnected = client != null && botStarted;


                ImGui.BeginGroup();
                ImGui.TextColored(theme.MutedText, "Discord Connection");
                if (botStarted)
                {
                    if (isConnected)
                    {
                        ImGui.TextColored(UiTheme.ColorSuccessText, $"Connected (Ping: {client?.Ping ?? 0}ms)");
                        ImGui.SameLine();
                        ImGui.TextColored(theme.MutedText, $"| Gateway: v{client?.GatewayVersion ?? 0}");
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(1f, 0.64f, 0f, 1f), "Initializing / Connecting...");
                    }
                }
                else
                {
                    ImGui.TextColored(UiTheme.ColorDangerText, "Stopped");
                }
                ImGui.EndGroup();

                ImGui.SameLine(avail / 2f);

                ImGui.BeginGroup();
                ImGui.TextColored(theme.MutedText, "Current User");
                if (isConnected && client?.CurrentUser != null)
                {
                    ImGui.Text($"{client.CurrentUser.Username}#{client.CurrentUser.Discriminator}");
                    ImGui.TextColored(theme.MutedText, $"ID: {client.CurrentUser.Id}");
                }
                else
                {
                    ImGui.Text("-");
                }
                ImGui.EndGroup();

                theme.SpacerY(1f);

                if (ImGui.Button("Restart Bot##dbgRestart", new Vector2(120, 0)))
                {
                    plugin.Discord.Stop();
                    plugin.Discord.Start();
                }
                theme.HoverHandIfItem();

                ImGui.SameLine();

                if (ImGui.Button("Force Reconnect", new Vector2(150, 0)))
                {
                    if (isConnected && client != null)
                    {
                        _ = client.ReconnectAsync();
                    }
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Attempt to reconnect the gateway socket.");
            }
        );
    }

    private void DrawMessageSimulator()
    {
        bool unused = true;
        theme.DrawPluginCardAuto(
            id: "debug-simulator-card",
            enabled: ref unused,
            showCheckbox: false,
            title: "Message Simulator",
            drawContent: (avail) =>
            {

                theme.SpacerY(0.5f);

                ImGui.SetNextItemWidth(200f);
                if (ImGui.BeginCombo("##dbgChatType", selectedChatType.ToString()))
                {
                    foreach (var val in Enum.GetValues<XivChatType>())
                    {
                        if (ImGui.Selectable(val.ToString(), val == selectedChatType))
                        {
                            selectedChatType = val;
                        }
                    }
                    ImGui.EndCombo();
                }
                ImGui.SameLine();
                ImGui.TextColored(theme.MutedText, "Chat Type");

                theme.SpacerY(0.5f);

                float half = avail / 2f - ImGui.GetStyle().ItemSpacing.X;

                ImGui.BeginGroup();
                ImGui.Text("Sender (Name@World)");
                ImGui.SetNextItemWidth(half);
                ImGui.InputText("##dbgSendUser", ref sendUser, 64);
                ImGui.EndGroup();

                ImGui.SameLine();

                ImGui.BeginGroup();
                ImGui.Text("Message Content");
                ImGui.SetNextItemWidth(half);
                ImGui.InputText("##dbgSendMsg", ref sendTestMessage, 128);
                ImGui.EndGroup();

                theme.SpacerY(1f);

                if (ImGui.Button("Send to Discord (Forwarding Test)", new Vector2(200, 0)))
                {
                    var parts = sendUser.Split('@');
                    var name = parts[0];
                    var world = parts.Length > 1 ? parts[1] : "Unknown";
                    _ = plugin.Discord.SendMessage(null, sendTestMessage, name, world, selectedChatType);
                }
                theme.HoverHandIfItem();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Triggers the 'SendMessage' method as if caught from game chat.");

                ImGui.SameLine();

                if (ImGui.Button("Inject into Game Chat", new Vector2(200, 0)))
                {
                    _ = plugin._chat.SendAsync(selectedChatType, sendTestMessage);
                }
                theme.HoverHandIfItem();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Injects message into local game chat log.");
            }
        );
    }

    private void DrawThroughputStats()
    {
        var stats = plugin.Config.Stats;
        bool unused = true;

        theme.DrawPluginCardAuto(
            id: "debug-throughput-card",
            enabled: ref unused,
            showCheckbox: false,
            title: "Throughput",
            drawContent: (avail) =>
            {
                ImGui.TextColored(UiTheme.ColorSuccessText, $"Total Messages Processed: {stats.TotalMessages}");
                ImGui.SameLine(avail / 3f);
                ImGui.TextColored(UiTheme.ColorSuccessText, $"Total Peeps Tracked: {stats.TotalPeepsTracked}");
                ImGui.SameLine(avail * 2f / 3f);
                ImGui.TextColored(UiTheme.ColorSuccessText, $"Total Emotes Tracked: {stats.TotalEmotesTracked}");

                theme.SpacerY(1f);


                if (ImGui.BeginTabBar("##throughputTabs"))
                {
                    if (ImGui.BeginTabItem("Game Chat Types"))
                    {

                        float tableHeight = 200f * ImGuiHelpers.GlobalScale;

                        Dictionary<XivChatType, long> snapshot;
                        lock (stats)
                        {
                            snapshot = new Dictionary<XivChatType, long>(stats.ChatTypeStats);
                        }

                        if (snapshot.Count == 0)
                        {
                            ImGui.TextColored(theme.MutedText, "No data yet.");
                        }
                        else
                        {
                            if (ImGui.BeginChild("##thruChatChild", new Vector2(0, tableHeight), false))
                            {
                                ImGui.Indent(theme.PadX(0.5f));
                                if (ImGui.BeginTable("##thruChatTable_v2", 2, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Sortable))
                                {
                                    ImGui.TableSetupColumn("Chat Type");
                                    ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending, 80f);
                                    ImGui.TableHeadersRow();

                                    var sortSpecs = ImGui.TableGetSortSpecs();
                                    if (sortSpecs.SpecsDirty)
                                    {
                                        sortSpecs.SpecsDirty = false;
                                    }

                                    var sortedSnapshot = snapshot.AsEnumerable();
                                    if (sortSpecs.SpecsCount > 0)
                                    {
                                        var spec = sortSpecs.Specs;
                                        if (spec.ColumnIndex == 0)
                                            sortedSnapshot = spec.SortDirection == ImGuiSortDirection.Ascending ? sortedSnapshot.OrderBy(x => x.Key.ToString()) : sortedSnapshot.OrderByDescending(x => x.Key.ToString());
                                        else
                                            sortedSnapshot = spec.SortDirection == ImGuiSortDirection.Ascending ? sortedSnapshot.OrderBy(x => x.Value) : sortedSnapshot.OrderByDescending(x => x.Value);
                                    }
                                    else
                                    {
                                        sortedSnapshot = sortedSnapshot.OrderByDescending(x => x.Value);
                                    }

                                    foreach (var kvp in sortedSnapshot)
                                    {
                                        ImGui.TableNextRow();
                                        ImGui.TableNextColumn();
                                        ImGui.Text(kvp.Key.ToString());
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{kvp.Value}");
                                    }
                                    ImGui.EndTable();
                                }
                                ImGui.Unindent(theme.PadX(0.5f));
                            }
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Tell Targets"))
                    {
                        float tableHeight = 200f * ImGuiHelpers.GlobalScale;
                        Dictionary<string, long> snapshot;
                        lock (stats)
                        {
                            snapshot = new Dictionary<string, long>(stats.TellStats);
                        }

                        if (snapshot.Count == 0)
                        {
                            ImGui.TextColored(theme.MutedText, "No data yet.");
                        }
                        else
                        {
                            if (ImGui.BeginChild("##thruTellChild", new Vector2(0, tableHeight), false))
                            {
                                ImGui.Indent(theme.PadX(0.5f));
                                if (ImGui.BeginTable("##thruTellTable_v2", 2, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Sortable))
                                {
                                    ImGui.TableSetupColumn("Target");
                                    ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending, 80f);
                                    ImGui.TableHeadersRow();

                                    var sortSpecs = ImGui.TableGetSortSpecs();
                                    if (sortSpecs.SpecsDirty)
                                    {
                                        sortSpecs.SpecsDirty = false;
                                    }
                                    var sortedSnapshot = snapshot.AsEnumerable();
                                    if (sortSpecs.SpecsCount > 0)
                                    {
                                        var spec = sortSpecs.Specs;
                                        if (spec.ColumnIndex == 0)
                                            sortedSnapshot = spec.SortDirection == ImGuiSortDirection.Ascending ? sortedSnapshot.OrderBy(x => x.Key) : sortedSnapshot.OrderByDescending(x => x.Key);
                                        else
                                            sortedSnapshot = spec.SortDirection == ImGuiSortDirection.Ascending ? sortedSnapshot.OrderBy(x => x.Value) : sortedSnapshot.OrderByDescending(x => x.Value);
                                    }
                                    else
                                    {
                                        sortedSnapshot = sortedSnapshot.OrderByDescending(x => x.Value);
                                    }

                                    foreach (var kvp in sortedSnapshot)
                                    {
                                        ImGui.TableNextRow();
                                        ImGui.TableNextColumn();
                                        ImGui.Text(kvp.Key);
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{kvp.Value}");
                                    }
                                    ImGui.EndTable();
                                }
                                ImGui.Unindent(theme.PadX(0.5f));
                            }
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Peeps"))
                    {
                        float tableHeight = 200f * ImGuiHelpers.GlobalScale;
                        Dictionary<string, PeeperStats> snapshot;
                        lock (stats)
                        {
                            snapshot = new Dictionary<string, PeeperStats>(stats.PeepStats);
                        }

                        if (snapshot.Count == 0)
                        {
                            ImGui.TextColored(theme.MutedText, "No data yet.");
                        }
                        else
                        {
                            if (ImGui.BeginChild("##thruPeepChild", new Vector2(0, tableHeight), false))
                            {
                                ImGui.Indent(theme.PadX(0.5f));
                                if (ImGui.BeginTable("##thruPeepTable_v2", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Sortable))
                                {
                                    ImGui.TableSetupColumn("Player");
                                    ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending, 80f);
                                    ImGui.TableSetupColumn("Last Seen", ImGuiTableColumnFlags.WidthFixed, 140f);
                                    ImGui.TableHeadersRow();

                                    var sortSpecs = ImGui.TableGetSortSpecs();
                                    if (sortSpecs.SpecsDirty)
                                    {
                                        sortSpecs.SpecsDirty = false;
                                    }
                                    var sortedSnapshot = snapshot.AsEnumerable();
                                    if (sortSpecs.SpecsCount > 0)
                                    {
                                        var spec = sortSpecs.Specs;
                                        if (spec.ColumnIndex == 0)
                                            sortedSnapshot = spec.SortDirection == ImGuiSortDirection.Ascending ? sortedSnapshot.OrderBy(x => x.Value.Name).ThenBy(x => x.Value.World) : sortedSnapshot.OrderByDescending(x => x.Value.Name).ThenByDescending(x => x.Value.World);
                                        else if (spec.ColumnIndex == 1)
                                            sortedSnapshot = spec.SortDirection == ImGuiSortDirection.Ascending ? sortedSnapshot.OrderBy(x => x.Value.Count) : sortedSnapshot.OrderByDescending(x => x.Value.Count);
                                        else
                                            sortedSnapshot = spec.SortDirection == ImGuiSortDirection.Ascending ? sortedSnapshot.OrderBy(x => x.Value.LastSeen) : sortedSnapshot.OrderByDescending(x => x.Value.LastSeen);
                                    }
                                    else
                                    {
                                        sortedSnapshot = sortedSnapshot.OrderByDescending(x => x.Value.Count);
                                    }

                                    foreach (var kvp in sortedSnapshot)
                                    {
                                        ImGui.TableNextRow();
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{kvp.Value.Name}@{kvp.Value.World}");
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{kvp.Value.Count}");
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{kvp.Value.LastSeen:yyyy-MM-dd HH:mm:ss}");
                                    }
                                    ImGui.EndTable();
                                }
                                ImGui.Unindent(theme.PadX(0.5f));
                            }
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Emotes"))
                    {
                        float tableHeight = 200f * ImGuiHelpers.GlobalScale;
                        Dictionary<string, PeeperStats> snapshot;
                        lock (stats)
                        {
                            snapshot = new Dictionary<string, PeeperStats>(stats.EmoteStats);
                        }

                        if (snapshot.Count == 0)
                        {
                            ImGui.TextColored(theme.MutedText, "No data yet.");
                        }
                        else
                        {
                            if (ImGui.BeginChild("##thruEmoteChild", new Vector2(0, tableHeight), false))
                            {
                                ImGui.Indent(theme.PadX(0.5f));
                                if (ImGui.BeginTable("##thruEmoteTable_v2", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Sortable))
                                {
                                    ImGui.TableSetupColumn("Player");
                                    ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending, 80f);
                                    ImGui.TableSetupColumn("Last Seen", ImGuiTableColumnFlags.WidthFixed, 140f);
                                    ImGui.TableHeadersRow();

                                    var sortSpecs = ImGui.TableGetSortSpecs();
                                    if (sortSpecs.SpecsDirty)
                                    {
                                        sortSpecs.SpecsDirty = false;
                                    }
                                    var sortedSnapshot = snapshot.AsEnumerable();
                                    if (sortSpecs.SpecsCount > 0)
                                    {
                                        var spec = sortSpecs.Specs;
                                        if (spec.ColumnIndex == 0)
                                            sortedSnapshot = spec.SortDirection == ImGuiSortDirection.Ascending ? sortedSnapshot.OrderBy(x => x.Value.Name).ThenBy(x => x.Value.World) : sortedSnapshot.OrderByDescending(x => x.Value.Name).ThenByDescending(x => x.Value.World);
                                        else if (spec.ColumnIndex == 1)
                                            sortedSnapshot = spec.SortDirection == ImGuiSortDirection.Ascending ? sortedSnapshot.OrderBy(x => x.Value.Count) : sortedSnapshot.OrderByDescending(x => x.Value.Count);
                                        else
                                            sortedSnapshot = spec.SortDirection == ImGuiSortDirection.Ascending ? sortedSnapshot.OrderBy(x => x.Value.LastSeen) : sortedSnapshot.OrderByDescending(x => x.Value.LastSeen);
                                    }
                                    else
                                    {
                                        sortedSnapshot = sortedSnapshot.OrderByDescending(x => x.Value.Count);
                                    }

                                    foreach (var kvp in sortedSnapshot)
                                    {
                                        ImGui.TableNextRow();
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{kvp.Value.Name}@{kvp.Value.World}");
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{kvp.Value.Count}");
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{kvp.Value.LastSeen:yyyy-MM-dd HH:mm:ss}");
                                    }
                                    ImGui.EndTable();
                                }
                                ImGui.Unindent(theme.PadX(0.5f));
                            }
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }
            }
        );
    }

    private void DrawLodestoneCache()
    {
        bool unused = true;
        theme.DrawPluginCardAuto(
            id: "debug-lodestone-cache-card",
            enabled: ref unused,
            showCheckbox: false,
            title: "Lodestone Character ID Cache",
            drawContent: (avail) =>
            {
                var cache = plugin.Config.Lodestone.CharacterIdCache;
                ImGui.TextColored(UiTheme.ColorSuccessText, $"Cached Character IDs: {cache.Count}");

                ImGui.SameLine();
                if (ImGui.Button("Clear Cache"))
                {
                    plugin.Lodestone.ClearCache();
                }
                theme.HoverHandIfItem();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Clear all cached character IDs (both memory and persistent)");

                theme.SpacerY(1f);

                if (cache.Count == 0)
                {
                    ImGui.TextColored(theme.MutedText, "No cached character IDs yet.");
                }
                else
                {
                    float tableHeight = 200f * ImGuiHelpers.GlobalScale;
                    if (ImGui.BeginChild("##lodestoneCache", new Vector2(0, tableHeight), false))
                    {
                        ImGui.Indent(theme.PadX(0.5f));
                        if (ImGui.BeginTable("##lodestoneCacheTable", 2, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.ScrollY))
                        {
                            ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch);
                            ImGui.TableSetupColumn("Lodestone ID", ImGuiTableColumnFlags.WidthFixed, 120f);
                            ImGui.TableHeadersRow();

                            foreach (var kvp in cache.OrderBy(x => x.Key))
                            {
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.Text(kvp.Key);
                                ImGui.TableNextColumn();
                                ImGui.Text(kvp.Value);
                            }
                            ImGui.EndTable();
                        }
                        ImGui.Unindent(theme.PadX(0.5f));
                    }
                    ImGui.EndChild();
                }
            }
        );
    }

    private void DrawStateInspector()
    {
        if (ImGui.CollapsingHeader("Internal State Inspector"))
        {
            ImGui.Indent();

            if (ImGui.TreeNode("Active Channel Mappings"))
            {
                if (plugin.Config.Chat.Mappings.Count == 0)
                {
                    ImGui.TextColored(theme.MutedText, "No mappings configured.");
                }
                else
                {
                    if (ImGui.BeginTable("##dbgMapTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                    {
                        ImGui.TableSetupColumn("Game Chat Type");
                        ImGui.TableSetupColumn("Discord Channel ID");
                        ImGui.TableHeadersRow();

                        foreach (var map in plugin.Config.Chat.Mappings)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.Text(map.GameChatType.ToString());
                            ImGui.TableNextColumn();
                            ImGui.Text(map.DiscordChannelId);
                        }
                        ImGui.EndTable();
                    }
                }
                ImGui.TreePop();
            }


            if (ImGui.TreeNode("Tell Thread Mappings"))
            {
                if (plugin.Config.Chat.TellThreadMappings.Count == 0)
                {
                    ImGui.TextColored(theme.MutedText, "No tell threads active.");
                }
                else
                {
                    if (ImGui.BeginTable("##dbgTellTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                    {
                        ImGui.TableSetupColumn("Correspondent");
                        ImGui.TableSetupColumn("Thread ID");
                        ImGui.TableHeadersRow();

                        foreach (var kvp in plugin.Config.Chat.TellThreadMappings)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.Text(kvp.Key);
                            ImGui.TableNextColumn();
                            ImGui.Text(kvp.Value);
                        }
                        ImGui.EndTable();
                    }
                }
                ImGui.TreePop();
            }


            if (ImGui.TreeNode("Custom Avatars"))
            {
                if (plugin.Config.Chat.CustomAvatars.Count == 0)
                {
                    ImGui.TextColored(theme.MutedText, "No custom avatars defined.");
                }
                else
                {
                    if (ImGui.BeginTable("##dbgAvatarTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                    {
                        ImGui.TableSetupColumn("Key");
                        ImGui.TableSetupColumn("URL");
                        ImGui.TableHeadersRow();

                        foreach (var kvp in plugin.Config.Chat.CustomAvatars)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.Text(kvp.Key);
                            ImGui.TableNextColumn();
                            ImGui.Text(kvp.Value);
                        }
                        ImGui.EndTable();
                    }
                }
                ImGui.TreePop();
            }


            if (ImGui.TreeNode("Discord Guilds"))
            {

                if (DateTime.Now - _lastGuildFetch > _cacheInterval || _cachedGuilds.Count == 0)
                {
                    _lastGuildFetch = DateTime.Now;
                    if (plugin.Discord.Client != null)
                    {
                        _cachedGuilds = plugin.Discord.Client.Guilds.Values.ToList();
                    }
                    else
                    {
                        _cachedGuilds.Clear();
                    }
                }

                if (plugin.Discord.Client != null)
                {
                    foreach (var guild in _cachedGuilds)
                    {
                        ImGui.Text($"{guild.Name} (ID: {guild.Id}) - Members: {guild.MemberCount}");
                    }
                }
                else
                {
                    ImGui.TextColored(theme.MutedText, "Client not connected.");
                }
                ImGui.TreePop();
            }


            if (ImGui.TreeNode("Avatar Cache"))
            {
                var cache = plugin.Lodestone.AvatarCache;
                ImGui.Text($"Cached Entries: {cache.Count}");

                theme.SpacerY(1f);

                ImGui.BeginGroup();
                ImGui.Text("Name");
                ImGui.SetNextItemWidth(120);
                ImGui.InputText("##acName", ref newAvatarName, 32);
                ImGui.EndGroup();

                ImGui.SameLine();

                ImGui.BeginGroup();
                ImGui.Text("World");
                ImGui.SetNextItemWidth(120);
                ImGui.InputText("##acWorld", ref newAvatarWorld, 32);
                ImGui.EndGroup();

                ImGui.SameLine();

                ImGui.BeginGroup();
                ImGui.Text("Avatar URL");
                ImGui.SetNextItemWidth(250);
                ImGui.InputText("##acUrl", ref newAvatarUrl, 256);
                ImGui.EndGroup();

                ImGui.SameLine();


                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ImGui.GetTextLineHeightWithSpacing());
                if (ImGui.Button("Add/Update"))
                {
                    if (!string.IsNullOrWhiteSpace(newAvatarName) && !string.IsNullOrWhiteSpace(newAvatarWorld) && !string.IsNullOrWhiteSpace(newAvatarUrl))
                    {
                        var key = $"{newAvatarName}@{newAvatarWorld}";
                        plugin.Lodestone.UpdateAvatarCache(key, newAvatarUrl);
                        newAvatarName = "";
                        newAvatarWorld = "";
                        newAvatarUrl = "";
                    }
                }

                theme.SpacerY(1f);

                if (ImGui.Button("Clear Entire Cache"))
                {
                    plugin.Lodestone.ClearCache();
                }

                theme.SpacerY(1f);

                if (cache.Count > 0)
                {
                    if (ImGui.BeginTable("##dbgRunAvatarTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                    {
                        ImGui.TableSetupColumn("Key", ImGuiTableColumnFlags.WidthFixed, 150f);
                        ImGui.TableSetupColumn("URL");
                        ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 50f);
                        ImGui.TableHeadersRow();


                        var keys = cache.Keys.ToList();

                        foreach (var key in keys)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.Text(key);
                            ImGui.TableNextColumn();
                            if (cache.TryGetValue(key, out var url))
                            {
                                ImGui.PushStyleColor(ImGuiCol.Text, theme.SliderGrab);
                                ImGui.Text(url);
                                if (ImGui.IsItemClicked())
                                {
                                    ImGui.SetClipboardText(url);
                                    plugin.NotificationManager.Add(
                                        "Cordi",
                                        "Avatar URL copied to clipboard",
                                        CordiNotificationType.Success
                                    );
                                }
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip("Click to copy URL");
                                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                                }
                                ImGui.PopStyleColor();
                            }
                            ImGui.TableNextColumn();
                            if (ImGui.Button($"X##rem{key}"))
                            {
                                plugin.Lodestone.InvalidateAvatarCache(key);
                            }
                        }
                        ImGui.EndTable();
                    }
                }
                else
                {
                    ImGui.TextColored(theme.MutedText, "Cache is empty.");
                }

                ImGui.TreePop();
            }

            ImGui.Unindent();
        }
    }

    private string _debugEmoteName = "Wave";
    private string _debugEmoteCommand = "/wave";

    private void DrawEmoteLogDebug()
    {
        bool unused = true;
        theme.DrawPluginCardAuto(
            id: "emote-log-debug-card",
            enabled: ref unused,
            showCheckbox: false,
            title: "Emote Log Debugging",
            drawContent: (avail) =>
            {
                ImGui.TextColored(theme.MutedText, "Inspect Emote Log State and Simulate Events.");

                theme.SpacerY(0.5f);

                if (ImGui.BeginTabBar("##emoteLogDebugTabs"))
                {

                    if (ImGui.BeginTabItem("Simulation"))
                    {
                        ImGui.Text("Simulate Incoming Emote (as if from another player)");

                        ImGui.BeginGroup();
                        ImGui.Text("Peeper Name");
                        ImGui.SetNextItemWidth(150);
                        ImGui.InputText("##dmgEmPName", ref _debugName, 64);
                        ImGui.EndGroup();

                        ImGui.SameLine();

                        ImGui.BeginGroup();
                        ImGui.Text("Peeper World");
                        ImGui.SetNextItemWidth(150);
                        ImGui.InputText("##dbgEmPWorld", ref _debugWorld, 64);
                        ImGui.EndGroup();

                        theme.SpacerY(0.5f);

                        ImGui.BeginGroup();
                        ImGui.Text("Emote Name");
                        ImGui.SetNextItemWidth(150);
                        ImGui.InputText("##dbgEmName", ref _debugEmoteName, 64);
                        ImGui.EndGroup();

                        ImGui.SameLine();

                        ImGui.BeginGroup();
                        ImGui.Text("Command");
                        ImGui.SetNextItemWidth(150);
                        ImGui.InputText("##dbgEmCmd", ref _debugEmoteCommand, 64);
                        ImGui.EndGroup();

                        theme.SpacerY(0.5f);

                        if (ImGui.Button("Inject Emote Event"))
                        {
                            plugin.EmoteLog.SimulateEmote(_debugName, _debugWorld, _debugEmoteName, _debugEmoteCommand);
                        }

                        ImGui.EndTabItem();
                    }


                    if (ImGui.BeginTabItem("Active Discord Emotes"))
                    {
                        var active = plugin.EmoteLog.ActiveDiscordEmotes;
                        ImGui.Text($"Active Tracking Count: {active.Count}");

                        if (ImGui.BeginTable("##dbgActiveEmotes", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                        {
                            ImGui.TableSetupColumn("Key");
                            ImGui.TableSetupColumn("User");
                            ImGui.TableSetupColumn("Emote");
                            ImGui.TableSetupColumn("Count");
                            ImGui.TableSetupColumn("Msg ID");
                            ImGui.TableHeadersRow();

                            foreach (var kvp in active)
                            {
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.Text(kvp.Key);
                                ImGui.TableNextColumn();
                                ImGui.Text($"{kvp.Value.User}@{kvp.Value.World}");
                                ImGui.TableNextColumn();
                                ImGui.Text(kvp.Value.EmoteName);
                                ImGui.TableNextColumn();
                                ImGui.Text(kvp.Value.Count.ToString());
                                ImGui.TableNextColumn();
                                ImGui.Text(kvp.Value.MessageId.ToString());
                            }
                            ImGui.EndTable();
                        }
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Message ID Cache"))
                    {
                        var cache = plugin.EmoteLog.MessageIdCache;
                        ImGui.Text($"Cache Size: {cache.Count}");

                        if (ImGui.BeginTable("##dbgMsgCache", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                        {
                            ImGui.TableSetupColumn("Message ID");
                            ImGui.TableSetupColumn("User");
                            ImGui.TableSetupColumn("Emote");
                            ImGui.TableSetupColumn("Emoted Back?");
                            ImGui.TableHeadersRow();

                            foreach (var kvp in cache)
                            {
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.Text(kvp.Key.ToString());
                                ImGui.TableNextColumn();
                                ImGui.Text($"{kvp.Value.User}@{kvp.Value.World}");
                                ImGui.TableNextColumn();
                                ImGui.Text(kvp.Value.EmoteName);
                                ImGui.TableNextColumn();
                                ImGui.Text(kvp.Value.EmotedBack ? "YES" : "NO");
                                if (kvp.Value.EmotedBack) ImGui.TextColored(UiTheme.ColorSuccessText, "Done");
                            }
                            ImGui.EndTable();
                        }
                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }
            }
        );
    }
}
