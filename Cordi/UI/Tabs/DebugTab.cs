using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Cordi.Services;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using DSharpPlus;
using DSharpPlus.Entities;
using Dalamud.Bindings.ImGui;
using System.Collections.Generic;

using Cordi.Core;
using Cordi.UI.Themes;
using Cordi.Configuration;

namespace Cordi.UI.Tabs;

public class DebugTab : ConfigTabBase
{

    private string sendTestMessage = "Test message from Cordi plugin.";
    private string sendUser = "Nya@Alpaha";
    private XivChatType selectedChatType = XivChatType.None;


    private string newAvatarName = "";
    private string newAvatarWorld = "";
    private string newAvatarUrl = "";


    private List<DiscordGuild> _cachedGuilds = new();
    private DateTime _lastGuildFetch = DateTime.MinValue;
    private readonly TimeSpan _cacheInterval = TimeSpan.FromSeconds(5);

    private string _slashCmdDebugFilter = string.Empty;
    private bool _slashCmdShowEnabled = true;
    private bool _slashCmdShowDisabled = true;
    private bool _slashCmdShowEmotes = false;
    private bool _slashCmdShowUser = true;

    public override string Label => "Debug";

    public DebugTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
    }

    protected override IReadOnlyList<(string Label, Action Draw)> GetSubTabs()
    {
        return new List<(string Label, Action Draw)>
        {
            ("System",         DrawSystemSubTab),
            ("Simulators",     DrawSimulatorsSubTab),
            ("State",          DrawStateSubTab),
            ("Slash Commands", DrawSlashCommandsDebug),
        };
    }

    private void DrawSystemSubTab()
    {
        DrawStatusCard();
        theme.SpacerY();

        DrawCacheOverview();
        theme.SpacerY();

        DrawQueueOverview();
        theme.SpacerY();

        DrawThroughputStats();
    }

    private void DrawSimulatorsSubTab()
    {
        DrawMessageSimulator();
        theme.SpacerY();

        DrawCordiPeepDebug();
        theme.SpacerY();

        DrawEmoteLogDebug();
        theme.SpacerY();

        DrawPartyDebug();
    }

    private void DrawStateSubTab()
    {
        DrawLodestoneCache();
        theme.SpacerY();

        DrawStateInspector();
    }

    private void DrawCacheOverview()
    {
        bool unused = true;
        theme.DrawPluginCardAuto(
            id: "debug-cache-card",
            enabled: ref unused,
            showCheckbox: false,
            title: "Caches",
            drawContent: (avail) =>
            {
                var caches = plugin.CacheRegistry.All.OrderBy(c => c.Name).ToList();

                long aggregatedHits = caches.Sum(c => c.Hits);
                long aggregatedMisses = caches.Sum(c => c.Misses);
                long aggregatedTotal = aggregatedHits + aggregatedMisses;
                double aggregatedHitRate = aggregatedTotal == 0 ? 0 : aggregatedHits * 100.0 / aggregatedTotal;
                int totalEntries = caches.Sum(c => c.Count);
                int totalCapacity = caches.Sum(c => c.Capacity);

                using (ImRaii.Group())
                {
                    ImGui.TextColored(theme.MutedText, "Registered");
                    ImGui.Text($"{caches.Count} cache(s)");
                }
                ImGui.SameLine(avail / 4f);
                using (ImRaii.Group())
                {
                    ImGui.TextColored(theme.MutedText, "Entries");
                    ImGui.Text($"{totalEntries} / {totalCapacity}");
                }
                ImGui.SameLine(avail / 2f);
                using (ImRaii.Group())
                {
                    ImGui.TextColored(theme.MutedText, "Aggregate hit rate");
                    ImGui.TextColored(HitRateColor(aggregatedHitRate), $"{aggregatedHitRate:F1}% ({aggregatedHits:N0} / {aggregatedTotal:N0})");
                }

                theme.SpacerY(1f);

                if (caches.Count == 0)
                {
                    ImGui.TextColored(theme.MutedText, "No caches registered.");
                    return;
                }

                float scale = ImGuiHelpers.GlobalScale;
                using var table = ImRaii.Table("##cacheTable", 6,
                    ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp);
                if (!table) return;

                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Size", ImGuiTableColumnFlags.WidthFixed, 90f * scale);
                ImGui.TableSetupColumn("Hit %", ImGuiTableColumnFlags.WidthFixed, 70f * scale);
                ImGui.TableSetupColumn("Hits", ImGuiTableColumnFlags.WidthFixed, 80f * scale);
                ImGui.TableSetupColumn("Misses", ImGuiTableColumnFlags.WidthFixed, 80f * scale);
                ImGui.TableSetupColumn("Age", ImGuiTableColumnFlags.WidthFixed, 80f * scale);
                ImGui.TableHeadersRow();

                foreach (var c in caches)
                {
                    long total = c.Hits + c.Misses;
                    double hitRate = total == 0 ? 0 : c.Hits * 100.0 / total;

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(c.Name);
                    ImGui.TableNextColumn(); ImGui.TextUnformatted($"{c.Count}/{c.Capacity}");
                    ImGui.TableNextColumn();
                    if (total == 0) ImGui.TextColored(theme.MutedText, "—");
                    else ImGui.TextColored(HitRateColor(hitRate), $"{hitRate:F1}%");
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(c.Hits.ToString("N0"));
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(c.Misses.ToString("N0"));
                    ImGui.TableNextColumn(); ImGui.TextColored(theme.MutedText, FormatDuration(DateTime.UtcNow - c.CreatedAt));
                }
            }
        );
    }

    private void DrawQueueOverview()
    {
        bool unused = true;
        theme.DrawPluginCardAuto(
            id: "debug-queue-card",
            enabled: ref unused,
            showCheckbox: false,
            title: "Send Queues",
            drawContent: (avail) =>
            {
                var dq = plugin.DiscordSendQueue;
                var cm = plugin._chat;

                ImGui.TextColored(theme.MutedText, "Discord outbound");
                theme.SpacerY(0.3f);
                DrawQueueRow(avail,
                    pending: dq.Pending, capacity: dq.Capacity,
                    sent: dq.Sent, retried: dq.Retried,
                    failed: dq.Failed, dropped: dq.Dropped);

                theme.SpacerY(1f);
                ImGui.Separator();
                theme.SpacerY(0.5f);

                ImGui.TextColored(theme.MutedText, "Game chat messenger");
                theme.SpacerY(0.3f);
                DrawQueueRow(avail,
                    pending: cm?.Pending ?? 0, capacity: cm?.MaxPending ?? 0,
                    sent: cm?.Sent ?? 0, retried: 0,
                    failed: cm?.Failed ?? 0, dropped: cm?.Dropped ?? 0);
            }
        );
    }

    private void DrawQueueRow(float avail, long pending, int capacity, long sent, long retried, long failed, long dropped)
    {
        float col = avail / 6f;

        using (ImRaii.Group())
        {
            ImGui.TextColored(theme.MutedText, "Pending");
            var pendColor = capacity > 0 && pending >= capacity * 0.8
                ? UiTheme.ColorDangerText
                : capacity > 0 && pending >= capacity * 0.5
                    ? new Vector4(1f, 0.85f, 0.3f, 1f)
                    : UiTheme.ColorSuccessText;
            ImGui.TextColored(pendColor, $"{pending} / {capacity}");
        }

        ImGui.SameLine(col);
        using (ImRaii.Group())
        {
            ImGui.TextColored(theme.MutedText, "Sent");
            ImGui.Text(sent.ToString("N0"));
        }

        ImGui.SameLine(col * 2);
        using (ImRaii.Group())
        {
            ImGui.TextColored(theme.MutedText, "Retried");
            if (retried == 0) ImGui.TextColored(theme.MutedText, "0");
            else ImGui.TextColored(new Vector4(1f, 0.85f, 0.3f, 1f), retried.ToString("N0"));
        }

        ImGui.SameLine(col * 3);
        using (ImRaii.Group())
        {
            ImGui.TextColored(theme.MutedText, "Failed");
            if (failed == 0) ImGui.TextColored(theme.MutedText, "0");
            else ImGui.TextColored(UiTheme.ColorDangerText, failed.ToString("N0"));
        }

        ImGui.SameLine(col * 4);
        using (ImRaii.Group())
        {
            ImGui.TextColored(theme.MutedText, "Dropped");
            if (dropped == 0) ImGui.TextColored(theme.MutedText, "0");
            else ImGui.TextColored(UiTheme.ColorDangerText, dropped.ToString("N0"));
        }
    }

    private Vector4 HitRateColor(double rate)
    {
        if (rate >= 80) return UiTheme.ColorSuccessText;
        if (rate >= 50) return new Vector4(1f, 0.85f, 0.3f, 1f);
        return UiTheme.ColorDangerText;
    }

    private static string FormatDuration(TimeSpan t)
    {
        if (t.TotalSeconds < 60) return $"{(int)t.TotalSeconds}s";
        if (t.TotalMinutes < 60) return $"{(int)t.TotalMinutes}m";
        if (t.TotalHours < 24) return $"{(int)t.TotalHours}h {(int)(t.TotalMinutes % 60)}m";
        return $"{(int)t.TotalDays}d {(int)(t.TotalHours % 24)}h";
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


                using (var group1 = ImRaii.Group())
                {
                    ImGui.TextColored(theme.MutedText, "Discord Connection");
                    if (botStarted)
                    {
                        if (isConnected)
                        {
                            ImGui.TextColored(UiTheme.ColorSuccessText, "Connected (Ping: "); ImGui.SameLine(0, 0); ImGui.TextColored(UiTheme.ColorSuccessText, (client?.Ping ?? 0).ToString()); ImGui.SameLine(0, 0); ImGui.TextColored(UiTheme.ColorSuccessText, "ms)");
                            ImGui.SameLine();
                            ImGui.TextColored(theme.MutedText, "| Gateway: v"); ImGui.SameLine(0, 0); ImGui.TextColored(theme.MutedText, (client?.GatewayVersion ?? 0).ToString());
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
                }

                ImGui.SameLine(avail / 2f);

                using (var group2 = ImRaii.Group())
                {
                    ImGui.TextColored(theme.MutedText, "Current User");
                    if (isConnected && client?.CurrentUser != null)
                    {
                        ImGui.TextUnformatted(client.CurrentUser.Username); ImGui.SameLine(0, 0); ImGui.TextUnformatted("#"); ImGui.SameLine(0, 0); ImGui.TextUnformatted(client.CurrentUser.Discriminator ?? "0000");
                        ImGui.TextColored(theme.MutedText, "ID: "); ImGui.SameLine(0, 0); ImGui.TextColored(theme.MutedText, client.CurrentUser.Id.ToString());
                    }
                    else
                    {
                        ImGui.Text("-");
                    }
                }

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
                using (var combo = ImRaii.Combo("##dbgChatType", selectedChatType.ToString()))
                if (combo)
                {
                    foreach (var val in Enum.GetValues<XivChatType>())
                    {
                        if (ImGui.Selectable(val.ToString(), val == selectedChatType))
                        {
                            selectedChatType = val;
                        }
                    }
                }
                ImGui.SameLine();
                ImGui.TextColored(theme.MutedText, "Chat Type");

                theme.SpacerY(0.5f);

                float half = avail / 2f - ImGui.GetStyle().ItemSpacing.X;

                using (var group3 = ImRaii.Group())
                {
                    ImGui.Text("Sender (Name@World)");
                    ImGui.SetNextItemWidth(half);
                    ImGui.InputText("##dbgSendUser", ref sendUser, 64);
                }

                ImGui.SameLine();

                using (var group4 = ImRaii.Group())
                {
                    ImGui.Text("Message Content");
                    ImGui.SetNextItemWidth(half);
                    ImGui.InputText("##dbgSendMsg", ref sendTestMessage, 128);
                }

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

    private string _throughputFilter = string.Empty;
    private int _throughputCategory = 0;
    private static readonly string[] ThroughputCategoryLabels = { "Chat Types", "Tell Targets", "Peepers", "Emotes" };

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
                DrawThroughputKpis(avail, stats);
                theme.SpacerY(1f);

                DrawThroughputCategorySelector(avail);
                theme.SpacerY(0.5f);

                ImGui.SetNextItemWidth(avail);
                ImGui.InputTextWithHint("##throughputFilter", "Filter by name...", ref _throughputFilter, 64);
                theme.SpacerY(0.5f);

                switch (_throughputCategory)
                {
                    case 0: DrawThroughputChatTypes(stats); break;
                    case 1: DrawThroughputTells(stats); break;
                    case 2: DrawThroughputPeepers(stats); break;
                    case 3: DrawThroughputEmotes(stats); break;
                }
            });
    }

    private void DrawThroughputKpis(float avail, Cordi.Configuration.ThroughputStats stats)
    {
        float colW = avail / 3f;

        DrawKpi(colW, "TOTAL MESSAGES", stats.TotalMessages.ToString("N0"));
        ImGui.SameLine(colW);
        DrawKpi(colW, "PEEPS TRACKED", stats.TotalPeepsTracked.ToString("N0"));
        ImGui.SameLine(colW * 2);
        DrawKpi(colW, "EMOTES TRACKED", stats.TotalEmotesTracked.ToString("N0"));
    }

    private void DrawKpi(float width, string label, string value)
    {
        using (ImRaii.Group())
        {
            ImGui.TextColored(theme.MutedText, label);
            theme.ApplyFontScale(1.4f);
            ImGui.TextUnformatted(value);
            theme.ApplyFontScale();
        }
    }

    private void DrawThroughputCategorySelector(float avail)
    {
        float btnW = avail / ThroughputCategoryLabels.Length - theme.Gap(0.5f);
        float btnH = 28f * ImGuiHelpers.GlobalScale * UiTheme.GlobalFontScale;

        using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, theme.Radius()))
        {
            for (int i = 0; i < ThroughputCategoryLabels.Length; i++)
            {
                if (i > 0) ImGui.SameLine();
                bool isActive = _throughputCategory == i;
                using (ImRaii.PushColor(ImGuiCol.Button, isActive ? theme.Accent : theme.FrameBg))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, isActive ? theme.Accent : theme.FrameBgHover))
                using (ImRaii.PushColor(ImGuiCol.ButtonActive, isActive ? theme.Accent : theme.FrameBgActive))
                {
                    if (ImGui.Button(ThroughputCategoryLabels[i], new Vector2(btnW, btnH)))
                        _throughputCategory = i;
                    theme.HoverHandIfItem();
                }
            }
        }
    }

    private static (int colIndex, bool ascending) GetSortSpec(int defaultCol)
    {
        var sortSpecs = ImGui.TableGetSortSpecs();
        if (sortSpecs.SpecsDirty) sortSpecs.SpecsDirty = false;
        if (sortSpecs.SpecsCount > 0)
        {
            var spec = sortSpecs.Specs;
            return (spec.ColumnIndex, spec.SortDirection == ImGuiSortDirection.Ascending);
        }
        return (defaultCol, false);
    }

    private void DrawThroughputChatTypes(Cordi.Configuration.ThroughputStats stats)
    {
        Dictionary<XivChatType, long> snapshot;
        lock (stats) snapshot = new Dictionary<XivChatType, long>(stats.ChatTypeStats);

        var filtered = string.IsNullOrWhiteSpace(_throughputFilter)
            ? snapshot
            : snapshot.Where(kvp => kvp.Key.ToString().Contains(_throughputFilter, StringComparison.OrdinalIgnoreCase))
                      .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        long total = snapshot.Sum(x => x.Value);
        if (filtered.Count == 0)
        {
            ImGui.TextColored(theme.MutedText, snapshot.Count == 0 ? "No data yet." : "No results.");
            return;
        }

        float scale = ImGuiHelpers.GlobalScale;
        float tableHeight = 240f * scale;

        using var child = ImRaii.Child("##thruChatChild", new Vector2(0, tableHeight), false);
        if (!child) return;
        using var table = ImRaii.Table("##thruChatTable", 3,
            ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.RowBg
            | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY);
        if (!table) return;

        ImGui.TableSetupColumn("Chat Type", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending, 100f * scale);
        ImGui.TableSetupColumn("Share", ImGuiTableColumnFlags.WidthFixed, 100f * scale);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        var (sortCol, asc) = GetSortSpec(1);
        IEnumerable<KeyValuePair<XivChatType, long>> sorted = filtered;
        sorted = sortCol == 0
            ? (asc ? sorted.OrderBy(x => x.Key.ToString()) : sorted.OrderByDescending(x => x.Key.ToString()))
            : (asc ? sorted.OrderBy(x => x.Value) : sorted.OrderByDescending(x => x.Value));

        foreach (var kvp in sorted)
        {
            double share = total == 0 ? 0 : kvp.Value * 100.0 / total;
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.TextUnformatted(kvp.Key.ToString());
            ImGui.TableNextColumn(); ImGui.TextUnformatted(kvp.Value.ToString("N0"));
            ImGui.TableNextColumn(); ImGui.TextColored(theme.MutedText, $"{share:F1}%");
        }
    }

    private void DrawThroughputTells(Cordi.Configuration.ThroughputStats stats)
    {
        Dictionary<string, long> snapshot;
        lock (stats) snapshot = new Dictionary<string, long>(stats.TellStats);

        var filtered = string.IsNullOrWhiteSpace(_throughputFilter)
            ? snapshot
            : snapshot.Where(kvp => kvp.Key.Contains(_throughputFilter, StringComparison.OrdinalIgnoreCase))
                      .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        long total = snapshot.Sum(x => x.Value);
        if (filtered.Count == 0)
        {
            ImGui.TextColored(theme.MutedText, snapshot.Count == 0 ? "No data yet." : "No results.");
            return;
        }

        float scale = ImGuiHelpers.GlobalScale;
        float tableHeight = 240f * scale;

        using var child = ImRaii.Child("##thruTellChild", new Vector2(0, tableHeight), false);
        if (!child) return;
        using var table = ImRaii.Table("##thruTellTable", 3,
            ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.RowBg
            | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY);
        if (!table) return;

        ImGui.TableSetupColumn("Target", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending, 100f * scale);
        ImGui.TableSetupColumn("Share", ImGuiTableColumnFlags.WidthFixed, 100f * scale);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        var (sortCol, asc) = GetSortSpec(1);
        IEnumerable<KeyValuePair<string, long>> sorted = filtered;
        sorted = sortCol == 0
            ? (asc ? sorted.OrderBy(x => x.Key) : sorted.OrderByDescending(x => x.Key))
            : (asc ? sorted.OrderBy(x => x.Value) : sorted.OrderByDescending(x => x.Value));

        foreach (var kvp in sorted)
        {
            double share = total == 0 ? 0 : kvp.Value * 100.0 / total;
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.TextUnformatted(kvp.Key);
            ImGui.TableNextColumn(); ImGui.TextUnformatted(kvp.Value.ToString("N0"));
            ImGui.TableNextColumn(); ImGui.TextColored(theme.MutedText, $"{share:F1}%");
        }
    }

    private void DrawThroughputPeepers(Cordi.Configuration.ThroughputStats stats)
        => DrawThroughputPlayerStats("Peeper", stats.PeepStats, "thruPeep");

    private void DrawThroughputEmotes(Cordi.Configuration.ThroughputStats stats)
        => DrawThroughputPlayerStats("Emote", stats.EmoteStats, "thruEmote");

    private void DrawThroughputPlayerStats(string label, Dictionary<string, PeeperStats> source, string idPrefix)
    {
        Dictionary<string, PeeperStats> snapshot;
        lock (source) snapshot = new Dictionary<string, PeeperStats>(source);

        var filtered = string.IsNullOrWhiteSpace(_throughputFilter)
            ? snapshot
            : snapshot.Where(kvp =>
                kvp.Value.Name.Contains(_throughputFilter, StringComparison.OrdinalIgnoreCase)
             || kvp.Value.World.Contains(_throughputFilter, StringComparison.OrdinalIgnoreCase))
              .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (filtered.Count == 0)
        {
            ImGui.TextColored(theme.MutedText, snapshot.Count == 0 ? "No data yet." : "No results.");
            return;
        }

        float scale = ImGuiHelpers.GlobalScale;
        float tableHeight = 280f * scale;

        using var child = ImRaii.Child($"##{idPrefix}Child", new Vector2(0, tableHeight), false);
        if (!child) return;
        using var table = ImRaii.Table($"##{idPrefix}Table", 3,
            ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.RowBg
            | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY);
        if (!table) return;

        ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending, 90f * scale);
        ImGui.TableSetupColumn("Last Seen", ImGuiTableColumnFlags.WidthFixed, 130f * scale);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        var (sortCol, asc) = GetSortSpec(1);
        IEnumerable<KeyValuePair<string, PeeperStats>> sorted = filtered;
        sorted = sortCol switch
        {
            0 => asc ? sorted.OrderBy(x => x.Value.Name).ThenBy(x => x.Value.World)
                     : sorted.OrderByDescending(x => x.Value.Name).ThenByDescending(x => x.Value.World),
            1 => asc ? sorted.OrderBy(x => x.Value.Count)
                     : sorted.OrderByDescending(x => x.Value.Count),
            _ => asc ? sorted.OrderBy(x => x.Value.LastSeen)
                     : sorted.OrderByDescending(x => x.Value.LastSeen),
        };

        foreach (var kvp in sorted)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(kvp.Value.Name);
            ImGui.SameLine(0, 0); ImGui.TextColored(theme.MutedText, "@");
            ImGui.SameLine(0, 0); ImGui.TextColored(theme.MutedText, kvp.Value.World);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(kvp.Value.Count.ToString("N0"));

            ImGui.TableNextColumn();
            ImGui.TextColored(theme.MutedText, FormatRelativeTime(kvp.Value.LastSeen));
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(kvp.Value.LastSeen.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }

    private static string FormatRelativeTime(DateTime when)
    {
        var span = DateTime.Now - when;
        if (span.TotalSeconds < 30) return "just now";
        if (span.TotalMinutes < 1) return $"{(int)span.TotalSeconds}s ago";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays}d ago";
        if (span.TotalDays < 365) return $"{(int)(span.TotalDays / 30)}mo ago";
        return $"{(int)(span.TotalDays / 365)}y ago";
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
                    using (var child = ImRaii.Child("##lodestoneCache", new Vector2(0, tableHeight), false))
                    if (child)
                    {
                        using (ImRaii.PushIndent(theme.PadX(0.5f)))
                        {
                            using (var table = ImRaii.Table("##lodestoneCacheTable", 2, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.ScrollY))
                            if (table)
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
                            }
                        }
                    }
                }
            }
        );
    }

    private void DrawStateInspector()
    {
        if (ImGui.CollapsingHeader("Internal State Inspector"))
        {
            using (ImRaii.PushIndent())
            {
                using (var tree1 = ImRaii.TreeNode("Active Channel Mappings"))
                {
                    if (tree1)
                    {
                        if (plugin.Config.Chat.Mappings.Count == 0)
                        {
                            ImGui.TextColored(theme.MutedText, "No mappings configured.");
                        }
                        else
                        {
                            using (var table = ImRaii.Table("##dbgMapTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                            if (table)
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
                            }
                        }
                    }
                }


                using (var tree2 = ImRaii.TreeNode("Tell Thread Mappings"))
                {
                    if (tree2)
                    {
                        if (plugin.Config.Chat.TellThreadMappings.Count == 0)
                        {
                            ImGui.TextColored(theme.MutedText, "No tell threads active.");
                        }
                        else
                        {
                            using (var table = ImRaii.Table("##dbgTellTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                            if (table)
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
                            }
                        }
                    }
                }


                using (var tree3 = ImRaii.TreeNode("Custom Avatars"))
                {
                    if (tree3)
                    {
                        if (plugin.Config.Chat.CustomAvatars.Count == 0)
                        {
                            ImGui.TextColored(theme.MutedText, "No custom avatars defined.");
                        }
                        else
                        {
                            using (var table = ImRaii.Table("##dbgAvatarTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                            if (table)
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
                            }
                        }
                    }
                }


                using (var tree4 = ImRaii.TreeNode("Discord Guilds"))
                {
                    if (tree4)
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
                                ImGui.TextUnformatted(guild.Name); ImGui.SameLine(0, 0); ImGui.TextUnformatted(" (ID: "); ImGui.SameLine(0, 0); ImGui.TextUnformatted(guild.Id.ToString()); ImGui.SameLine(0, 0); ImGui.TextUnformatted(") - Members: "); ImGui.SameLine(0, 0); ImGui.TextUnformatted(guild.MemberCount.ToString());
                            }
                        }
                        else
                        {
                            ImGui.TextColored(theme.MutedText, "Client not connected.");
                        }
                    }
                }


                using (var tree5 = ImRaii.TreeNode("Avatar Cache"))
                {
                    if (tree5)
                    {
                        var keys = plugin.Lodestone.AvatarCacheKeys;
                        ImGui.TextUnformatted("Cached Entries: "); ImGui.SameLine(0, 0); ImGui.TextUnformatted(keys.Count.ToString());

                        theme.SpacerY(1f);

                        using (ImRaii.Group())
                        {
                            ImGui.Text("Name");
                            ImGui.SetNextItemWidth(120);
                            ImGui.InputText("##acName", ref newAvatarName, 32);
                        }

                        ImGui.SameLine();

                        using (ImRaii.Group())
                        {
                            ImGui.Text("World");
                            ImGui.SetNextItemWidth(120);
                            ImGui.InputText("##acWorld", ref newAvatarWorld, 32);
                        }

                        ImGui.SameLine();

                        using (ImRaii.Group())
                        {
                            ImGui.Text("Avatar URL");
                            ImGui.SetNextItemWidth(250);
                            ImGui.InputText("##acUrl", ref newAvatarUrl, 256);
                        }

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

                        if (keys.Count > 0)
                        {
                            using (var table = ImRaii.Table("##dbgRunAvatarTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                            if (table)
                            {
                                ImGui.TableSetupColumn("Key", ImGuiTableColumnFlags.WidthFixed, 150f);
                                ImGui.TableSetupColumn("URL");
                                ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 50f);
                                ImGui.TableHeadersRow();


                                foreach (var key in keys.ToList())
                                {
                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();
                                    ImGui.Text(key);
                                    ImGui.TableNextColumn();
                                    if (plugin.Lodestone.TryGetAvatar(key, out var url))
                                    {
                                        using (ImRaii.PushColor(ImGuiCol.Text, theme.SliderGrab))
                                        {
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
                                        }
                                    }
                                    ImGui.TableNextColumn();
                                    if (ImGui.Button($"X##rem{key}"))
                                    {
                                        plugin.Lodestone.InvalidateAvatarCache(key);
                                    }
                                }
                            }
                        }
                        else
                        {
                            ImGui.TextColored(theme.MutedText, "Cache is empty.");
                        }
                    }
                }
            }
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

                using (var tabBar = ImRaii.TabBar("##emoteLogDebugTabs"))
                if (tabBar)
                {

                    using (var tabItem1 = ImRaii.TabItem("Simulation"))
                    if (tabItem1)
                    {
                        ImGui.Text("Simulate Incoming Emote (as if from another player)");

                        using (var group1 = ImRaii.Group())
                        {
                            ImGui.Text("Peeper Name");
                            ImGui.SetNextItemWidth(150);
                            ImGui.InputText("##dmgEmPName", ref _debugName, 64);
                        }

                        ImGui.SameLine();

                        using (var group2 = ImRaii.Group())
                        {
                            ImGui.Text("Peeper World");
                            ImGui.SetNextItemWidth(150);
                            ImGui.InputText("##dbgEmPWorld", ref _debugWorld, 64);
                        }

                        theme.SpacerY(0.5f);

                        using (var group3 = ImRaii.Group())
                        {
                            ImGui.Text("Emote Name");
                            ImGui.SetNextItemWidth(150);
                            ImGui.InputText("##dbgEmName", ref _debugEmoteName, 64);
                        }

                        ImGui.SameLine();

                        using (var group4_inner = ImRaii.Group()) // Renamed to avoid conflict with outer group4
                        {
                            ImGui.Text("Command");
                            ImGui.SetNextItemWidth(150);
                            ImGui.InputText("##dbgEmCmd", ref _debugEmoteCommand, 64);
                        }

                        theme.SpacerY(0.5f);

                        if (ImGui.Button("Inject Emote Event"))
                        {
                            plugin.EmoteLog.SimulateEmote(_debugName, _debugWorld, _debugEmoteName, _debugEmoteCommand);
                        }
                    }


                    using (var tabItem2 = ImRaii.TabItem("Active Discord Emotes"))
                    if (tabItem2)
                    {
                        var active = plugin.EmoteLog.ActiveDiscordEmotes;
                        ImGui.TextUnformatted("Active Tracking Count: "); ImGui.SameLine(0, 0); ImGui.TextUnformatted(active.Count.ToString());

                        using (var table1 = ImRaii.Table("##dbgActiveEmotes", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                        if (table1)
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
                                ImGui.TextUnformatted(kvp.Value.User); ImGui.SameLine(0, 0); ImGui.TextUnformatted("@"); ImGui.SameLine(0, 0); ImGui.TextUnformatted(kvp.Value.World);
                                ImGui.TableNextColumn();
                                ImGui.Text(kvp.Value.EmoteName);
                                ImGui.TableNextColumn();
                                ImGui.Text(kvp.Value.Count.ToString());
                                ImGui.TableNextColumn();
                                ImGui.Text(kvp.Value.MessageId.ToString());
                            }
                        }
                    }

                    using (var tabItem3 = ImRaii.TabItem("Message ID Cache"))
                    if (tabItem3)
                    {
                        var cache = plugin.EmoteLog.MessageIdCache;
                        ImGui.TextUnformatted("Cache Size: "); ImGui.SameLine(0, 0); ImGui.TextUnformatted(cache.Count.ToString());

                        using (var table2 = ImRaii.Table("##dbgMsgCache", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                        if (table2)
                        {
                            ImGui.TableSetupColumn("Message ID");
                            ImGui.TableSetupColumn("User");
                            ImGui.TableSetupColumn("Emote");
                            ImGui.TableSetupColumn("Emoted Back?");
                            ImGui.TableHeadersRow();

                            foreach (var key in cache.Keys.ToList())
                            {
                                if (!cache.TryGet(key, out var value)) continue;
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.Text(key.ToString());
                                ImGui.TableNextColumn();
                                ImGui.TextUnformatted(value.User); ImGui.SameLine(0, 0); ImGui.TextUnformatted("@"); ImGui.SameLine(0, 0); ImGui.TextUnformatted(value.World);
                                ImGui.TableNextColumn();
                                ImGui.Text(value.EmoteName);
                                ImGui.TableNextColumn();
                                ImGui.Text(value.EmotedBack ? "YES" : "NO");
                                if (value.EmotedBack) ImGui.TextColored(UiTheme.ColorSuccessText, "Done");
                            }
                        }
                    }
                }
            }
        );
    }

    private void DrawSlashCommandsDebug()
    {
        var config = plugin.Config.SlashCommands;
        var userCommands = config.Commands;
        var emoteCommands = plugin.SlashCommandService?.EmoteCommands ?? new();
        int userCount = userCommands.Count;
        int emoteCount = emoteCommands.Count;
        int totalCommands = userCount + emoteCount;
        int enabledCount = userCommands.Count(c => c.IsEnabled);
        int groupCount = config.Groups.Count;

        bool unused = true;
        theme.DrawPluginCardAuto(
            id: "slash-commands-debug",
            enabled: ref unused,
            showCheckbox: false,
            title: "Slash Commands",
            drawContent: (avail) =>
            {
                // Overview stats
                ImGui.TextColored(theme.MutedText, "Overview");
                theme.SpacerY(0.3f);

                using (var statsTable = ImRaii.Table("##slashStats", 2, ImGuiTableFlags.SizingStretchProp))
                {
                    if (statsTable)
                    {
                        ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 160f * ImGuiHelpers.GlobalScale);
                        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

                        void StatRow(string label, string value, Vector4? color = null)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.TextColored(theme.MutedText, label);
                            ImGui.TableNextColumn();
                            if (color.HasValue)
                                ImGui.TextColored(color.Value, value);
                            else
                                ImGui.TextUnformatted(value);
                        }

                        StatRow("Feature Enabled", config.Enabled ? "Yes" : "No",
                            config.Enabled ? UiTheme.ColorSuccessText : UiTheme.ColorDangerText);
                        StatRow("Guild ID", string.IsNullOrEmpty(config.GuildId) ? "(not set)" : config.GuildId);
                        StatRow("Channel Restriction", string.IsNullOrEmpty(config.CommandChannelId) ? "None" : config.CommandChannelId);
                        StatRow("Total Commands", totalCommands.ToString());
                        StatRow("User Commands", userCount.ToString());
                        StatRow("Emote Commands", $"{emoteCount} (always available via /emote)");
                        StatRow("Enabled / Limit", $"{enabledCount} / 98",
                            enabledCount >= 98 ? UiTheme.ColorDangerText : (Vector4?)null);
                        StatRow("Groups", groupCount.ToString());
                        StatRow("Bot Connected", plugin.SlashCommandService != null ? "Yes" : "No",
                            plugin.SlashCommandService != null ? UiTheme.ColorSuccessText : UiTheme.ColorDangerText);
                    }
                }

                theme.SpacerY(1f);
                ImGui.Separator();
                theme.SpacerY(1f);

                // Groups overview
                if (config.Groups.Count > 0)
                {
                    ImGui.TextColored(theme.MutedText, "Groups");
                    theme.SpacerY(0.3f);

                    using (var groupTable = ImRaii.Table("##slashGroups", 3, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp))
                    {
                        if (groupTable)
                        {
                            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                            ImGui.TableSetupColumn("Commands", ImGuiTableColumnFlags.WidthFixed, 80f * ImGuiHelpers.GlobalScale);
                            ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 80f * ImGuiHelpers.GlobalScale);
                            ImGui.TableHeadersRow();

                            foreach (var group in config.Groups.OrderBy(g => g.Name))
                            {
                                var cmds = userCommands.Where(c =>
                                    string.Equals(c.Group, group.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                                int en = cmds.Count(c => c.IsEnabled);

                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.TextUnformatted(group.Name);
                                ImGui.TableNextColumn();
                                ImGui.TextUnformatted(cmds.Count.ToString());
                                ImGui.TableNextColumn();
                                ImGui.TextColored(en > 0 ? UiTheme.ColorSuccessText : theme.MutedText, en.ToString());
                            }
                        }
                    }

                    theme.SpacerY(1f);
                    ImGui.Separator();
                    theme.SpacerY(1f);
                }

                // Command list with filters
                ImGui.TextColored(theme.MutedText, "Command List");
                theme.SpacerY(0.3f);

                ImGui.SetNextItemWidth(avail);
                ImGui.InputTextWithHint("##slashDebugSearch", "Filter by name, command, or group...", ref _slashCmdDebugFilter, 64);
                theme.SpacerY(0.3f);

                // Filter toggles
                ImGui.Checkbox("Enabled", ref _slashCmdShowEnabled);
                ImGui.SameLine();
                ImGui.Checkbox("Disabled", ref _slashCmdShowDisabled);
                ImGui.SameLine();
                ImGui.Checkbox("User", ref _slashCmdShowUser);
                ImGui.SameLine();
                ImGui.Checkbox("Emotes", ref _slashCmdShowEmotes);
                theme.SpacerY(0.5f);

                // Build combined list from user commands + in-memory emotes
                var combined = new List<CustomSlashCommand>();
                if (_slashCmdShowUser)
                {
                    IEnumerable<CustomSlashCommand> userFiltered = userCommands;
                    if (!_slashCmdShowEnabled)
                        userFiltered = userFiltered.Where(c => !c.IsEnabled);
                    if (!_slashCmdShowDisabled)
                        userFiltered = userFiltered.Where(c => c.IsEnabled);
                    combined.AddRange(userFiltered);
                }
                if (_slashCmdShowEmotes)
                    combined.AddRange(emoteCommands);

                if (!string.IsNullOrWhiteSpace(_slashCmdDebugFilter))
                {
                    var f = _slashCmdDebugFilter;
                    combined = combined.Where(c =>
                        c.Name.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                        c.GameCommand.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                        (c.Group ?? "").Contains(f, StringComparison.OrdinalIgnoreCase) ||
                        (c.Description ?? "").Contains(f, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                var list = combined
                    .OrderBy(c => c.IsEmote)
                    .ThenByDescending(c => c.IsEnabled)
                    .ThenBy(c => c.Name)
                    .ToList();

                ImGui.TextColored(theme.MutedText, $"Showing {list.Count} of {totalCommands}");
                theme.SpacerY(0.3f);

                using (var cmdTable = ImRaii.Table("##slashCmdList", 6,
                    ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.ScrollY,
                    new Vector2(0, 300f * ImGuiHelpers.GlobalScale)))
                {
                    if (cmdTable)
                    {
                        ImGui.TableSetupColumn("##status", ImGuiTableColumnFlags.WidthFixed, 14f * ImGuiHelpers.GlobalScale);
                        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 120f * ImGuiHelpers.GlobalScale);
                        ImGui.TableSetupColumn("Game Cmd", ImGuiTableColumnFlags.WidthFixed, 120f * ImGuiHelpers.GlobalScale);
                        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 50f * ImGuiHelpers.GlobalScale);
                        ImGui.TableSetupColumn("Group", ImGuiTableColumnFlags.WidthFixed, 80f * ImGuiHelpers.GlobalScale);
                        ImGui.TableSetupColumn("Params", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableHeadersRow();

                        foreach (var cmd in list)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();

                            // Status dot
                            var dotColor = cmd.IsEmote
                                ? new Vector4(0.3f, 0.7f, 1f, 1f) // Blue for always-available emotes
                                : cmd.IsEnabled ? UiTheme.ColorSuccessText : new Vector4(0.5f, 0.5f, 0.5f, 1f);
                            ImGui.TextColored(dotColor, (cmd.IsEmote || cmd.IsEnabled) ? "*" : " ");

                            ImGui.TableNextColumn();
                            if (!cmd.IsEnabled) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
                            ImGui.TextUnformatted($"/{cmd.Name}");

                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted(cmd.GameCommand);

                            ImGui.TableNextColumn();
                            ImGui.TextColored(theme.MutedText, cmd.IsEmote ? "Emote" : "User");
                            if (cmd.IsEmote && ImGui.IsItemHovered())
                                ImGui.SetTooltip("Embedded — always available via /emote");

                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted(cmd.Group ?? "");

                            ImGui.TableNextColumn();
                            if (cmd.Parameters is { Count: > 0 })
                            {
                                var paramStr = string.Join(", ", cmd.Parameters.Select(p =>
                                    p.Required ? $"{p.Name}*" : p.Name));
                                ImGui.TextColored(theme.MutedText, paramStr);
                            }
                            if (!cmd.IsEnabled) ImGui.PopStyleVar();
                        }
                    }
                }
            }
        );
    }
}
