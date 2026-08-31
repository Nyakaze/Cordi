using System;
using System.Numerics;
using Cordi.UI.Search;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Components;

public sealed class SearchBox
{
    private readonly UiTheme theme;
    private readonly SettingsSearchIndex index;

    private string query = string.Empty;
    private bool inputActive;
    private int selectedResult;

    public SearchBox(UiTheme theme, SettingsSearchIndex index)
    {
        this.theme = theme;
        this.index = index;
    }

    public bool HasQuery => !string.IsNullOrWhiteSpace(query);

    public void Draw(float width)
    {
        var draw = ImGui.GetWindowDrawList();
        float height = theme.Scaled(34f);
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, height);

        draw.AddRectFilled(min, max, ImGui.GetColorU32(theme.FrameBg), theme.Radius());
        draw.AddRect(min, max, ImGui.GetColorU32(inputActive ? theme.AccentBorder : theme.Border), theme.Radius());

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.FaintText))
        {
            var glyph = FontAwesomeIcon.Search.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(new Vector2(min.X + theme.PadX(0.9f), min.Y + (height - size.Y) * 0.5f));
            ImGui.TextUnformatted(glyph);
        }

        float inputX = min.X + theme.PadX(0.9f) + theme.Scaled(22f);
        float clearWidth = HasQuery ? theme.Scaled(26f) : 0f;
        float inputWidth = max.X - theme.PadX(0.6f) - clearWidth - inputX;

        ImGui.SetCursorScreenPos(new Vector2(inputX, min.Y + (height - ImGui.GetFrameHeight()) * 0.5f));
        ImGui.SetNextItemWidth(inputWidth);

        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.FrameBgHovered, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.FrameBgActive, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 0f))
        {
            ImGui.InputTextWithHint("##cordi-search", "Search settings...", ref query, 128);
        }

        inputActive = ImGui.IsItemActive();

        if (clearWidth <= 0f)
        {
            ImGui.SetCursorScreenPos(new Vector2(min.X, max.Y));
            return;
        }

        var clearMin = new Vector2(max.X - theme.PadX(0.4f) - clearWidth, min.Y);

        ImGui.SetCursorScreenPos(clearMin);
        if (ImGui.InvisibleButton("##cordi-search-clear", new Vector2(clearWidth, height)))
            query = string.Empty;

        bool clearHovered = ImGui.IsItemHovered();
        if (clearHovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, clearHovered ? theme.Text : theme.FaintText))
        {
            var glyph = FontAwesomeIcon.Times.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(new Vector2(clearMin.X + (clearWidth - size.X) * 0.5f, min.Y + (height - size.Y) * 0.5f));
            ImGui.TextUnformatted(glyph);
        }

        ImGui.SetCursorScreenPos(new Vector2(min.X, max.Y));
    }

    public SearchEntry? DrawResults()
    {
        var results = index.Search(query, 20);

        theme.ApplyFontScale(0.9f);
        using (ImRaii.PushColor(ImGuiCol.Text, theme.MutedText))
            ImGui.TextUnformatted($"{results.Count} result(s) for \"{query.Trim()}\"");
        theme.ApplyFontScale();

        theme.SpacerY(0.75f);

        if (results.Count == 0)
        {
            ImGui.TextDisabled("Nothing matched. Try a page name like \"chatbox\" or \"tracker\".");
            return null;
        }

        if (selectedResult >= results.Count)
            selectedResult = 0;

        if (inputActive)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.DownArrow, true))
                selectedResult = (selectedResult + 1) % results.Count;
            if (ImGui.IsKeyPressed(ImGuiKey.UpArrow, true))
                selectedResult = (selectedResult - 1 + results.Count) % results.Count;
        }

        SearchEntry? navigateTo = null;

        if (inputActive && (ImGui.IsKeyPressed(ImGuiKey.Enter, false) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter, false)))
            navigateTo = results[selectedResult];

        float rowHeight = theme.Scaled(46f);

        for (int i = 0; i < results.Count; i++)
        {
            if (DrawResultRow(results[i], i == selectedResult, rowHeight, i))
                navigateTo = results[i];
        }

        if (navigateTo != null)
        {
            query = string.Empty;
            selectedResult = 0;
        }

        return navigateTo;
    }

    private bool DrawResultRow(SearchEntry entry, bool selected, float height, int index)
    {
        var draw = ImGui.GetWindowDrawList();
        float width = ImGui.GetContentRegionAvail().X;
        var min = ImGui.GetCursorScreenPos();

        bool clicked = ImGui.InvisibleButton($"##search-result-{index}", new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        var max = min + new Vector2(width, height);
        draw.AddRectFilled(min, max, ImGui.GetColorU32(selected ? theme.AccentSelected : hovered ? theme.RowHover : theme.RowBg), theme.Radius());

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.Accent))
        {
            var glyph = entry.Icon.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(new Vector2(min.X + theme.PadX(1f), min.Y + (height - size.Y) * 0.5f));
            ImGui.TextUnformatted(glyph);
        }

        float textX = min.X + theme.PadX(1f) + theme.Scaled(26f);
        bool hasKeywords = !string.IsNullOrEmpty(entry.Keywords);

        if (hasKeywords)
        {
            float lineHeight = ImGui.GetTextLineHeight();
            float blockHeight = lineHeight * 1.86f + theme.Scaled(4f);
            float titleY = min.Y + (height - blockHeight) * 0.5f;

            ImGui.SetCursorScreenPos(new Vector2(textX, titleY));
            ImGui.TextUnformatted(entry.Label);

            theme.ApplyFontScale(0.84f);
            ImGui.SetCursorScreenPos(new Vector2(textX, titleY + lineHeight + theme.Scaled(4f)));
            using (ImRaii.PushColor(ImGuiCol.Text, theme.MutedText))
                ImGui.TextUnformatted(entry.Keywords);
            theme.ApplyFontScale();
        }
        else
        {
            var labelSize = ImGui.CalcTextSize(entry.Label);
            ImGui.SetCursorScreenPos(new Vector2(textX, min.Y + (height - labelSize.Y) * 0.5f));
            ImGui.TextUnformatted(entry.Label);
        }

        theme.ApplyFontScale(0.82f);
        var pageSize = ImGui.CalcTextSize(entry.PageLabel);
        ImGui.SetCursorScreenPos(new Vector2(max.X - pageSize.X - theme.PadX(1f), min.Y + (height - pageSize.Y) * 0.5f));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.FaintText))
            ImGui.TextUnformatted(entry.PageLabel);
        theme.ApplyFontScale();

        ImGui.SetCursorScreenPos(new Vector2(min.X, max.Y + theme.Gap(0.4f)));
        return clicked;
    }
}
