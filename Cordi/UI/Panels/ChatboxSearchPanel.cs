using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Cordi.Core;
using Cordi.Domain;
using Cordi.Services.Chatbox;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Panels;

public sealed partial class ChatboxSearchPanel : IDisposable
{
    private const int FilterColumns = 3;

    private static readonly IReadOnlyList<DropdownItem> FieldOptions = Options(
        (ChatboxSearchField.Everything, "Everything"),
        (ChatboxSearchField.Message, "Message text"),
        (ChatboxSearchField.Author, "Player name"));

    private static readonly IReadOnlyList<DropdownItem> MatchOptions = Options(
        (ChatboxSearchMatch.Contains, "Contains"),
        (ChatboxSearchMatch.WholeWord, "Whole word"),
        (ChatboxSearchMatch.StartsWith, "Starts with"),
        (ChatboxSearchMatch.Exact, "Exact match"),
        (ChatboxSearchMatch.Regex, "Regular expression"));

    private static readonly IReadOnlyList<DropdownItem> RangeOptions = Options(
        (ChatboxSearchRange.Any, "Any time"),
        (ChatboxSearchRange.Today, "Today"),
        (ChatboxSearchRange.Yesterday, "Yesterday"),
        (ChatboxSearchRange.LastSevenDays, "Last 7 days"),
        (ChatboxSearchRange.LastThirtyDays, "Last 30 days"),
        (ChatboxSearchRange.ThisYear, "This year"),
        (ChatboxSearchRange.Custom, "Custom"));

    private static readonly IReadOnlyList<DropdownItem> SortOptions = Options(
        (ChatboxSearchSort.Newest, "Newest first"),
        (ChatboxSearchSort.Oldest, "Oldest first"),
        (ChatboxSearchSort.AuthorAscending, "Player A to Z"),
        (ChatboxSearchSort.AuthorDescending, "Player Z to A"),
        (ChatboxSearchSort.ChannelThenNewest, "Channel, newest first"),
        (ChatboxSearchSort.ChannelThenOldest, "Channel, oldest first"),
        (ChatboxSearchSort.LongestFirst, "Longest message first"),
        (ChatboxSearchSort.ShortestFirst, "Shortest message first"));

    private static readonly IReadOnlyList<DropdownItem> FlagOptions = Options(
        (ChatboxSearchFlag.Any, "Any"),
        (ChatboxSearchFlag.Only, "Only these"),
        (ChatboxSearchFlag.Exclude, "Exclude"));

    private readonly CordiPlugin _plugin;
    private readonly UiTheme _theme;
    private readonly string _id;
    private readonly ChatboxSearchSession _session;

    private string _text = string.Empty;
    private string _author = string.Empty;
    private string _fromDate = string.Empty;
    private string _toDate = string.Empty;
    private string _fromTime = string.Empty;
    private string _toTime = string.Empty;
    private string _validation = string.Empty;
    private ChatboxSearchRange _range = ChatboxSearchRange.Any;
    private IReadOnlyList<ChatboxChannelSummary> _archived = Array.Empty<ChatboxChannelSummary>();
    private bool _showFilters = true;

    public ChatboxSearchPanel(CordiPlugin plugin, UiTheme theme, string id)
    {
        _plugin = plugin;
        _theme = theme;
        _id = id;
        _session = new ChatboxSearchSession(plugin);
    }

    public Action<ChatboxMessage>? OnOpenMessage { get; set; }

    private ChatboxSearchQuery Query => _session.Query;

    private ChatboxService Chatbox => _plugin.Chatbox;

    public void FocusInput() => _focusInput = true;

    public void Draw(float width, float resultsHeight)
    {
        DrawSearchBar(width);

        if (_showFilters)
        {
            _theme.SpacerY(0.4f);
            DrawFilters(width);
        }

        _theme.SpacerY(0.4f);
        DrawStatus(width);
        DrawExportBar(width);
        DrawResults(width, resultsHeight);
    }

    private bool _focusInput;

    private void DrawSearchBar(float width)
    {
        var controlHeight = _theme.Scaled(UiTheme.ControlHeight);
        var gap = _theme.Gap(0.5f);
        var buttonWidth = _theme.Scaled(92f);
        var toggleWidth = _theme.Scaled(96f);
        var inputWidth = MathF.Max(120f, width - (buttonWidth * 2f + toggleWidth + gap * 3f));

        var pos = ImGui.GetCursorScreenPos();
        var submit = false;

        if (_focusInput)
        {
            _focusInput = false;
            ImGui.SetKeyboardFocusHere();
        }

        _theme.PushInputScope();
        if (_theme.TextInput(
                $"##{_id}-text", pos, inputWidth, ref _text, 512,
                "Search messages, players, keywords...",
                ImGuiInputTextFlags.EnterReturnsTrue))
            submit = true;
        _theme.PopInputScope();

        ImGui.SameLine(0, gap);
        if (_theme.PrimaryButton($"Search##{_id}", new Vector2(buttonWidth, controlHeight))) submit = true;

        ImGui.SameLine(0, gap);
        if (_theme.SecondaryButton($"Reset##{_id}", new Vector2(buttonWidth, controlHeight))) Reset();

        ImGui.SameLine(0, gap);
        if (_theme.SecondaryButton(
                $"{(_showFilters ? "Hide" : "Show")} Filters##{_id}",
                new Vector2(toggleWidth, controlHeight)))
            _showFilters = !_showFilters;

        if (submit) Run();
    }

    private void DrawFilters(float width)
    {
        DrawRow(width,
            ("Search in", w => Picker("field", w, () => Query.Field, v => Query.Field = v, FieldOptions)),
            ("Matching", w => Picker("match", w, () => Query.Match, v => Query.Match = v, MatchOptions)),
            ("Case", DrawMatchCase));

        DrawRow(width,
            ("Player", w =>
            {
                _theme.PushInputScope();
                _theme.TextInput($"##{_id}-author", ImGui.GetCursorScreenPos(), w, ref _author, 128, "Name or world");
                _theme.PopInputScope();
            }),
            ("Channels", DrawChannelPicker),
            ("Sort", w => Picker("sort", w, () => Query.Sort, v => Query.Sort = v, SortOptions)));

        DrawRow(width,
            ("Date range", w => Picker("range", w, () => _range, ApplyRange, RangeOptions)),
            ("From date", w => DateInput("from-date", w, ref _fromDate, "YYYY-MM-DD")),
            ("To date", w => DateInput("to-date", w, ref _toDate, "YYYY-MM-DD")));

        DrawRow(width,
            ("From time", w => DateInput("from-time", w, ref _fromTime, "HH:MM")),
            ("To time", w => DateInput("to-time", w, ref _toTime, "HH:MM")),
            ("Max results", w =>
            {
                var value = Query.Limit;
                if (_theme.NumberInput($"##{_id}-limit", ImGui.GetCursorScreenPos(), w, ref value, 10, 20000))
                    Query.Limit = value;
            }));

        DrawRow(width,
            ("Mentions me", w => Picker("mentions", w, () => Query.Mentions, v => Query.Mentions = v, FlagOptions)),
            ("Sent by me", w => Picker("self", w, () => Query.FromMe, v => Query.FromMe = v, FlagOptions)),
            ("Attachments", w => Picker("attach", w, () => Query.Attachments, v => Query.Attachments = v, FlagOptions)));

        DrawRow(width,
            ("Links", w => Picker("links", w, () => Query.Links, v => Query.Links = v, FlagOptions)),
            ("Filtered ads", w => Picker("ads", w, () => Query.FilteredAds, v => Query.FilteredAds = v, FlagOptions)),
            ("Hide types", DrawCategoryPicker));
    }

    private void DrawMatchCase(float width)
    {
        var control = _theme.Scaled(UiTheme.ControlHeight);
        var pos = ImGui.GetCursorScreenPos();

        ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + MathF.Max(0f, (control - ImGui.GetFrameHeight()) * 0.5f)));

        var value = Query.MatchCase;
        if (_theme.Checkbox($"Match case##{_id}", ref value)) Query.MatchCase = value;
    }

    private void DrawRow(float width, params (string Label, Action<float> Draw)[] fields)
    {
        var gap = _theme.Gap(0.6f);
        var column = MathF.Max(70f, (width - gap * (FilterColumns - 1)) / FilterColumns);
        var labelHeight = ImGui.GetTextLineHeightWithSpacing();
        var rowHeight = labelHeight + _theme.Scaled(UiTheme.ControlHeight);

        var origin = ImGui.GetCursorScreenPos();

        for (var i = 0; i < fields.Length; i++)
        {
            var x = origin.X + i * (column + gap);

            ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
            _theme.MutedLabel(fields[i].Label);

            ImGui.SetCursorScreenPos(new Vector2(x, origin.Y + labelHeight));
            fields[i].Draw(column);
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + rowHeight));
        ImGui.Dummy(new Vector2(width, _theme.Gap(0.5f)));
    }

    private void Picker<T>(string key, float width, Func<T> get, Action<T> set, IReadOnlyList<DropdownItem> items)
        where T : struct, Enum =>
        _theme.OptionPicker(
            $"{_id}-{key}",
            get().ToString(),
            items,
            value =>
            {
                if (Enum.TryParse<T>(value, out var parsed)) set(parsed);
            },
            width);

    private void DateInput(string key, float width, ref string value, string hint)
    {
        var pos = ImGui.GetCursorScreenPos();
        _theme.PushInputScope();

        if (_theme.TextInput($"##{_id}-{key}", pos, width, ref value, 24, hint))
            _range = ChatboxSearchRange.Custom;

        _theme.PopInputScope();
    }

    private void DrawChannelPicker(float width)
    {
        var label = Query.ChannelIds.Count == 0 ? "All channels" : $"{Query.ChannelIds.Count} selected";
        var popupId = $"{_id}-channels-popup";

        if (_theme.SecondaryButton($"{label}##{_id}-channels", new Vector2(width, _theme.Scaled(UiTheme.ControlHeight))))
        {
            _archived = Chatbox.ArchivedChannels();
            ImGui.OpenPopup(popupId);
        }

        using var popup = ImRaii.Popup(popupId);
        if (!popup) return;

        if (_theme.SecondaryButton($"All##{_id}-channels-all", new Vector2(_theme.Scaled(70f), _theme.Scaled(26f))))
            Query.ChannelIds.Clear();

        _theme.SpacerY(0.3f);

        using var child = ImRaii.Child($"##{_id}-channels-list", new Vector2(_theme.Scaled(320f), _theme.Scaled(360f)), false);
        if (!child) return;

        foreach (var channel in Chatbox.Channels)
            ChannelToggle(channel.Id, channel.Config.Name);

        if (_archived.Count == 0) return;

        _theme.SpacerY(0.4f);
        _theme.MutedLabel("Older history from removed channels");

        foreach (var summary in _archived)
            ChannelToggle(summary.Id, ArchivedLabel(summary));
    }

    private void ChannelToggle(string channelId, string label)
    {
        var toggled = Query.ChannelIds.Contains(channelId);

        if (!_theme.Checkbox($"{label}##{_id}-ch-{channelId}", ref toggled)) return;

        if (toggled) Query.ChannelIds.Add(channelId);
        else Query.ChannelIds.Remove(channelId);
    }

    private static string ArchivedLabel(ChatboxChannelSummary summary)
    {
        var name = summary.Id.StartsWith("dm:", StringComparison.Ordinal)
            ? summary.Id[3..]
            : ChatTypes.Label(summary.DominantType);

        return string.Create(CultureInfo.InvariantCulture,
            $"{name} - {summary.Count} msg, {summary.FirstSeen:yyyy-MM-dd} to {summary.LastSeen:yyyy-MM-dd}");
    }

    private void DrawCategoryPicker(float width)
    {
        var hidden = Query.ExcludedCategories.Count;
        var label = hidden == 0 ? "Nothing hidden" : $"{hidden} hidden";
        var popupId = $"{_id}-categories-popup";

        if (_theme.SecondaryButton($"{label}##{_id}-categories", new Vector2(width, _theme.Scaled(UiTheme.ControlHeight))))
            ImGui.OpenPopup(popupId);

        using var popup = ImRaii.Popup(popupId);
        if (!popup) return;

        var buttonSize = new Vector2(_theme.Scaled(80f), _theme.Scaled(26f));

        if (_theme.SecondaryButton($"None##{_id}-categories-none", buttonSize))
            Query.ExcludedCategories.Clear();

        ImGui.SameLine(0, _theme.Gap(0.4f));

        if (_theme.SecondaryButton($"Default##{_id}-categories-default", buttonSize))
        {
            Query.ExcludedCategories.Clear();
            Query.ExcludedCategories.AddRange(ChatboxSearchCategories.Default);
        }

        _theme.SpacerY(0.3f);

        foreach (var category in ChatboxSearchCategories.All)
        {
            var toggled = Query.ExcludedCategories.Contains(category);

            if (!_theme.Checkbox($"{ChatboxSearchCategories.Label(category)}##{_id}-cat-{category}", ref toggled)) continue;

            if (toggled) Query.ExcludedCategories.Add(category);
            else Query.ExcludedCategories.Remove(category);
        }

        _theme.SpacerY(0.3f);
        _theme.MutedLabel("Ticked types are left out of the results.");
    }

    private void ApplyRange(ChatboxSearchRange range)
    {
        _range = range;

        var today = DateTime.Today;

        switch (range)
        {
            case ChatboxSearchRange.Any:
                _fromDate = string.Empty;
                _toDate = string.Empty;
                break;
            case ChatboxSearchRange.Today:
                _fromDate = Format(today);
                _toDate = Format(today);
                break;
            case ChatboxSearchRange.Yesterday:
                _fromDate = Format(today.AddDays(-1));
                _toDate = Format(today.AddDays(-1));
                break;
            case ChatboxSearchRange.LastSevenDays:
                _fromDate = Format(today.AddDays(-6));
                _toDate = Format(today);
                break;
            case ChatboxSearchRange.LastThirtyDays:
                _fromDate = Format(today.AddDays(-29));
                _toDate = Format(today);
                break;
            case ChatboxSearchRange.ThisYear:
                _fromDate = Format(new DateTime(today.Year, 1, 1));
                _toDate = Format(today);
                break;
        }
    }

    private static string Format(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private void Reset()
    {
        _text = string.Empty;
        _author = string.Empty;
        _fromDate = string.Empty;
        _toDate = string.Empty;
        _fromTime = string.Empty;
        _toTime = string.Empty;
        _validation = string.Empty;
        _range = ChatboxSearchRange.Any;

        Query.Text = string.Empty;
        Query.Author = string.Empty;
        Query.Field = ChatboxSearchField.Everything;
        Query.Match = ChatboxSearchMatch.Contains;
        Query.MatchCase = false;
        Query.ChannelIds.Clear();
        Query.ExcludedCategories.Clear();
        Query.ExcludedCategories.AddRange(ChatboxSearchCategories.Default);
        Query.From = null;
        Query.To = null;
        Query.TimeFromMinutes = null;
        Query.TimeToMinutes = null;
        Query.Mentions = ChatboxSearchFlag.Any;
        Query.FromMe = ChatboxSearchFlag.Any;
        Query.Attachments = ChatboxSearchFlag.Any;
        Query.Links = ChatboxSearchFlag.Any;
        Query.FilteredAds = ChatboxSearchFlag.Exclude;
        Query.Sort = ChatboxSearchSort.Newest;
        Query.Limit = ChatboxSearchQuery.DefaultLimit;

        _selected.Clear();
        _exportStatus = string.Empty;
        _session.Clear();
    }

    private void Run()
    {
        _validation = string.Empty;

        Query.Text = _text;
        Query.Author = _author;

        if (!TryDate(_fromDate, out var from)) return;
        if (!TryDate(_toDate, out var to)) return;
        if (!TryTime(_fromTime, out var timeFrom)) return;
        if (!TryTime(_toTime, out var timeTo)) return;

        Query.From = from;
        Query.To = to?.AddDays(1);
        Query.TimeFromMinutes = timeFrom;
        Query.TimeToMinutes = timeTo;

        if (string.IsNullOrWhiteSpace(Query.Text) && !Query.HasTerms)
        {
            _validation = "Enter a keyword or pick at least one filter.";
            return;
        }

        _selected.Clear();
        _exportStatus = string.Empty;
        _session.Start();
    }

    private bool TryDate(string value, out DateTime? parsed)
    {
        parsed = null;

        if (string.IsNullOrWhiteSpace(value)) return true;

        if (DateTime.TryParseExact(
                value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || DateTime.TryParse(value.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
        {
            parsed = date.Date;
            return true;
        }

        _validation = $"\"{value}\" is not a date. Use YYYY-MM-DD.";
        return false;
    }

    private bool TryTime(string value, out int? minutes)
    {
        minutes = null;

        if (string.IsNullOrWhiteSpace(value)) return true;

        if (TimeSpan.TryParseExact(value.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out var time)
            || TimeSpan.TryParseExact(value.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out time)
            || TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out time))
        {
            minutes = (int)time.TotalMinutes;
            return true;
        }

        _validation = $"\"{value}\" is not a time. Use HH:MM.";
        return false;
    }

    private static IReadOnlyList<DropdownItem> Options<T>(params (T Value, string Label)[] entries) where T : struct, Enum
    {
        var list = new List<DropdownItem>(entries.Length);

        foreach (var (value, label) in entries)
            list.Add(new DropdownItem { Key = value.ToString(), Label = label });

        return list;
    }

    public void Dispose() => _session.Dispose();
}
