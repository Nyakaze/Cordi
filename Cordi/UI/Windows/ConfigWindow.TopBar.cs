using System.Collections.Generic;
using System.Numerics;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Windows;

public sealed partial class ConfigWindow
{
    private void DrawTopBar()
    {
        float height = theme.Scaled(UiTheme.StatChipHeight) + theme.Gap(1.5f);

        using var bar = ImRaii.Child("##cordi-topbar", new Vector2(0, height), false);
        if (!bar)
            return;

        var origin = ImGui.GetCursorScreenPos();
        float availWidth = ImGui.GetContentRegionAvail().X;
        float chipTop = origin.Y;

        var chips = BuildStatChips();
        float chipsWidth = 0f;

        if (chips.Count > 0)
            chipsWidth = statChips.Draw(chips, new Vector2(origin.X + availWidth, chipTop));

        float searchWidth = availWidth - chipsWidth - theme.Gap(2f);
        float maxSearchWidth = theme.Scaled(320f);
        if (searchWidth > maxSearchWidth)
            searchWidth = maxSearchWidth;

        if (searchWidth > theme.Scaled(120f))
        {
            ImGui.SetCursorScreenPos(new Vector2(origin.X, chipTop + theme.Scaled(10f)));
            searchBox.Draw(searchWidth);
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, chipTop + theme.Scaled(UiTheme.StatChipHeight) + theme.Gap(0.6f)));
    }

    private IReadOnlyList<StatChip> BuildStatChips()
    {
        var stats = plugin.Config.Stats;
        var chips = new List<StatChip>();

        if (plugin.Config.Chat.Mappings.Count > 0)
        {
            chips.Add(new StatChip
            {
                Label = "Messages",
                Value = stats.TotalMessages.ToString("N0"),
                Icon = FontAwesomeIcon.CommentDots,
                Tooltip = "Total messages processed",
            });
        }

        if (plugin.Config.CordiPeep.Enabled)
        {
            chips.Add(new StatChip
            {
                Label = "Peers",
                Value = stats.TotalPeepsTracked.ToString("N0"),
                Icon = FontAwesomeIcon.UserFriends,
                Tooltip = "Total players tracked",
            });
        }

        if (plugin.Config.EmoteLog.Enabled)
        {
            chips.Add(new StatChip
            {
                Label = "Emotes",
                Value = stats.TotalEmotesTracked.ToString("N0"),
                Icon = FontAwesomeIcon.SmileBeam,
                Tooltip = "Total emotes tracked",
            });
        }

        return chips;
    }
}
