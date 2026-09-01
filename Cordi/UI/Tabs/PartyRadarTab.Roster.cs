using System.Numerics;
using Cordi.Services.Features;
using Cordi.UI.Themes;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class PartyRadarTab
{
    private void DrawHero()
    {
        Layout.Draw(
            "Party Radar",
            "Tracks the party you are in and remembers who you played with",
            innerWidth => DrawToggleRow(
                "party-master",
                FontAwesomeIcon.Crosshairs,
                Party.Enabled ? UiTheme.TileGreen : theme.MutedText,
                "Track my party",
                Party.Enabled
                    ? "Party changes are watched and announced in Discord"
                    : "Nothing is tracked, the roster below stays empty",
                innerWidth,
                () => Party.Enabled,
                value => Party.Enabled = value));
    }

    private void DrawRosterCard()
    {
        var members = plugin.PartyService.PartyMembers;

        Card.Draw(
            "party-roster",
            innerWidth =>
            {
                if (!Party.Enabled)
                {
                    Row.Draw(
                        id: "party-roster-off",
                        icon: FontAwesomeIcon.PowerOff,
                        iconColor: theme.MutedText,
                        title: "Tracking is off",
                        subtitle: "Turn on party tracking above to see your party here",
                        rowWidth: innerWidth);

                    return;
                }

                if (members.Count == 0)
                {
                    Row.Draw(
                        id: "party-roster-empty",
                        icon: FontAwesomeIcon.UserSlash,
                        iconColor: theme.MutedText,
                        title: "Not in a party",
                        subtitle: "Members show up here as soon as you join one",
                        rowWidth: innerWidth);

                    return;
                }

                for (int index = 0; index < members.Count; index++)
                    DrawMemberRow(members[index], innerWidth);
            },
            label: "Current Party",
            drawTrailing: anchor => DrawCountChip(
                anchor,
                $"{members.Count}/8",
                members.Count >= 8 ? UiTheme.TileGreen : theme.MutedText));
    }

    private void DrawMemberRow(PartyService.PartyMemberInfo member, float innerWidth)
    {
        string key = $"member-{member.Name}@{member.World}";
        string note = NoteFor(member.Name, member.World);
        bool hasNote = !string.IsNullOrWhiteSpace(note);

        string subtitle = hasNote
            ? $"{member.World}  -  {JobAbbreviation(member.JobId)}  -  {note}"
            : $"{member.World}  -  {JobAbbreviation(member.JobId)}  -  no note yet";

        var result = Row.Draw(
            id: key,
            icon: FontAwesomeIcon.User,
            iconColor: hasNote ? UiTheme.TileTeal : UiTheme.TileBlue,
            title: member.Name,
            subtitle: subtitle,
            showChevron: true,
            rowWidth: innerWidth,
            drawTitleBadge: (pos, lineHeight) => DrawItemLevelBadge(member, pos, lineHeight));

        if (result.RowClicked || result.ChevronClicked)
            OpenNoteEditor(key, note);

        DrawNoteEditor(key, member.Name, member.World, innerWidth);
    }

    private void DrawItemLevelBadge(PartyService.PartyMemberInfo member, Vector2 pos, float lineHeight)
    {
        if (!Party.ShowGearLevel || member.ItemLevel is not { } level || level <= 0)
            return;

        string text = $"i{level}";
        var size = theme.ChipSize(text);
        theme.ChipAt(new Vector2(pos.X, pos.Y + (lineHeight - size.Y) * 0.5f), text, UiTheme.TileAmber);
    }
}
