using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Domain.Tracking;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Tabs;

public class PlayerTrackerTab : ConfigTabBase
{
    private enum StatusFilter { All, Confirmed, Provisional }

    private string searchText = string.Empty;
    private StatusFilter statusFilter = StatusFilter.All;
    private IReadOnlyList<RowVM> cachedList = Array.Empty<RowVM>();
    private DateTime lastListRefresh = DateTime.MinValue;
    private static readonly TimeSpan ListRefreshInterval = TimeSpan.FromSeconds(2);

    // KPI counts, formatted once when a refresh result is consumed (not every frame).
    private string countTotal = "0";
    private string countConfirmed = "0";
    private string countProvisional = "0";
    private string countRecent = "0";

    private static readonly Vector4 ProvisionalDot = new(0.55f, 0.55f, 0.55f, 1f);
    private static readonly Vector4 KpiProvisionalColor = new(0.65f, 0.65f, 0.65f, 1f);

    // Background refresh state. Only the UI thread reads/writes these fields,
    // except for `_pendingResult` which is published via Volatile.Write from the
    // worker and consumed (then cleared) on the next UI frame.
    private int _refreshInFlight;
    private RefreshResult? _pendingResult;

    // Fully pre-formatted row so per-frame drawing does zero string/format work.
    private sealed record RowVM(
        Guid LocalId,
        bool Provisional,
        string Name,
        string World,
        string Seen,
        string LastSeenRelative,
        string LastSeenAbsolute,
        string Source);

    private sealed record RefreshResult(
        string Query,
        IReadOnlyList<RowVM> List,
        int Total,
        int Confirmed,
        int Provisional,
        int Recent);

    public override string Label => "Player Tracker";

    public PlayerTrackerTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme) { }

    public override void Draw()
    {
        bool enabled = plugin.Config.PlayerTracker.Enabled;
        theme.DrawPluginCardAuto(
            id: "player-tracker-list",
            title: "Tracked Players",
            enabled: ref enabled,
            showCheckbox: true,
            mutedText: "Opt-in. When off, no players are tracked, scanned, or recorded.",
            drawContent: (avail) =>
            {
                // Previously tracked players stay viewable even when tracking is off;
                // only new scanning/recording is paused (gated in the tracking service).
                if (!plugin.Config.PlayerTracker.Enabled)
                    DrawPausedNotice();

                EnsureListFresh();

                DrawKpiTiles(avail);
                theme.SpacerY();

                DrawSearchAndFilters(avail);
                theme.SpacerY(0.5f);

                DrawPlayerTable();
            }
        );

        if (enabled != plugin.Config.PlayerTracker.Enabled)
        {
            plugin.Config.PlayerTracker.Enabled = enabled;
            plugin.Config.Save();
            // Force the list to refetch next time tracking is turned back on.
            lastListRefresh = DateTime.MinValue;
        }
    }

    private void DrawPausedNotice()
    {
        ImGui.TextColored(theme.MutedText,
            "Tracking is paused — no new players are being scanned or recorded. " +
            "Previously tracked players remain viewable below.");
        theme.SpacerY(0.5f);
    }

    private void EnsureListFresh()
    {
        // 1) Consume any result the background task published since the last frame.
        var ready = Interlocked.Exchange(ref _pendingResult, null);
        if (ready != null)
        {
            cachedList = statusFilter switch
            {
                StatusFilter.Confirmed => ready.List.Where(x => !x.Provisional).ToList(),
                StatusFilter.Provisional => ready.List.Where(x => x.Provisional).ToList(),
                _ => ready.List,
            };
            countTotal = ready.Total.ToString("N0");
            countConfirmed = ready.Confirmed.ToString("N0");
            countProvisional = ready.Provisional.ToString("N0");
            countRecent = ready.Recent.ToString("N0");
            Interlocked.Exchange(ref _refreshInFlight, 0);

            // If the user changed the query while the worker was running, force a
            // re-fetch immediately; otherwise honor the throttle.
            lastListRefresh = ready.Query == (searchText ?? string.Empty)
                ? DateTime.UtcNow
                : DateTime.MinValue;
        }

        if ((DateTime.UtcNow - lastListRefresh) <= ListRefreshInterval) return;

        // 2) Kick off a single background refresh. Subsequent frames keep
        //    rendering the existing cached list until the worker publishes.
        if (Interlocked.CompareExchange(ref _refreshInFlight, 1, 0) != 0) return;

        var query = searchText ?? string.Empty;
        var since = DateTime.UtcNow.AddDays(-7);

        Task.Run(() =>
        {
            try
            {
                var tracker = plugin.PlayerTracker;
                var baseList = string.IsNullOrWhiteSpace(query)
                    ? tracker.GetRecent(500)
                    : tracker.Search(query, 500);

                var vms = new List<RowVM>(baseList.Count);
                foreach (var p in baseList) vms.Add(BuildRowVM(p));

                var result = new RefreshResult(
                    Query: query,
                    List: vms,
                    Total: tracker.Count(),
                    Confirmed: tracker.CountConfirmed(),
                    Provisional: tracker.CountProvisional(),
                    Recent: tracker.CountSeenSince(since));

                Interlocked.Exchange(ref _pendingResult, result);
            }
            catch
            {
                Interlocked.Exchange(ref _refreshInFlight, 0);
            }
        });
    }

    private void DrawKpiTiles(float avail)
    {
        float colW = avail / 4f;

        DrawKpi("TOTAL TRACKED", countTotal);
        ImGui.SameLine(colW);
        DrawKpi("CONFIRMED", countConfirmed, UiTheme.ColorSuccessText);
        ImGui.SameLine(colW * 2);
        DrawKpi("PROVISIONAL", countProvisional, KpiProvisionalColor);
        ImGui.SameLine(colW * 3);
        DrawKpi("LAST 7 DAYS", countRecent);
    }

    private void DrawKpi(string label, string value, Vector4? valueColor = null)
    {
        using (ImRaii.Group())
        {
            ImGui.TextColored(theme.MutedText, label);
            theme.ApplyFontScale(1.3f);
            if (valueColor.HasValue) ImGui.TextColored(valueColor.Value, value);
            else ImGui.TextUnformatted(value);
            theme.ApplyFontScale();
        }
    }

    private void DrawSearchAndFilters(float avail)
    {
        float pillsW = 280f * ImGuiHelpers.GlobalScale;
        float searchW = avail - pillsW - theme.Gap(0.5f);

        ImGui.SetNextItemWidth(searchW);
        if (ImGui.InputTextWithHint("##playerTrackerSearch", "Search by name, world, or notes...", ref searchText, 64))
        {
            lastListRefresh = DateTime.MinValue;
        }

        ImGui.SameLine();
        DrawStatusFilterPills(pillsW);
    }

    private static readonly StatusFilter[] FilterValues = { StatusFilter.All, StatusFilter.Confirmed, StatusFilter.Provisional };
    private static readonly string[] FilterLabels = { "All", "Confirmed", "Provisional" };

    private void DrawStatusFilterPills(float totalWidth)
    {
        var filters = FilterValues;
        var labels = FilterLabels;
        float btnW = totalWidth / filters.Length - theme.Gap(0.3f);
        float btnH = ImGui.GetFrameHeight();

        using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, theme.Radius()))
        {
            for (int i = 0; i < filters.Length; i++)
            {
                if (i > 0) ImGui.SameLine();
                bool isActive = statusFilter == filters[i];
                using (ImRaii.PushColor(ImGuiCol.Button, isActive ? theme.Accent : theme.FrameBg))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, isActive ? theme.Accent : theme.FrameBgHover))
                using (ImRaii.PushColor(ImGuiCol.ButtonActive, isActive ? theme.Accent : theme.FrameBgActive))
                {
                    if (ImGui.Button(labels[i], new Vector2(btnW, btnH)))
                    {
                        statusFilter = filters[i];
                        lastListRefresh = DateTime.MinValue;
                    }
                    theme.HoverHandIfItem();
                }
            }
        }
    }

    private void DrawPlayerTable()
    {
        float remainingHeight = ImGui.GetContentRegionAvail().Y;
        if (remainingHeight < 80f) remainingHeight = 80f;

        if (cachedList.Count == 0)
        {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + remainingHeight / 3f);
            using (ImRaii.PushIndent(ImGui.GetContentRegionAvail().X / 3f))
            {
                ImGui.TextColored(theme.MutedText, "No players match.");
            }
            return;
        }

        if (!ImGui.BeginTable("##playerList", 5,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.NoBordersInBody
            | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp,
            new Vector2(0, remainingHeight)))
            return;

        float scale = ImGuiHelpers.GlobalScale;
        ImGui.TableSetupColumn("##status", ImGuiTableColumnFlags.WidthFixed, 16f * scale);
        ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Seen", ImGuiTableColumnFlags.WidthFixed, 60f * scale);
        ImGui.TableSetupColumn("Last Seen", ImGuiTableColumnFlags.WidthFixed, 120f * scale);
        ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed, 90f * scale);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        // Only the visible rows are processed each frame; the rest are skipped entirely.
        var clipper = new ImGuiListClipper();
        clipper.Begin(cachedList.Count, ImGui.GetTextLineHeightWithSpacing());
        while (clipper.Step())
        {
            for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                DrawPlayerRow(cachedList[i]);
        }
        clipper.End();

        ImGui.EndTable();
    }

    private void DrawPlayerRow(RowVM p)
    {
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.TextColored(p.Provisional ? ProvisionalDot : UiTheme.ColorSuccessText, "●");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(p.Provisional ? "Provisional (no ContentId / Lodestone ID resolved)" : "Confirmed");

        ImGui.TableNextColumn();
        if (ImGui.Selectable($"##row-{p.LocalId}", false, ImGuiSelectableFlags.SpanAllColumns))
            plugin.PlayerDetailWindow.Show(p.LocalId);
        theme.HoverHandIfItem();
        ImGui.SameLine(0, 0);
        ImGui.TextUnformatted(p.Name);
        ImGui.SameLine(0, 0);
        ImGui.TextColored(theme.MutedText, " @ ");
        ImGui.SameLine(0, 0);
        ImGui.TextColored(theme.MutedText, p.World);

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(p.Seen);

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(p.LastSeenRelative);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(p.LastSeenAbsolute);

        ImGui.TableNextColumn();
        ImGui.TextColored(theme.MutedText, p.Source);
    }

    private static RowVM BuildRowVM(TrackedPlayer p) => new(
        LocalId: p.LocalId,
        Provisional: p.IsProvisional,
        Name: p.Info.Name,
        World: p.Info.World,
        Seen: p.Stats.SeenCount.ToString("N0"),
        LastSeenRelative: FormatRelative(p.Stats.LastSeen),
        LastSeenAbsolute: p.Stats.LastSeen == default
            ? "—"
            : p.Stats.LastSeen.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
        Source: p.Stats.FirstSeenVia.ToString());

    private static string FormatRelative(DateTime when)
    {
        if (when == default) return "—";
        var span = DateTime.UtcNow - when.ToUniversalTime();
        if (span.TotalSeconds < 30) return "just now";
        if (span.TotalMinutes < 1) return $"{(int)span.TotalSeconds}s ago";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays}d ago";
        if (span.TotalDays < 365) return $"{(int)(span.TotalDays / 30)}mo ago";
        return $"{(int)(span.TotalDays / 365)}y ago";
    }
}
