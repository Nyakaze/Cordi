using System.Numerics;
using Cordi.Configuration;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class PartyRadarTab
{
    private void DrawArchiveCard()
    {
        int total = Memory.RememberedPlayers.Count;

        Card.Draw(
            "party-archive",
            innerWidth =>
            {
                var header = Row.Draw(
                    id: "party-archive-toggle",
                    icon: FontAwesomeIcon.AddressBook,
                    iconColor: showArchive ? theme.Accent : theme.MutedText,
                    title: "Browse remembered players",
                    subtitle: total == 0
                        ? "Nobody saved yet"
                        : showArchive
                            ? "Click a player to edit their note"
                            : $"{total} saved, open to search and edit notes",
                    showChevron: true,
                    rowWidth: innerWidth);

                if (header.RowClicked || header.ChevronClicked)
                {
                    showArchive = !showArchive;

                    if (!showArchive)
                        expandedKey = null;
                }

                if (!showArchive)
                    return;

                theme.SpacerY(0.5f);
                DrawArchiveSearch(innerWidth);

                var players = plugin.RememberMe.SearchPlayers(archiveSearch);

                if (players.Count == 0)
                {
                    ImGui.TextColored(
                        theme.FaintText,
                        total == 0
                            ? "Players you meet are saved here once memory is on."
                            : "No player matches that search.");
                }
                else
                {
                    RememberedPlayerEntry? remove = null;

                    foreach (var player in players)
                    {
                        if (DrawArchiveRow(player, innerWidth))
                            remove = player;
                    }

                    if (remove != null)
                    {
                        plugin.RememberMe.RemovePlayer(remove.Name, remove.World);
                        expandedKey = null;
                    }
                }

                theme.SpacerY(0.5f);
                DrawArchiveAddRow(innerWidth);
            },
            label: "Remembered Players",
            drawTrailing: anchor => DrawCountChip(anchor, total == 1 ? "1 player" : $"{total} players"));
    }

    private void DrawArchiveSearch(float innerWidth)
    {
        var origin = ImGui.GetCursorScreenPos();
        float height = theme.Scaled(UiTheme.ControlHeight);

        theme.PushInputScope();
        theme.TextInput("##party-archive-search", origin, innerWidth, ref archiveSearch, 64, "Search by name, world or note");
        theme.PopInputScope();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(innerWidth, height + theme.Gap(0.4f)));
    }

    private bool DrawArchiveRow(RememberedPlayerEntry player, float innerWidth)
    {
        string key = $"archive-{player.FullName}";
        bool hasNote = !string.IsNullOrWhiteSpace(player.Notes);
        bool remove = false;

        var result = Row.Draw(
            id: key,
            icon: FontAwesomeIcon.UserCircle,
            iconColor: hasNote ? UiTheme.TileTeal : theme.MutedText,
            title: player.FullName,
            subtitle: hasNote
                ? $"{player.GetLastSeenRelative()}  -  {player.Notes}"
                : $"{player.GetLastSeenRelative()}  -  no note yet",
            controlWidth: UiTheme.ActionButtonSize,
            drawControl: (pos, width) =>
            {
                if (theme.DeleteAction(
                        $"party-archive-del-{key}",
                        pos,
                        "Forget this player",
                        width,
                        theme.Scaled(UiTheme.ControlHeight)))
                    remove = true;
            },
            showChevron: true,
            rowWidth: innerWidth);

        if ((result.RowClicked || result.ChevronClicked) && !remove)
            OpenNoteEditor(key, player.Notes);

        DrawNoteEditor(key, player.Name, player.World, innerWidth);

        return remove;
    }

    private void DrawArchiveAddRow(float innerWidth)
    {
        var origin = ImGui.GetCursorScreenPos();
        float height = theme.Scaled(UiTheme.ControlHeight);
        float gap = theme.Gap();
        float buttonWidth = theme.Scaled(80f);
        float available = innerWidth - buttonWidth - gap * 3f;
        float nameWidth = available * 0.32f;
        float worldWidth = available * 0.22f;
        float noteWidth = available - nameWidth - worldWidth;

        theme.PushInputScope();

        theme.TextInput("##party-archive-name", origin, nameWidth, ref newPlayerName, 64, "Character name");

        theme.TextInput(
            "##party-archive-world",
            new Vector2(origin.X + nameWidth + gap, origin.Y),
            worldWidth,
            ref newPlayerWorld,
            64,
            "World");

        theme.TextInput(
            "##party-archive-note",
            new Vector2(origin.X + nameWidth + worldWidth + gap * 2f, origin.Y),
            noteWidth,
            ref newPlayerNote,
            512,
            "Note");

        ImGui.SetCursorScreenPos(new Vector2(origin.X + innerWidth - buttonWidth, origin.Y));

        if (theme.PrimaryButton("Add##party-archive-add", new Vector2(buttonWidth, height)))
            AddRememberedPlayer();
        theme.HoverHandIfItem();

        theme.PopInputScope();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(innerWidth, height));
    }

    private void AddRememberedPlayer()
    {
        if (!TryParsePlayer(newPlayerName, out var name, out var world))
            return;

        if (world.Length == 0)
            world = newPlayerWorld.Trim();

        if (world.Length == 0)
            return;

        plugin.RememberMe.AddOrUpdatePlayer(name, world, null, newPlayerNote.Trim());

        newPlayerName = string.Empty;
        newPlayerWorld = string.Empty;
        newPlayerNote = string.Empty;
    }
}
