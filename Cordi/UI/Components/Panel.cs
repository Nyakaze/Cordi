using System;
using System.Numerics;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Components;

public sealed class Panel
{
    private readonly UiTheme theme;

    public Panel(UiTheme theme)
    {
        this.theme = theme;
    }

    public void Draw(string id, Action<float> drawContent, string? label = null, Action<Vector2>? drawTrailing = null)
    {
        var draw = ImGui.GetWindowDrawList();
        float width = ImGui.GetContentRegionAvail().X;
        var min = ImGui.GetCursorScreenPos();
        float padX = theme.PadX(1.1f);
        float padY = theme.PadY(1.1f);

        draw.ChannelsSplit(2);
        draw.ChannelsSetCurrent(1);

        ImGui.SetCursorScreenPos(new Vector2(min.X + padX, min.Y + padY));

        using (ImRaii.Group())
        {
            if (!string.IsNullOrEmpty(label))
            {
                var labelPos = ImGui.GetCursorScreenPos();

                theme.ApplyFontScale(0.84f);
                using (ImRaii.PushColor(ImGuiCol.Text, theme.FaintText))
                    ImGui.TextUnformatted(label.ToUpperInvariant());
                theme.ApplyFontScale();

                float headerBottom = ImGui.GetItemRectMax().Y;

                if (drawTrailing != null)
                {
                    drawTrailing(new Vector2(min.X + width - padX, labelPos.Y));
                    headerBottom = MathF.Max(headerBottom, ImGui.GetItemRectMax().Y);
                }

                ImGui.SetCursorScreenPos(new Vector2(min.X + padX, headerBottom + theme.Gap(0.9f)));
            }

            drawContent(width - padX * 2f);
        }

        var max = new Vector2(min.X + width, ImGui.GetItemRectMax().Y + padY);

        draw.ChannelsSetCurrent(0);
        draw.AddRectFilled(min, max, ImGui.GetColorU32(theme.PanelBg), theme.Radius(1.4f));
        draw.AddRect(min, max, ImGui.GetColorU32(theme.Border), theme.Radius(1.4f));
        draw.ChannelsMerge();

        ImGui.SetCursorScreenPos(new Vector2(min.X, max.Y));
        ImGui.Dummy(new Vector2(width, theme.Gap(1.2f)));
    }
}
