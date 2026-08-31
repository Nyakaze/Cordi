using System;
using System.Numerics;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Components;

public sealed class PageHeader
{
    private readonly UiTheme theme;

    public PageHeader(UiTheme theme)
    {
        this.theme = theme;
    }

    public void Draw(string title, string subtitle, Action<float>? drawInside = null)
    {
        var draw = ImGui.GetWindowDrawList();
        float width = ImGui.GetContentRegionAvail().X;
        var min = ImGui.GetCursorScreenPos();
        float padX = theme.PadX(1.6f);
        float padY = theme.PadY(1.4f);

        draw.ChannelsSplit(2);
        draw.ChannelsSetCurrent(1);

        ImGui.SetCursorScreenPos(new Vector2(min.X + padX, min.Y + padY));

        using (ImRaii.Group())
        {
            theme.ApplyFontScale(1.45f);
            ImGui.TextUnformatted(title);
            theme.ApplyFontScale();

            if (!string.IsNullOrEmpty(subtitle))
            {
                using (ImRaii.PushColor(ImGuiCol.Text, theme.MutedText))
                    ImGui.TextUnformatted(subtitle);
            }

            if (drawInside != null)
            {
                theme.SpacerY(1.1f);
                drawInside(width - padX * 2f);
            }
        }

        var max = new Vector2(min.X + width, ImGui.GetItemRectMax().Y + padY);

        draw.ChannelsSetCurrent(0);
        draw.AddRectFilled(min, max, ImGui.GetColorU32(theme.PanelBg), theme.Radius(1.4f));
        DrawGlow(draw, min, max);
        draw.AddRect(min, max, ImGui.GetColorU32(theme.Border), theme.Radius(1.4f));
        draw.ChannelsMerge();

        ImGui.SetCursorScreenPos(new Vector2(min.X, max.Y));
        ImGui.Dummy(new Vector2(width, theme.Gap(1.2f)));
    }

    private void DrawGlow(ImDrawListPtr draw, Vector2 min, Vector2 max)
    {
        var center = new Vector2(max.X - (max.X - min.X) * 0.16f, min.Y - theme.Scaled(44f));
        float baseRadius = theme.Scaled(112f);

        draw.PushClipRect(min, max, true);
        for (int i = 6; i >= 1; i--)
        {
            float t = i / 6f;
            float radius = baseRadius * t;
            var color = new Vector4(theme.Accent.X, theme.Accent.Y, theme.Accent.Z, 0.055f * (1f - t + 0.35f));
            draw.AddCircleFilled(center, radius, ImGui.GetColorU32(color), 48);
        }
        draw.PopClipRect();
    }

    public void DrawSectionLabel(string label, Action? drawTrailing = null)
    {
        theme.ApplyFontScale(0.84f);
        using (ImRaii.PushColor(ImGuiCol.Text, theme.FaintText))
            ImGui.TextUnformatted(label.ToUpperInvariant());
        theme.ApplyFontScale();

        if (drawTrailing != null)
        {
            ImGui.SameLine();
            drawTrailing();
        }

        theme.SpacerY(0.6f);
    }

    public bool DrawNavCard(string id, FontAwesomeIcon icon, string title, string subtitle, float width)
    {
        var draw = ImGui.GetWindowDrawList();
        float height = theme.Scaled(72f);
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, height);

        bool clicked = ImGui.InvisibleButton($"##navcard-{id}", new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        if (hovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        float cardRadius = theme.Radius(1.2f);
        float accentWidth = theme.Scaled(3f);
        var accentMin = new Vector2(min.X + accentWidth, min.Y + cardRadius);
        var accentMax = new Vector2(min.X + accentWidth * 2f, max.Y - cardRadius);

        draw.AddRectFilled(min, max, ImGui.GetColorU32(hovered ? theme.RowHover : theme.CardBg), cardRadius);
        draw.AddRectFilled(accentMin, accentMax, ImGui.GetColorU32(theme.Accent), accentWidth * 0.5f);
        draw.AddRect(min, max, ImGui.GetColorU32(theme.Border), cardRadius);

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.Accent))
        {
            var glyph = icon.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(new Vector2(min.X + theme.PadX(1.4f), min.Y + (height - size.Y) * 0.5f));
            ImGui.TextUnformatted(glyph);
        }

        float textX = min.X + theme.PadX(1.4f) + theme.Scaled(28f);
        ImGui.SetCursorScreenPos(new Vector2(textX, min.Y + theme.Scaled(18f)));
        ImGui.TextUnformatted(title);

        theme.ApplyFontScale(0.86f);
        ImGui.SetCursorScreenPos(new Vector2(textX, min.Y + theme.Scaled(40f)));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.MutedText))
            ImGui.TextUnformatted(subtitle);
        theme.ApplyFontScale();

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, hovered ? theme.Text : theme.FaintText))
        {
            var glyph = FontAwesomeIcon.ChevronRight.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(new Vector2(max.X - theme.PadX(1.2f) - size.X, min.Y + (height - size.Y) * 0.5f));
            ImGui.TextUnformatted(glyph);
        }

        ImGui.SetCursorScreenPos(new Vector2(min.X, max.Y + theme.Gap()));
        return clicked;
    }
}
