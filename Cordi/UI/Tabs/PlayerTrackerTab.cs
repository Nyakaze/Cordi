using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Core;
using Cordi.Domain.Tracking;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;

namespace Cordi.UI.Tabs;

public class PlayerTrackerTab : ConfigTabBase
{
    private string searchText = string.Empty;
    private Guid? selectedPlayerId;
    private IReadOnlyList<TrackedPlayer> cachedList = Array.Empty<TrackedPlayer>();
    private DateTime lastListRefresh = DateTime.MinValue;
    private static readonly TimeSpan ListRefreshInterval = TimeSpan.FromSeconds(2);

    public override string Label => "Player Tracker";

    public PlayerTrackerTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme) { }

    public override void Draw()
    {
        DrawListCard();

        if (selectedPlayerId.HasValue)
        {
            theme.SpacerY();
            DrawDetailCard();
        }
    }

    private void DrawListCard()
    {
        bool enabled = true;
        theme.DrawPluginCardAuto(
            id: "player-tracker-list",
            title: "Tracked Players",
            enabled: ref enabled,
            showCheckbox: false,
            drawContent: (avail) =>
            {
                int total = plugin.PlayerTracker.Count();
                ImGui.TextColored(theme.MutedText, $"{total} player(s) tracked");
                theme.SpacerY(0.5f);

                ImGui.SetNextItemWidth(avail);
                if (ImGui.InputTextWithHint("##playerTrackerSearch", "Search by name, world, or notes...", ref searchText, 64))
                {
                    lastListRefresh = DateTime.MinValue;
                }

                theme.SpacerY(0.5f);

                if ((DateTime.UtcNow - lastListRefresh) > ListRefreshInterval)
                {
                    cachedList = string.IsNullOrWhiteSpace(searchText)
                        ? plugin.PlayerTracker.GetRecent(200)
                        : plugin.PlayerTracker.Search(searchText, 200);
                    lastListRefresh = DateTime.UtcNow;
                }

                if (cachedList.Count == 0)
                {
                    ImGui.TextDisabled("No players match.");
                    return;
                }

                using var table = ImRaii.Table("##playerList", 5,
                    ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH
                    | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp);
                if (!table) return;

                float scale = ImGuiHelpers.GlobalScale;
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthFixed, 110f * scale);
                ImGui.TableSetupColumn("Seen", ImGuiTableColumnFlags.WidthFixed, 60f * scale);
                ImGui.TableSetupColumn("Last Seen", ImGuiTableColumnFlags.WidthFixed, 130f * scale);
                ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 80f * scale);
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableHeadersRow();

                foreach (var p in cachedList)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    bool isSelected = selectedPlayerId == p.LocalId;
                    if (ImGui.Selectable($"{p.Info.Name}##row-{p.LocalId}", isSelected, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        selectedPlayerId = isSelected ? null : p.LocalId;
                    }

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(p.Info.World);

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(p.Stats.SeenCount.ToString());

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(FormatRelative(p.Stats.LastSeen));

                    ImGui.TableNextColumn();
                    if (p.IsProvisional)
                        ImGui.TextColored(theme.MutedText, "Provisional");
                    else
                        ImGui.TextColored(theme.MutedText, "Confirmed");
                }
            }
        );
    }

    private void DrawDetailCard()
    {
        var p = plugin.PlayerTracker.GetRecent(500).FirstOrDefault(x => x.LocalId == selectedPlayerId);
        if (p == null)
        {
            selectedPlayerId = null;
            return;
        }

        bool enabled = true;
        theme.DrawPluginCardAuto(
            id: "player-tracker-detail",
            title: $"{p.Info.Name} @ {p.Info.World}",
            enabled: ref enabled,
            showCheckbox: false,
            drawContent: (avail) =>
            {
                DrawInfoSection(p);
                theme.SpacerY(0.5f);
                ImGui.Separator();
                theme.SpacerY(0.5f);

                DrawStatsSection(p);
                theme.SpacerY(0.5f);
                ImGui.Separator();
                theme.SpacerY(0.5f);

                DrawHistorySection(p);
                theme.SpacerY(0.5f);
                ImGui.Separator();
                theme.SpacerY(0.5f);

                DrawNotesAndTagsSection(p);
                theme.SpacerY(0.5f);

                if (theme.DangerIconButton("##delete-tracked", FontAwesomeIcon.Trash, "Delete this entry"))
                {
                    plugin.PlayerTracker.Delete(p.LocalId);
                    selectedPlayerId = null;
                    lastListRefresh = DateTime.MinValue;
                }
                ImGui.SameLine();
                ImGui.TextColored(theme.MutedText, "Delete this entry (irreversible)");
            },
            drawHeaderRight: () =>
            {
                if (p.ContentId.HasValue)
                {
                    ImGui.TextColored(theme.MutedText, $"ContentId: {p.ContentId.Value:X}");
                }
                else if (!string.IsNullOrEmpty(p.LodestoneId))
                {
                    ImGui.TextColored(theme.MutedText, $"Lodestone: {p.LodestoneId}");
                }
                else
                {
                    ImGui.TextColored(theme.MutedText, "No identity yet");
                }
            }
        );
    }

    private void DrawInfoSection(TrackedPlayer p)
    {
        ImGui.Text("Info");
        theme.SpacerY(0.3f);

        DrawKeyValue("Race", ResolveRace(p.Info.RaceId));
        DrawKeyValue("Tribe", ResolveTribe(p.Info.TribeId));
        DrawKeyValue("Gender", ResolveGender(p.Info.Gender));
        DrawKeyValue("Free Company", p.Info.FreeCompanyTag);
    }

    private void DrawStatsSection(TrackedPlayer p)
    {
        ImGui.Text("Stats");
        theme.SpacerY(0.3f);

        DrawKeyValue("Seen count", p.Stats.SeenCount.ToString());
        DrawKeyValue("First seen", $"{p.Stats.FirstSeen.ToLocalTime():yyyy-MM-dd HH:mm}");
        DrawKeyValue("First seen via", p.Stats.FirstSeenVia.ToString());
        DrawKeyValue("Last seen", $"{p.Stats.LastSeen.ToLocalTime():yyyy-MM-dd HH:mm} ({FormatRelative(p.Stats.LastSeen)})");
        DrawKeyValue("Last location", p.Stats.LastTerritoryName ?? (p.Stats.LastTerritoryId?.ToString() ?? "—"));
    }

    private void DrawHistorySection(TrackedPlayer p)
    {
        ImGui.Text($"History ({p.History.Count})");
        theme.SpacerY(0.3f);

        if (p.History.Count == 0)
        {
            ImGui.TextDisabled("(no history)");
            return;
        }

        using var child = ImRaii.Child("##history-scroll",
            new Vector2(-1, 200f * ImGuiHelpers.GlobalScale), true);
        if (!child) return;

        var grouped = p.History
            .OrderBy(h => h.When)
            .GroupBy(h => h.When.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
            .ToList();

        foreach (var group in grouped)
        {
            ImGui.TextColored(theme.MutedText, group.Key);
            using var indent = ImRaii.PushIndent();
            foreach (var change in group)
            {
                var oldVal = string.IsNullOrEmpty(change.OldValue) ? "—" : change.OldValue;
                var newVal = string.IsNullOrEmpty(change.NewValue) ? "—" : change.NewValue;
                ImGui.TextUnformatted($"  {change.Field}: {oldVal} → {newVal}");
            }
        }
    }

    private void DrawNotesAndTagsSection(TrackedPlayer p)
    {
        ImGui.Text("Notes");
        theme.SpacerY(0.3f);

        string notes = p.Notes;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextMultiline("##notes", ref notes, 1024, new Vector2(-1, 60f * ImGuiHelpers.GlobalScale)))
        {
            p.Notes = notes;
        }
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            plugin.PlayerTracker.SaveChanges(p);
        }

        theme.SpacerY(0.5f);
        ImGui.Text($"Tags ({p.Tags.Count})");
        if (p.Tags.Count > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(theme.MutedText, string.Join(", ", p.Tags));
        }
    }

    private void DrawKeyValue(string key, string? value)
    {
        ImGui.TextColored(theme.MutedText, $"  {key}:");
        ImGui.SameLine();
        ImGui.TextUnformatted(string.IsNullOrEmpty(value) ? "—" : value);
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

    private static string? ResolveRace(byte? raceId)
    {
        if (!raceId.HasValue) return null;
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<Race>();
            if (sheet == null) return raceId.Value.ToString();
            var row = sheet.GetRow(raceId.Value);
            var name = row.Masculine.ExtractText();
            return string.IsNullOrEmpty(name) ? raceId.Value.ToString() : name;
        }
        catch { return raceId.Value.ToString(); }
    }

    private static string? ResolveTribe(byte? tribeId)
    {
        if (!tribeId.HasValue) return null;
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<Tribe>();
            if (sheet == null) return tribeId.Value.ToString();
            var row = sheet.GetRow(tribeId.Value);
            var name = row.Masculine.ExtractText();
            return string.IsNullOrEmpty(name) ? tribeId.Value.ToString() : name;
        }
        catch { return tribeId.Value.ToString(); }
    }

    private static string? ResolveGender(byte? gender)
    {
        if (!gender.HasValue) return null;
        return gender.Value switch
        {
            0 => "Male",
            1 => "Female",
            _ => gender.Value.ToString(),
        };
    }
}
