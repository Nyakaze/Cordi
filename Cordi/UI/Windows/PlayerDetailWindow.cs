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

    // Derived/display cache, rebuilt only when the underlying record actually changes.
    private Guid? _cacheForPlayer;
    private int _cachedHistoryCount = -1;
    private int _cachedEncounterCount = -1;
    private int _historyShownCount;
    private List<HistoryGroup> _groupedHistory = new();
    private List<EncounterRow> _encounterRows = new();
    private string _encounterSummary = string.Empty;
    private string? _cachedRace;
    private string? _cachedTribe;
    private string? _cachedGender;
    private string _glance = string.Empty;

    // Stat-strip values: time-sensitive, so recomputed on the (1s) refresh tick — not per frame.
    private string _statSeen = "0";
    private string _statFirstRel = "—";
    private string _statFirstAbs = "—";
    private string _statLastRel = "—";
    private string _statLastAbs = "—";
    private string _statTogether = "—";
    private string _statEncLabel = "0 encounters";

    // Toolbar icon group width, measured once per font-scale change rather than every frame.
    private float _toolbarScale = -1f;
    private float _toolbarGroupW;

    // Transient UI state.
    private bool _confirmDelete;
    private string _tagInput = string.Empty;

    private readonly record struct HistoryGroup(DateTime When, List<IdentityChange> Items);
    private readonly record struct EncounterRow(
        string WhenRelative, string WhenAbsolute, string Location, string Duration, string LevelClass);

    public PlayerDetailWindow(CordiPlugin plugin)
        : base("Player Details###CordiPlayerDetails", ImGuiWindowFlags.None)
    {
        _plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(440, 540),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        RespectCloseHotkey = true;
    }

    public void Show(Guid playerId)
    {
        _playerId = playerId;
        _player = _plugin.PlayerTracker.GetByLocalId(playerId);
        _lastRefresh = DateTime.UtcNow;
        _confirmDelete = false;
        _tagInput = string.Empty;
        InvalidateCache();
        if (_player != null) RecomputeStats(_player);
        IsOpen = true;
    }

    private void InvalidateCache()
    {
        _cacheForPlayer = null;
        _cachedHistoryCount = -1;
        _cachedEncounterCount = -1;
        _groupedHistory.Clear();
        _encounterRows.Clear();
        _encounterSummary = string.Empty;
        _cachedRace = null;
        _cachedTribe = null;
        _cachedGender = null;
        _glance = string.Empty;
    }

    private void RebuildCacheIfNeeded(TrackedPlayer p)
    {
        bool playerChanged = _cacheForPlayer != p.LocalId;
        bool historyChanged = _cachedHistoryCount != p.History.Count;
        bool encountersChanged = _cachedEncounterCount != p.Encounters.Count;
        if (!playerChanged && !historyChanged && !encountersChanged) return;

        if (playerChanged)
        {
            _cachedRace = ResolveRace(p.Info.RaceId);
            _cachedTribe = ResolveTribe(p.Info.TribeId);
            _cachedGender = ResolveGender(p.Info.Gender);
            _glance = BuildGlance(p);
        }

        if (playerChanged || historyChanged)
        {
            // Drop creation-time noise (entries that were unknown both before and after,
            // i.e. "(initial) → —"); keep first-known values and real before→after changes.
            _groupedHistory = p.History
                .Where(h => !(string.IsNullOrEmpty(h.OldValue) && string.IsNullOrEmpty(h.NewValue)))
                .OrderByDescending(h => h.When)
                .GroupBy(h => h.When.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
                .Select(g => new HistoryGroup(g.First().When, g.ToList()))
                .ToList();
            _historyShownCount = _groupedHistory.Sum(g => g.Items.Count);
            _cachedHistoryCount = p.History.Count;
        }

        if (playerChanged || encountersChanged)
        {
            _encounterRows = p.Encounters
                .OrderByDescending(e => e.StartedAt)
                .Select(e => new EncounterRow(
                    WhenRelative: FormatRelative(e.StartedAt),
                    WhenAbsolute: e.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    Location: e.TerritoryName ?? (e.TerritoryId?.ToString() ?? "—"),
                    Duration: FormatDuration(e.Duration),
                    LevelClass: FormatLevelClass(e.Level, e.ClassJobId)))
                .ToList();

            var total = p.Encounters.Aggregate(TimeSpan.Zero, (acc, e) => acc + e.Duration);
            _encounterSummary = p.Encounters.Count == 0
                ? "No encounters recorded yet."
                : $"{p.Encounters.Count} encounter{(p.Encounters.Count == 1 ? "" : "s")} · {FormatDuration(total)} together";
            _cachedEncounterCount = p.Encounters.Count;
        }

        _cacheForPlayer = p.LocalId;
    }

    private string BuildGlance(TrackedPlayer p)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(_cachedRace)) parts.Add(_cachedRace!);
        if (!string.IsNullOrEmpty(_cachedGender)) parts.Add(_cachedGender!);
        if (!string.IsNullOrEmpty(p.Info.FreeCompanyTag)) parts.Add($"«{p.Info.FreeCompanyTag}»");
        return parts.Count == 0 ? "Character details not yet known" : string.Join("  ·  ", parts);
    }

    public override void PreDraw()
    {
        base.PreDraw();
        _theme.PushWindow();

        // Dock as a drawer to the right of the main config window when it is open.
        var main = _plugin.MainConfigWindow;
        if (main != null && main.LastSize.X > 0 && main.LastSize.Y > 0)
        {
            var pos = main.LastPos + new Vector2(main.LastSize.X, 0);
            var size = new Vector2(470f * ImGuiHelpers.GlobalScale, main.LastSize.Y);
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
            if (_player != null) RecomputeStats(_player);
        }

        if (_player == null)
        {
            DrawEmptyState();
            return;
        }

        var p = _player;
        RebuildCacheIfNeeded(p);

        DrawHeader(p);
        _theme.SpacerY(0.4f);
        DrawStatStrip(p);
        _theme.SpacerY(0.5f);

        using var tabs = ImRaii.TabBar("##player-tabs", ImGuiTabBarFlags.None);
        if (!tabs) return;

        using (var t = ImRaii.TabItem("Overview"))
            if (t) DrawTabBody("##tab-overview", () => DrawOverview(p));

        using (var t = ImRaii.TabItem($"Encounters ({p.Encounters.Count})###tab-enc"))
            if (t) DrawTabBody("##tab-enc-body", () => DrawEncounters(p));

        using (var t = ImRaii.TabItem($"History ({_historyShownCount})###tab-hist"))
            if (t) DrawTabBody("##tab-hist-body", () => DrawHistory(p));
    }

    private void DrawEmptyState()
    {
        var avail = ImGui.GetContentRegionAvail();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + avail.Y * 0.4f);
        const string msg = "Select a player to view their details.";
        var w = ImGui.CalcTextSize(msg).X;
        ImGui.SetCursorPosX(MathF.Max(0, (avail.X - w) * 0.5f));
        ImGui.TextColored(_theme.MutedText, msg);
    }

    // ---- Header ---------------------------------------------------------------

    private void DrawHeader(TrackedPlayer p)
    {
        // Name (prominent).
        _theme.ApplyFontScale(1.6f);
        ImGui.TextUnformatted(p.Info.Name);
        _theme.ApplyFontScale();

        // World + status pill. Grouped so the Badge's cursor side effects don't
        // bleed into the next line — the glance below returns to the left margin.
        using (ImRaii.Group())
        {
            ImGui.TextColored(_theme.MutedText, p.Info.World);
            ImGui.SameLine(0, _theme.Gap(0.6f));
            if (p.IsProvisional)
                _theme.Badge("Provisional", _theme.FrameBg, _theme.MutedText);
            else
                _theme.Badge("Confirmed", _theme.Accent, _theme.AccentText);
        }

        // Glance line.
        ImGui.TextColored(_theme.MutedText, _glance);

        _theme.SpacerY(0.3f);
        DrawToolbar(p);
        _theme.SpacerY(0.3f);
        ImGui.Separator();
    }

    private void DrawToolbar(TrackedPlayer p)
    {
        if (_confirmDelete)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(UiTheme.ColorDangerText, "Delete this entry permanently?");
            ImGui.SameLine();

            float w = ImGui.CalcTextSize("Delete").X + ImGui.CalcTextSize("Cancel").X
                + ImGui.GetStyle().FramePadding.X * 4f + ImGui.GetStyle().ItemSpacing.X;
            RightAlignCursor(w);

            using (ImRaii.PushColor(ImGuiCol.Button, UiTheme.ColorDanger)
                .Push(ImGuiCol.ButtonHovered, UiTheme.ColorDanger)
                .Push(ImGuiCol.ButtonActive, UiTheme.ColorDanger))
            {
                if (_theme.Button("Delete##confirm"))
                {
                    _plugin.PlayerTracker.Delete(p.LocalId);
                    _player = null;
                    _playerId = null;
                    _confirmDelete = false;
                    IsOpen = false;
                    return;
                }
            }
            ImGui.SameLine();
            if (_theme.SecondaryButton("Cancel")) _confirmDelete = false;
            return;
        }

        // Right-aligned icon actions. The group width is measured from the actual icon
        // glyphs (so it sits flush inside the window) but only re-measured when the font
        // scale changes, not every frame.
        EnsureToolbarWidth();
        RightAlignCursor(_toolbarGroupW);

        if (_theme.SecondaryIconButton("##copy", FontAwesomeIcon.Copy, "Copy \"Name@World\""))
            ImGui.SetClipboardText($"{p.Info.Name}@{p.Info.World}");

        ImGui.SameLine();
        bool hasLodestone = !string.IsNullOrEmpty(p.LodestoneId);
        if (_theme.SecondaryIconButton("##lodestone", FontAwesomeIcon.ExternalLinkAlt,
            hasLodestone ? "Open Lodestone profile" : "Search Lodestone for this name + world"))
            OpenLodestone(p, hasLodestone);

        ImGui.SameLine();
        if (_theme.DangerIconButton("##delete", FontAwesomeIcon.Trash, "Delete this player entry"))
            _confirmDelete = true;
    }

    /// <summary>Positions the cursor so a row of width <paramref name="groupW"/> ends flush at the
    /// inner right edge, with a small margin, never running past the window.</summary>
    private void RightAlignCursor(float groupW)
    {
        float startX = ImGui.GetContentRegionMax().X - groupW - _theme.Gap(0.5f);
        if (startX > ImGui.GetCursorPosX())
            ImGui.SetCursorPosX(startX);
    }

    private void EnsureToolbarWidth()
    {
        float scale = ImGuiHelpers.GlobalScale;
        if (MathF.Abs(scale - _toolbarScale) < 0.001f) return;
        _toolbarScale = scale;

        float sp = ImGui.GetStyle().ItemSpacing.X;
        _toolbarGroupW = IconButtonWidth(FontAwesomeIcon.Copy)
            + IconButtonWidth(FontAwesomeIcon.ExternalLinkAlt)
            + IconButtonWidth(FontAwesomeIcon.Trash)
            + sp * 2f;
    }

    private static float IconButtonWidth(FontAwesomeIcon icon)
    {
        using (ImRaii.PushFont(UiBuilder.IconFont))
            return ImGui.CalcTextSize(icon.ToIconString()).X + ImGui.GetStyle().FramePadding.X * 2f;
    }

    // ---- Stat strip -----------------------------------------------------------

    private void DrawStatStrip(TrackedPlayer p)
    {
        using var border = ImRaii.PushColor(ImGuiCol.TableBorderLight, _theme.WindowBorder);
        using var table = ImRaii.Table("##stat-strip", 4,
            ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.BordersInnerV);
        if (!table) return;

        ImGui.TableNextColumn();
        DrawStat("SEEN", _statSeen, "visits");

        ImGui.TableNextColumn();
        DrawStat("FIRST SEEN", _statFirstRel, _statFirstAbs, tooltipFromSub: true);

        ImGui.TableNextColumn();
        DrawStat("LAST SEEN", _statLastRel, _statLastAbs, tooltipFromSub: true);

        ImGui.TableNextColumn();
        DrawStat("TOGETHER", _statTogether, _statEncLabel);
    }

    private void RecomputeStats(TrackedPlayer p)
    {
        _statSeen = p.Stats.SeenCount.ToString("N0");
        _statFirstRel = FormatRelative(p.Stats.FirstSeen);
        _statFirstAbs = p.Stats.FirstSeen == default ? "—" : p.Stats.FirstSeen.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        _statLastRel = FormatRelative(p.Stats.LastSeen);
        _statLastAbs = p.Stats.LastSeen == default ? "—" : p.Stats.LastSeen.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        int ec = p.Encounters.Count;
        var total = p.Encounters.Aggregate(TimeSpan.Zero, (acc, e) => acc + e.Duration);
        _statTogether = ec == 0 ? "—" : FormatDuration(total);
        _statEncLabel = $"{ec} encounter{(ec == 1 ? "" : "s")}";
    }

    private void DrawStat(string label, string value, string subtitle, bool tooltipFromSub = false)
    {
        ImGui.TextColored(_theme.MutedText, label);

        _theme.ApplyFontScale(1.25f);
        ImGui.TextUnformatted(value);
        _theme.ApplyFontScale();
        if (tooltipFromSub && ImGui.IsItemHovered()) ImGui.SetTooltip(subtitle);

        ImGui.TextColored(_theme.MutedText, subtitle);
    }

    // ---- Overview tab ---------------------------------------------------------

    private void DrawOverview(TrackedPlayer p)
    {
        SubHeading("Identity");
        DrawRow("Race", _cachedRace);
        DrawRow("Clan", _cachedTribe);
        DrawRow("Gender", _cachedGender);
        DrawRow("Free Company", p.Info.FreeCompanyTag);

        _theme.SpacerY(0.7f);
        SubHeading("Activity");
        DrawRow("First seen", $"{p.Stats.FirstSeen.ToLocalTime():yyyy-MM-dd HH:mm}  ({FormatRelative(p.Stats.FirstSeen)})");
        DrawRow("First source", p.Stats.FirstSeenVia.ToString());
        DrawRow("Last location", p.Stats.LastTerritoryName ?? (p.Stats.LastTerritoryId?.ToString()));

        _theme.SpacerY(0.7f);
        SubHeading("Identifiers");
        DrawRow("ContentId", p.ContentId.HasValue ? $"{p.ContentId.Value:X}" : null);
        DrawRow("Lodestone", p.LodestoneId);

        _theme.SpacerY(0.7f);
        SubHeading("Notes");
        string notes = p.Notes;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextMultiline("##player-notes", ref notes, 2048,
            new Vector2(-1, 80f * ImGuiHelpers.GlobalScale)))
        {
            p.Notes = notes;
        }
        if (ImGui.IsItemDeactivatedAfterEdit())
            _plugin.PlayerTracker.SaveChanges(p);

        _theme.SpacerY(0.7f);
        SubHeading($"Tags ({p.Tags.Count})");
        DrawTagEditor(p);
    }

    private void DrawTagEditor(TrackedPlayer p)
    {
        float avail = ImGui.GetContentRegionAvail().X;
        var style = ImGui.GetStyle();
        string? toRemove = null;

        if (p.Tags.Count == 0)
        {
            ImGui.TextColored(_theme.MutedText, "(no tags)");
        }
        else
        {
            using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, _theme.Radius(1.2f)))
            using (ImRaii.PushColor(ImGuiCol.Button, _theme.Accent)
                .Push(ImGuiCol.ButtonHovered, UiTheme.ColorDanger)
                .Push(ImGuiCol.ButtonActive, UiTheme.ColorDanger)
                .Push(ImGuiCol.Text, _theme.AccentText))
            {
                float lineW = 0f;
                for (int i = 0; i < p.Tags.Count; i++)
                {
                    string label = $"{p.Tags[i]}  ×";
                    float w = ImGui.CalcTextSize(label).X + style.FramePadding.X * 2f;

                    if (i > 0 && lineW + style.ItemSpacing.X + w <= avail)
                    {
                        ImGui.SameLine();
                        lineW += style.ItemSpacing.X + w;
                    }
                    else
                    {
                        lineW = w;
                    }

                    if (ImGui.Button($"{label}##tag{i}")) toRemove = p.Tags[i];
                    _theme.HoverHandIfItem();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Click to remove this tag");
                }
            }
        }

        if (toRemove != null)
        {
            p.Tags.Remove(toRemove);
            _plugin.PlayerTracker.SaveChanges(p);
        }

        _theme.SpacerY(0.3f);

        float addBtnW = ImGui.CalcTextSize("Add").X + style.FramePadding.X * 2f + _theme.Gap(2f);
        ImGui.SetNextItemWidth(avail - addBtnW - style.ItemSpacing.X);
        bool submitted = ImGui.InputTextWithHint("##tag-add", "Add a tag…", ref _tagInput, 48,
            ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        bool clicked = _theme.SecondaryButton("Add", new Vector2(addBtnW, 0));
        if (submitted || clicked) AddTag(p);
    }

    private void AddTag(TrackedPlayer p)
    {
        var tag = _tagInput.Trim();
        _tagInput = string.Empty;
        if (tag.Length == 0) return;
        if (p.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) return;

        p.Tags.Add(tag);
        _plugin.PlayerTracker.SaveChanges(p);
    }

    // ---- Encounters tab -------------------------------------------------------

    private void DrawEncounters(TrackedPlayer p)
    {
        if (_encounterRows.Count == 0)
        {
            _theme.SpacerY();
            ImGui.TextColored(_theme.MutedText,
                "No encounters recorded yet.\nThey appear here once you cross paths in the world.");
            return;
        }

        ImGui.TextColored(_theme.MutedText, _encounterSummary);
        _theme.SpacerY(0.3f);

        float scale = ImGuiHelpers.GlobalScale;
        using var table = ImRaii.Table("##encounters-table", 4,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.NoBordersInBody
            | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.ScrollY,
            new Vector2(0, 0));
        if (!table) return;

        ImGui.TableSetupColumn("When", ImGuiTableColumnFlags.WidthFixed, 70f * scale);
        ImGui.TableSetupColumn("Where", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("For", ImGuiTableColumnFlags.WidthFixed, 64f * scale);
        ImGui.TableSetupColumn("As", ImGuiTableColumnFlags.WidthFixed, 92f * scale);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        var clipper = new ImGuiListClipper();
        clipper.Begin(_encounterRows.Count, ImGui.GetTextLineHeightWithSpacing());
        while (clipper.Step())
        {
            for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var row = _encounterRows[i];
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.WhenRelative);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(row.WhenAbsolute);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.Location);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(row.Location);

                ImGui.TableNextColumn();
                ImGui.TextColored(_theme.MutedText, row.Duration);

                ImGui.TableNextColumn();
                ImGui.TextColored(_theme.MutedText, row.LevelClass);
            }
        }
        clipper.End();
    }

    // ---- History tab ----------------------------------------------------------

    private void DrawHistory(TrackedPlayer p)
    {
        if (_groupedHistory.Count == 0)
        {
            _theme.SpacerY();
            ImGui.TextColored(_theme.MutedText, "No identity changes recorded yet.");
            return;
        }

        float scale = ImGuiHelpers.GlobalScale;
        float gutter = 24f * scale;
        var origin = ImGui.GetCursorScreenPos();
        float railX = origin.X + 7f * scale;
        var nodeYs = new List<float>(_groupedHistory.Count);

        _theme.SpacerY(0.2f);
        using (ImRaii.PushIndent(gutter))
        {
            for (int gi = 0; gi < _groupedHistory.Count; gi++)
            {
                var group = _groupedHistory[gi];
                nodeYs.Add(ImGui.GetCursorScreenPos().Y + ImGui.GetTextLineHeight() * 0.5f);

                var local = group.When.ToLocalTime();
                ImGui.TextUnformatted(local.ToString("d MMM yyyy"));
                ImGui.SameLine(0, _theme.Gap(0.4f));
                ImGui.TextColored(_theme.MutedText, local.ToString("HH:mm"));
                ImGui.SameLine(0, _theme.Gap(0.6f));
                ImGui.TextColored(_theme.MutedText, $"· {FormatRelative(group.When)}");

                _theme.SpacerY(0.15f);
                DrawChangeTable(group, gi);
                _theme.SpacerY(0.6f);
            }
        }

        // Timeline rail + nodes, drawn in the reserved left gutter.
        var dl = ImGui.GetWindowDrawList();
        if (nodeYs.Count > 1)
            dl.AddLine(new Vector2(railX, nodeYs[0]), new Vector2(railX, nodeYs[^1]),
                ImGui.GetColorU32(_theme.WindowBorder), 2f * scale);
        foreach (var y in nodeYs)
            dl.AddCircleFilled(new Vector2(railX, y), 4f * scale, ImGui.GetColorU32(_theme.Accent));
    }

    private void DrawChangeTable(HistoryGroup group, int idx)
    {
        float scale = ImGuiHelpers.GlobalScale;
        using var table = ImRaii.Table($"##hist-{idx}", 2, ImGuiTableFlags.SizingStretchProp);
        if (!table) return;

        ImGui.TableSetupColumn("##field", ImGuiTableColumnFlags.WidthFixed, 110f * scale);
        ImGui.TableSetupColumn("##value", ImGuiTableColumnFlags.WidthStretch);

        foreach (var change in group.Items)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextColored(_theme.MutedText, FormatHistoryField(change.Field));

            ImGui.TableNextColumn();
            DrawChangeValue(change);
        }
    }

    private void DrawChangeValue(IdentityChange change)
    {
        string newD = FormatHistoryValue(change.Field, change.NewValue);

        // First time this field became known: just show the value.
        if (string.IsNullOrEmpty(change.OldValue))
        {
            ImGui.TextUnformatted(newD);
            return;
        }

        // A real change: old → new.
        ImGui.TextColored(_theme.MutedText, FormatHistoryValue(change.Field, change.OldValue));
        ImGui.SameLine(0, _theme.Gap(0.4f));
        ImGui.TextColored(_theme.MutedText, "→");
        ImGui.SameLine(0, _theme.Gap(0.4f));
        ImGui.TextUnformatted(newD);
    }

    // ---- Shared layout helpers ------------------------------------------------

    private void DrawTabBody(string id, System.Action draw)
    {
        _theme.SpacerY(0.3f);
        using var child = ImRaii.Child(id, new Vector2(0, 0), false);
        if (!child) return;
        draw();
    }

    private void SubHeading(string text)
    {
        ImGui.TextColored(_theme.MutedText, text.ToUpperInvariant());
        _theme.SpacerY(0.1f);
        ImGui.Separator();
        _theme.SpacerY(0.3f);
    }

    private void DrawRow(string label, string? value)
    {
        float labelW = 120f * ImGuiHelpers.GlobalScale;
        ImGui.TextColored(_theme.MutedText, label);
        ImGui.SameLine(labelW);
        ImGui.TextUnformatted(string.IsNullOrEmpty(value) ? "—" : value);
    }

    private static void OpenLodestone(TrackedPlayer p, bool hasLodestone)
    {
        string url = hasLodestone
            ? $"https://eu.finalfantasyxiv.com/lodestone/character/{p.LodestoneId}/"
            : $"https://eu.finalfantasyxiv.com/lodestone/character/?q={Uri.EscapeDataString(p.Info.Name)}&worldname={Uri.EscapeDataString(p.Info.World)}";
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    // ---- Formatting / resolution ---------------------------------------------

    private static string FormatHistoryField(string field) => field switch
    {
        "RaceId" => "Race",
        "TribeId" => "Clan",
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

    private static string FormatDuration(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        if (span.TotalSeconds < 60) return $"{(int)span.TotalSeconds}s";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m {span.Seconds}s";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h {span.Minutes}m";
        return $"{(int)span.TotalDays}d {span.Hours}h";
    }

    private static string FormatLevelClass(byte? level, uint? classJobId)
    {
        var job = ResolveClassJob(classJobId);
        bool hasLevel = level.HasValue && level.Value > 0;

        if (hasLevel && job != null) return $"Lv{level} {job}";
        if (hasLevel) return $"Lv{level}";
        return job ?? "—";
    }

    private static string? ResolveClassJob(uint? classJobId)
    {
        if (!classJobId.HasValue || classJobId.Value == 0) return null;
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<ClassJob>();
            if (sheet == null) return classJobId.Value.ToString();
            var abbr = sheet.GetRow(classJobId.Value).Abbreviation.ExtractText();
            return string.IsNullOrEmpty(abbr) ? classJobId.Value.ToString() : abbr.ToUpperInvariant();
        }
        catch { return classJobId.Value.ToString(); }
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
