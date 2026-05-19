using System;
using System.Linq;
using System.Numerics;
using Cordi.Core;
using Cordi.Domain.Tracking;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace Cordi.UI.Windows;

public class PlayerDetailWindow : Window
{
    private readonly CordiPlugin _plugin;
    private readonly UiTheme _theme = new();
    private TrackedPlayer? _player;
    private Guid? _playerId;
    private DateTime _lastRefresh = DateTime.MinValue;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    public PlayerDetailWindow(CordiPlugin plugin)
        : base("Player Details###CordiPlayerDetails", ImGuiWindowFlags.None)
    {
        _plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 520),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        RespectCloseHotkey = true;
    }

    public void Show(Guid playerId)
    {
        _playerId = playerId;
        _player = _plugin.PlayerTracker.GetByLocalId(playerId);
        _lastRefresh = DateTime.UtcNow;
        IsOpen = true;
    }

    public override void PreDraw()
    {
        base.PreDraw();
        _theme.PushWindow();

        var main = _plugin.MainConfigWindow;
        if (main != null && main.LastSize.X > 0 && main.LastSize.Y > 0)
        {
            var pos = main.LastPos + new Vector2(main.LastSize.X, 0);
            var size = new Vector2(440f * ImGuiHelpers.GlobalScale, main.LastSize.Y);
            ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
            ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        }
    }

    public override void PostDraw()
    {
        _theme.PopWindow();
        base.PostDraw();
    }

    public override void Draw()
    {
        _theme.ApplyFontScale();

        if (_playerId.HasValue && (DateTime.UtcNow - _lastRefresh) > RefreshInterval)
        {
            _player = _plugin.PlayerTracker.GetByLocalId(_playerId.Value);
            _lastRefresh = DateTime.UtcNow;
        }

        if (_player == null)
        {
            ImGui.TextColored(_theme.MutedText, "No player selected.");
            return;
        }

        DrawHeader(_player);
        _theme.SpacerY(0.5f);
        DrawKpiTiles(_player);
        _theme.SpacerY(1f);

        DrawSection("Identity", () => DrawIdentity(_player));
        _theme.SpacerY(0.5f);

        DrawSection("Activity", () => DrawActivity(_player));
        _theme.SpacerY(0.5f);

        DrawSection($"History ({_player.History.Count})", () => DrawHistory(_player));
        _theme.SpacerY(0.5f);

        DrawSection("Notes & Tags", () => DrawNotes(_player));
        _theme.SpacerY(1f);

        DrawFooter(_player);
    }

    private void DrawHeader(TrackedPlayer p)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        using (ImRaii.Group())
        {
            _theme.ApplyFontScale(1.5f);
            ImGui.TextUnformatted(p.Info.Name);
            _theme.ApplyFontScale();

            ImGui.TextColored(_theme.MutedText, p.Info.World);
        }

        ImGui.SameLine();
        var rightX = avail;

        using (ImRaii.Group())
        {
            float dotSize = 8f * ImGuiHelpers.GlobalScale;
            Vector4 statusColor = p.IsProvisional
                ? new Vector4(0.7f, 0.7f, 0.7f, 1f)
                : UiTheme.ColorSuccessText;
            string statusLabel = p.IsProvisional ? "Provisional" : "Confirmed";

            float groupWidth = ImGui.CalcTextSize(statusLabel).X + dotSize + ImGui.GetStyle().ItemSpacing.X + 8;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, rightX - groupWidth - ImGui.GetCursorPosX()));

            ImGui.TextColored(statusColor, FontAwesomeIcon.Circle.ToIconString().Length > 0 ? "●" : "*");
            ImGui.SameLine(0, 4f);
            ImGui.TextColored(statusColor, statusLabel);
        }

        _theme.SpacerY(0.3f);
        using (ImRaii.Group())
        {
            if (p.ContentId.HasValue)
            {
                ImGui.TextColored(_theme.MutedText, $"ContentId: {p.ContentId.Value:X}");
            }
            if (!string.IsNullOrEmpty(p.LodestoneId))
            {
                if (p.ContentId.HasValue) ImGui.SameLine();
                ImGui.TextColored(_theme.MutedText, $"Lodestone: {p.LodestoneId}");
            }
            if (!p.ContentId.HasValue && string.IsNullOrEmpty(p.LodestoneId))
            {
                ImGui.TextColored(_theme.MutedText, "No identity resolved");
            }
        }
    }

    private void DrawKpiTiles(TrackedPlayer p)
    {
        float avail = ImGui.GetContentRegionAvail().X;
        float colW = avail / 3f;

        DrawKpi("SEEN COUNT", p.Stats.SeenCount.ToString("N0"), "total visits");
        ImGui.SameLine(colW);
        DrawKpi("LAST SEEN", FormatRelative(p.Stats.LastSeen), $"{p.Stats.LastSeen.ToLocalTime():yyyy-MM-dd HH:mm}");
        ImGui.SameLine(colW * 2);
        DrawKpi("LOCATION",
            p.Stats.LastTerritoryName ?? (p.Stats.LastTerritoryId?.ToString() ?? "—"),
            "last seen at");
    }

    private void DrawKpi(string label, string value, string subtitle)
    {
        using (ImRaii.Group())
        {
            ImGui.TextColored(_theme.MutedText, label);
            _theme.ApplyFontScale(1.2f);
            ImGui.TextUnformatted(value);
            _theme.ApplyFontScale();
            ImGui.TextColored(_theme.MutedText, subtitle);
        }
    }

    private void DrawSection(string title, System.Action drawContent)
    {
        ImGui.TextUnformatted(title);
        _theme.SpacerY(0.2f);
        ImGui.Separator();
        _theme.SpacerY(0.4f);
        using var indent = ImRaii.PushIndent();
        drawContent();
    }

    private void DrawIdentity(TrackedPlayer p)
    {
        DrawRow("Race", ResolveRace(p.Info.RaceId));
        DrawRow("Tribe", ResolveTribe(p.Info.TribeId));
        DrawRow("Gender", ResolveGender(p.Info.Gender));
        DrawRow("Free Company", p.Info.FreeCompanyTag);
    }

    private void DrawActivity(TrackedPlayer p)
    {
        DrawRow("First seen", $"{p.Stats.FirstSeen.ToLocalTime():yyyy-MM-dd HH:mm}  ({FormatRelative(p.Stats.FirstSeen)})");
        DrawRow("First source", p.Stats.FirstSeenVia.ToString());
        DrawRow("Total sightings", p.Stats.SeenCount.ToString("N0"));
    }

    private void DrawHistory(TrackedPlayer p)
    {
        if (p.History.Count == 0)
        {
            ImGui.TextColored(_theme.MutedText, "(no history)");
            return;
        }

        using var child = ImRaii.Child("##player-history",
            new Vector2(-1, 180f * ImGuiHelpers.GlobalScale), true);
        if (!child) return;

        var grouped = p.History
            .OrderByDescending(h => h.When)
            .GroupBy(h => h.When.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
            .ToList();

        foreach (var group in grouped)
        {
            ImGui.TextColored(_theme.MutedText, group.Key);
            using var indent = ImRaii.PushIndent();
            foreach (var change in group)
            {
                ImGui.TextUnformatted(FormatHistoryField(change.Field));
                ImGui.SameLine();
                ImGui.TextColored(_theme.MutedText, "·");
                ImGui.SameLine();
                if (string.IsNullOrEmpty(change.OldValue))
                {
                    ImGui.TextColored(_theme.MutedText, "(initial)");
                    ImGui.SameLine();
                    ImGui.TextColored(_theme.MutedText, "→");
                    ImGui.SameLine();
                    ImGui.TextUnformatted(FormatHistoryValue(change.Field, change.NewValue));
                }
                else
                {
                    ImGui.TextColored(_theme.MutedText, FormatHistoryValue(change.Field, change.OldValue));
                    ImGui.SameLine();
                    ImGui.TextColored(_theme.MutedText, "→");
                    ImGui.SameLine();
                    ImGui.TextUnformatted(FormatHistoryValue(change.Field, change.NewValue));
                }
            }
            _theme.SpacerY(0.2f);
        }
    }

    private void DrawNotes(TrackedPlayer p)
    {
        ImGui.TextColored(_theme.MutedText, "Notes");
        _theme.SpacerY(0.2f);
        string notes = p.Notes;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextMultiline("##player-notes", ref notes, 2048,
            new Vector2(-1, 80f * ImGuiHelpers.GlobalScale)))
        {
            p.Notes = notes;
        }
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _plugin.PlayerTracker.SaveChanges(p);
        }

        _theme.SpacerY(0.5f);
        ImGui.TextColored(_theme.MutedText, $"Tags ({p.Tags.Count})");
        ImGui.SameLine();
        if (p.Tags.Count > 0)
            ImGui.TextUnformatted(string.Join(", ", p.Tags));
        else
            ImGui.TextColored(_theme.MutedText, "(none)");
    }

    private void DrawFooter(TrackedPlayer p)
    {
        ImGui.Separator();
        _theme.SpacerY(0.4f);

        bool hasLodestone = !string.IsNullOrEmpty(p.LodestoneId);
        string buttonLabel = hasLodestone ? "Open on Lodestone" : "Search on Lodestone";
        if (ImGui.Button(buttonLabel))
        {
            string url = hasLodestone
                ? $"https://eu.finalfantasyxiv.com/lodestone/character/{p.LodestoneId}/"
                : $"https://eu.finalfantasyxiv.com/lodestone/character/?q={Uri.EscapeDataString(p.Info.Name)}&worldname={Uri.EscapeDataString(p.Info.World)}";
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(hasLodestone ? "Opens this player's Lodestone profile" : "Opens a Lodestone search for this name+world");

        ImGui.SameLine();
        float btnW = ImGui.CalcTextSize("Delete").X + ImGui.GetStyle().FramePadding.X * 2 + 30;
        ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() - btnW);

        if (_theme.DangerIconButton("##player-delete", FontAwesomeIcon.Trash, "Delete this player entry"))
        {
            _plugin.PlayerTracker.Delete(p.LocalId);
            _player = null;
            _playerId = null;
            IsOpen = false;
        }
    }

    private void DrawRow(string label, string? value)
    {
        float labelW = 130f * ImGuiHelpers.GlobalScale;
        using (ImRaii.Group())
        {
            ImGui.TextColored(_theme.MutedText, label);
        }
        ImGui.SameLine(labelW);
        ImGui.TextUnformatted(string.IsNullOrEmpty(value) ? "—" : value);
    }

    private static string FormatHistoryField(string field) => field switch
    {
        "RaceId" => "Race",
        "TribeId" => "Tribe",
        "Gender" => "Gender",
        "FreeCompanyTag" => "Free Company",
        _ => field,
    };

    private static string FormatHistoryValue(string field, string? value)
    {
        if (string.IsNullOrEmpty(value)) return "—";
        if (!byte.TryParse(value, out var b)) return value;
        return field switch
        {
            "RaceId" => ResolveRace(b) ?? value,
            "TribeId" => ResolveTribe(b) ?? value,
            "Gender" => ResolveGender(b) ?? value,
            _ => value,
        };
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
