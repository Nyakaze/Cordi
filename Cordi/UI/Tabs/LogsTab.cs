using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Core;
using Cordi.Services;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Tabs;

public class LogsTab : ConfigTabBase
{
    private string _searchText = string.Empty;
    private string _selectedSource = string.Empty; // empty = all
    private CordiLogLevel _minLevel = CordiLogLevel.Debug;
    private bool _autoScroll = true;

    public override string Label => "Logs";

    public LogsTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme) { }

    public override void Draw()
    {
        var logService = plugin.LogService;
        if (logService == null) return;

        theme.SpacerY(0.5f);

        // --- Toolbar ---
        DrawToolbar(logService);

        theme.SpacerY(0.5f);
        ImGui.Separator();
        theme.SpacerY(0.5f);

        // --- Log entries ---
        DrawLogEntries(logService);
    }

    private void DrawToolbar(CordiLogService logService)
    {
        // Search
        ImGui.TextColored(theme.MutedText, "Search");
        ImGui.SameLine();
        using (ImRaii.ItemWidth(200))
        {
            ImGui.InputText("##log-search", ref _searchText, 256);
        }

        ImGui.SameLine(0, 15);

        // Source filter
        ImGui.TextColored(theme.MutedText, "Source");
        ImGui.SameLine();
        var sources = logService.GetSources();
        sources.Insert(0, "(All)");
        int currentIdx = string.IsNullOrEmpty(_selectedSource) ? 0 : sources.IndexOf(_selectedSource);
        if (currentIdx < 0) currentIdx = 0;

        using (ImRaii.ItemWidth(150))
        {
            if (ImGui.BeginCombo("##log-source", sources[currentIdx]))
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    bool selected = i == currentIdx;
                    if (ImGui.Selectable(sources[i], selected))
                    {
                        _selectedSource = i == 0 ? string.Empty : sources[i];
                    }
                    if (selected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
        }

        ImGui.SameLine(0, 15);

        // Level filter
        ImGui.TextColored(theme.MutedText, "Level");
        ImGui.SameLine();
        var levels = Enum.GetNames(typeof(CordiLogLevel));
        int levelIdx = (int)_minLevel;
        using (ImRaii.ItemWidth(100))
        {
            if (ImGui.BeginCombo("##log-level", levels[levelIdx]))
            {
                for (int i = 0; i < levels.Length; i++)
                {
                    bool selected = i == levelIdx;
                    if (ImGui.Selectable(levels[i], selected))
                    {
                        _minLevel = (CordiLogLevel)i;
                    }
                    if (selected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
        }

        ImGui.SameLine(0, 15);

        // Auto-scroll toggle
        ImGui.Checkbox("Auto-scroll", ref _autoScroll);

        ImGui.SameLine(0, 15);

        // Clear button
        if (theme.SecondaryButton("Clear", new Vector2(60, 0)))
        {
            logService.Clear();
        }
        theme.HoverHandIfItem();
    }

    private void DrawLogEntries(CordiLogService logService)
    {
        var entries = logService.GetEntries();

        // Apply filters
        var filtered = entries.Where(e => e.Level >= _minLevel);

        if (!string.IsNullOrEmpty(_selectedSource))
            filtered = filtered.Where(e => e.Source == _selectedSource);

        if (!string.IsNullOrEmpty(_searchText))
        {
            var search = _searchText;
            filtered = filtered.Where(e =>
                e.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Source.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();

        using var child = ImRaii.Child("##log-entries", new Vector2(0, 0), true);
        if (!child.Success) return;

        // Use clipper for performance with large lists
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 2));
        for (int i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            DrawLogEntry(entry);
        }
        ImGui.PopStyleVar();

        if (_autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 20)
            ImGui.SetScrollHereY(1.0f);
    }

    private void DrawLogEntry(CordiLogEntry entry)
    {
        var levelColor = entry.Level switch
        {
            CordiLogLevel.Debug => theme.MutedText,
            CordiLogLevel.Info => theme.Text,
            CordiLogLevel.Warning => new Vector4(1.0f, 0.8f, 0.2f, 1.0f),
            CordiLogLevel.Error => new Vector4(0.9f, 0.2f, 0.2f, 1.0f),
            _ => theme.Text,
        };

        var timeStr = entry.Timestamp.ToString("HH:mm:ss.fff");
        var levelStr = entry.Level switch
        {
            CordiLogLevel.Debug => "DBG",
            CordiLogLevel.Info => "INF",
            CordiLogLevel.Warning => "WRN",
            CordiLogLevel.Error => "ERR",
            _ => "???",
        };

        ImGui.TextColored(theme.MutedText, timeStr);
        ImGui.SameLine(0, 8);
        ImGui.TextColored(levelColor, $"[{levelStr}]");
        ImGui.SameLine(0, 8);
        ImGui.TextColored(theme.Accent, $"[{entry.Source}]");
        ImGui.SameLine(0, 8);
        ImGui.TextColored(entry.Level >= CordiLogLevel.Warning ? levelColor : theme.Text, entry.Message);
    }
}
