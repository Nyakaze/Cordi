using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Components;

public sealed class SidebarNav
{
    private readonly UiTheme theme;

    public SidebarNav(UiTheme theme)
    {
        this.theme = theme;
    }

    public string? Draw(IReadOnlyList<NavSection> sections, string selectedId, SidebarFooterState footer)
    {
        string? clicked = null;

        float width = theme.Scaled(UiTheme.SidebarWidth);
        float footerHeight = theme.Scaled(96f);

        using (ImRaii.PushColor(ImGuiCol.ChildBg, theme.SidebarBg))
        using (ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, theme.Radius(1.2f)))
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(theme.PadX(0.8f), theme.PadY(0.8f))))
        using (var child = ImRaii.Child("##cordi-sidebar", new Vector2(width, 0), true))
        {
            if (!child)
                return null;

            DrawBrand();

            float listHeight = ImGui.GetContentRegionAvail().Y - footerHeight;
            bool showFooter = listHeight >= theme.Scaled(120f);
            if (!showFooter)
                listHeight = ImGui.GetContentRegionAvail().Y;

            using (var list = ImRaii.Child("##cordi-nav-list", new Vector2(0, listHeight), false))
            {
                if (list)
                {
                    foreach (var section in sections)
                    {
                        if (section.Items.Count == 0)
                            continue;

                        DrawSectionLabel(section.Label);

                        foreach (var item in section.Items)
                        {
                            if (DrawItem(item, item.Id == selectedId))
                                clicked = item.Id;
                        }

                        theme.SpacerY(0.75f);
                    }
                }
            }

            if (showFooter)
                DrawFooter(footer);
        }

        return clicked;
    }

    private void DrawBrand()
    {
        var draw = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        float tile = theme.Scaled(38f);

        draw.AddRectFilled(origin, origin + new Vector2(tile, tile), ImGui.GetColorU32(theme.AccentSoft), theme.Radius(1.2f));

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.Accent))
        {
            var glyph = FontAwesomeIcon.Cat.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(origin + new Vector2((tile - size.X) * 0.5f, (tile - size.Y) * 0.5f));
            ImGui.TextUnformatted(glyph);
        }

        float textX = origin.X + tile + theme.Gap();
        theme.ApplyFontScale(1.8f);
        var titleSize = ImGui.CalcTextSize("Cordi");
        ImGui.SetCursorScreenPos(new Vector2(textX, origin.Y + (tile - titleSize.Y) * 0.5f));
        ImGui.TextUnformatted("Cordi");
        theme.ApplyFontScale();

        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + tile + theme.Gap(1.2f)));
        DrawDivider();
        theme.SpacerY(0.75f);
    }

    private void DrawDivider()
    {
        var draw = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X;
        draw.AddLine(p, new Vector2(p.X + w, p.Y), ImGui.GetColorU32(theme.Border), 1f * ImGuiHelpers.GlobalScale);
    }

    private void DrawSectionLabel(string label)
    {
        theme.SpacerY(0.5f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + theme.PadX(0.6f));
        theme.ApplyFontScale(0.82f);
        using (ImRaii.PushColor(ImGuiCol.Text, theme.FaintText))
            ImGui.TextUnformatted(label.ToUpperInvariant());
        theme.ApplyFontScale();
        theme.SpacerY(0.35f);
    }

    private bool DrawItem(NavItem item, bool active)
    {
        var draw = ImGui.GetWindowDrawList();
        float height = theme.Scaled(UiTheme.NavItemHeight);
        float width = ImGui.GetContentRegionAvail().X;

        var min = ImGui.GetCursorScreenPos();
        bool clicked = ImGui.InvisibleButton($"##nav-{item.Id}", new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        var max = min + new Vector2(width, height);
        float radius = theme.Radius();

        if (active)
        {
            draw.AddRectFilled(min, max, ImGui.GetColorU32(theme.AccentSelected), radius);
            draw.AddRect(min, max, ImGui.GetColorU32(theme.AccentSoft), radius);
        }
        else if (hovered)
        {
            draw.AddRectFilled(min, max, ImGui.GetColorU32(theme.RowHover), radius);
        }

        var textColor = active ? theme.Text : hovered ? theme.Text : theme.MutedText;

        using (ImRaii.PushColor(ImGuiCol.Text, textColor))
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                var glyph = item.Icon.ToIconString();
                var size = ImGui.CalcTextSize(glyph);
                ImGui.SetCursorScreenPos(new Vector2(min.X + theme.PadX(1f), min.Y + (height - size.Y) * 0.5f));
                ImGui.TextUnformatted(glyph);
            }

            var labelSize = ImGui.CalcTextSize(item.Label);
            ImGui.SetCursorScreenPos(new Vector2(min.X + theme.PadX(1f) + theme.Scaled(26f), min.Y + (height - labelSize.Y) * 0.5f));
            ImGui.TextUnformatted(item.Label);
        }

        ImGui.SetCursorScreenPos(new Vector2(min.X, max.Y + theme.Gap(0.25f)));
        return clicked;
    }

    private void DrawFooter(SidebarFooterState footer)
    {
        var draw = ImGui.GetWindowDrawList();
        float width = ImGui.GetContentRegionAvail().X;
        float cardHeight = theme.Scaled(48f);

        var min = ImGui.GetCursorScreenPos();
        bool clicked = ImGui.InvisibleButton("##nav-bot-card", new Vector2(width, cardHeight));
        bool hovered = ImGui.IsItemHovered();
        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (clicked)
            footer.OnBotCardClicked?.Invoke();

        var max = min + new Vector2(width, cardHeight);
        draw.AddRectFilled(min, max, ImGui.GetColorU32(hovered ? theme.RowHover : theme.CardBg), theme.Radius());
        draw.AddRect(min, max, ImGui.GetColorU32(theme.Border), theme.Radius());

        float tile = theme.Scaled(28f);
        var tileMin = new Vector2(min.X + theme.PadX(0.6f), min.Y + (cardHeight - tile) * 0.5f);
        draw.AddRectFilled(tileMin, tileMin + new Vector2(tile, tile), ImGui.GetColorU32(theme.AccentSoft), theme.Radius(0.8f));

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.Accent))
        {
            var glyph = FontAwesomeIcon.Robot.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(tileMin + new Vector2((tile - size.X) * 0.5f, (tile - size.Y) * 0.5f));
            ImGui.TextUnformatted(glyph);
        }

        float textX = tileMin.X + tile + theme.Gap(0.75f);
        theme.ApplyFontScale(0.92f);
        ImGui.SetCursorScreenPos(new Vector2(textX, min.Y + theme.Scaled(8f)));
        ImGui.TextUnformatted("Cordi Discord Bot");

        float dotRadius = theme.Scaled(3f);
        float statusY = min.Y + theme.Scaled(26f);
        draw.AddCircleFilled(new Vector2(textX + dotRadius, statusY + dotRadius + theme.Scaled(2f)), dotRadius,
            ImGui.GetColorU32(footer.BotOnline ? UiTheme.ColorOnline : UiTheme.ColorOffline));

        ImGui.SetCursorScreenPos(new Vector2(textX + dotRadius * 2 + theme.Gap(0.4f), statusY));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.MutedText))
            ImGui.TextUnformatted(footer.BotStatusText);
        theme.ApplyFontScale();

        float rowY = max.Y + theme.Gap(0.6f);
        float buttonWidth = (width - theme.Gap() * 2) / 3f;
        float step = buttonWidth + theme.Gap();

        DrawLinkButton("##nav-discord", FontAwesomeIcon.Comments, "Open Discord", new Vector2(min.X, rowY), buttonWidth, footer.OnDiscordClicked);
        DrawLinkButton("##nav-github", FontAwesomeIcon.CodeBranch, "Open GitHub", new Vector2(min.X + step, rowY), buttonWidth, footer.OnGitHubClicked);
        DrawLinkButton("##nav-docs", FontAwesomeIcon.FileAlt, "Documentation", new Vector2(min.X + step * 2, rowY), buttonWidth, footer.OnDocsClicked);
    }

    private void DrawLinkButton(string id, FontAwesomeIcon icon, string tooltip, Vector2 min, float width, Action? onClick)
    {
        var draw = ImGui.GetWindowDrawList();
        float height = theme.Scaled(30f);

        ImGui.SetCursorScreenPos(min);
        bool clicked = ImGui.InvisibleButton(id, new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            theme.Tooltip(tooltip);
        }
        if (clicked)
            onClick?.Invoke();

        var max = min + new Vector2(width, height);
        draw.AddRectFilled(min, max, ImGui.GetColorU32(hovered ? theme.RowHover : theme.CardBg), theme.Radius(0.8f));

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, hovered ? theme.Text : theme.MutedText))
        {
            var glyph = icon.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(min + new Vector2((width - size.X) * 0.5f, (height - size.Y) * 0.5f));
            ImGui.TextUnformatted(glyph);
        }
    }
}

public sealed class SidebarFooterState
{
    public bool BotOnline { get; init; }
    public string BotStatusText { get; init; } = "Disconnected";
    public Action? OnBotCardClicked { get; init; }
    public Action? OnDiscordClicked { get; init; }
    public Action? OnGitHubClicked { get; init; }
    public Action? OnDocsClicked { get; init; }
}
