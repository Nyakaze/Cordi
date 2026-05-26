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
    private IReadOnlyList<TrackedPlayer> cachedList = Array.Empty<TrackedPlayer>();
    private DateTime lastListRefresh = DateTime.MinValue;
    private static readonly TimeSpan ListRefreshInterval = TimeSpan.FromSeconds(2);

    private int countTotal;
    private int countConfirmed;
    private int countProvisional;
    private int countRecent;

    // Background refresh state. Only the UI thread reads/writes these fields,
    // except for `_pendingResult` which is published via Volatile.Write from the
    // worker and consumed (then cleared) on the next UI frame.
    private int _refreshInFlight;
    private RefreshResult? _pendingResult;

    private sealed record RefreshResult(
        string Query,
        IReadOnlyList<TrackedPlayer> List,
        int Total,
        int Confirmed,
        int Provisional,
        int Recent);

    public override string Label => "Player Tracker";

    public PlayerTrackerTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme) { }

    public override void Draw()
    {
        bool enabled = true;
        theme.DrawPluginCardAuto(
            id: "player-tracker-list",
            title: "Tracked Players",
            enabled: ref enabled,
            showCheckbox: false,
            drawContent: (avail) =>
            {
                EnsureListFresh();

                DrawKpiTiles(avail);
                theme.SpacerY();

                DrawSearchAndFilters(avail);
                theme.SpacerY(0.5f);

                DrawPlayerTable();
            }
        );
    }

    private void EnsureListFresh()
    {
        // 1) Consume any result the background task published since the last frame.
        var ready = Interlocked.Exchange(ref _pendingResult, null);
        if (ready != null)
        {
            cachedList = statusFilter switch
            {
                StatusFilter.Confirmed => ready.List.Where(x => !x.IsProvisional).ToList(),
                StatusFilter.Provisional => ready.List.Where(x => x.IsProvisional).ToList(),
                _ => ready.List,
            };
            countTotal = ready.Total;
            countConfirmed = ready.Confirmed;
            countProvisional = ready.Provisional;
            countRecent = ready.Recent;
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

                var result = new RefreshResult(
                    Query: query,
                    List: baseList,
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

        DrawKpi("TOTAL TRACKED", countTotal.ToString("N0"));
        ImGui.SameLine(colW);
        DrawKpi("CONFIRMED", countConfirmed.ToString("N0"), UiTheme.ColorSuccessText);
        ImGui.SameLine(colW * 2);
        DrawKpi("PROVISIONAL", countProvisional.ToString("N0"), new Vector4(0.65f, 0.65f, 0.65f, 1f));
        ImGui.SameLine(colW * 3);
        DrawKpi("LAST 7 DAYS", countRecent.ToString("N0"));
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

    private void DrawStatusFilterPills(float totalWidth)
    {
        var filters = new[] { StatusFilter.All, StatusFilter.Confirmed, StatusFilter.Provisional };
        var labels = new[] { "All", "Confirmed", "Provisional" };
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

        foreach (var p in cachedList)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            var dotColor = p.IsProvisional
                ? new Vector4(0.55f, 0.55f, 0.55f, 1f)
                : UiTheme.ColorSuccessText;
            ImGui.TextColored(dotColor, "●");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(p.IsProvisional ? "Provisional (no ContentId / Lodestone ID resolved)" : "Confirmed");

            ImGui.TableNextColumn();
            if (ImGui.Selectable($"##row-{p.LocalId}", false, ImGuiSelectableFlags.SpanAllColumns))
            {
                plugin.PlayerDetailWindow.Show(p.LocalId);
            }
            theme.HoverHandIfItem();
            ImGui.SameLine(0, 0);
            ImGui.TextUnformatted(p.Info.Name);
            ImGui.SameLine(0, 0);
            ImGui.TextColored(theme.MutedText, " @ ");
            ImGui.SameLine(0, 0);
            ImGui.TextColored(theme.MutedText, p.Info.World);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(p.Stats.SeenCount.ToString("N0"));

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(FormatRelative(p.Stats.LastSeen));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(p.Stats.LastSeen.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));

            ImGui.TableNextColumn();
            ImGui.TextColored(theme.MutedText, p.Stats.FirstSeenVia.ToString());
        }

        ImGui.EndTable();
    }

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
