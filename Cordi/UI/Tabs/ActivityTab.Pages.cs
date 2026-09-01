using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.UI.Themes;
using Crovus.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ActivityTab
{
    private void DrawPlayingPage()
    {
        if (pendingScrollTop)
        {
            pendingScrollTop = false;
            ImGui.SetScrollY(0f);
        }

        var conf = ConfigFor(ActivityType.Playing, "Playing {name}");

        if (editingGame is { } game)
        {
            if (Config.GameConfigs.TryGetValue(game, out var gameConf) && gameConf is not null)
            {
                DrawGameOverrideEditor(game, gameConf);
                return;
            }

            editingGame = null;
        }

        DrawTypePage(
            "activity-playing",
            ActivityType.Playing,
            "Playing",
            "Titles built from the game or app your Discord account is playing",
            conf,
            requiresUserId: true,
            drawExtra: DrawGameOverridesCard);
    }

    private void DrawListeningPage()
    {
        var conf = ConfigFor(ActivityType.ListeningTo, "♪ {details} - {state}");

        DrawTypePage(
            "activity-listening",
            ActivityType.ListeningTo,
            "Listening",
            "Titles built from the track your Discord account is listening to",
            conf,
            requiresUserId: true);
    }

    private void DrawWatchingPage()
    {
        var conf = ConfigFor(ActivityType.Watching, "Watching {details}");

        DrawTypePage(
            "activity-watching",
            ActivityType.Watching,
            "Watching",
            "Titles built from what your Discord account is watching",
            conf,
            requiresUserId: true);
    }

    private void DrawGameOverridesCard()
    {
        Card.Draw(
            "activity-game-overrides",
            innerWidth =>
            {
                theme.PushInputScope();

                var games = Config.GameConfigs.Keys.ToList();

                if (games.Count == 0)
                    ImGui.TextColored(theme.FaintText, "No overrides yet, every game uses the format above.");

                string? removeGame = null;

                foreach (var game in games)
                {
                    if (DrawGameOverrideRow(game, Config.GameConfigs[game], innerWidth))
                        removeGame = game;
                }

                if (removeGame != null)
                {
                    Config.GameConfigs.Remove(removeGame);

                    if (editingGame == removeGame)
                        editingGame = null;

                    Save();
                }

                theme.SpacerY(0.5f);
                DrawGameOverrideAddRow(innerWidth);

                theme.PopInputScope();
            },
            label: "Game Overrides",
            drawTrailing: anchor => DrawCountChip(anchor, Config.GameConfigs.Count, "override"));
    }

    private bool DrawGameOverrideRow(string game, ActivityTypeConfig conf, float rowWidth)
    {
        bool remove = false;
        bool toggled = false;

        var result = Row.Draw(
            id: $"activity-game-{game}",
            icon: FontAwesomeIcon.Gamepad,
            iconColor: conf.Enabled ? UiTheme.TileBlue : theme.MutedText,
            title: game,
            subtitle: string.IsNullOrWhiteSpace(conf.Format) ? "No format set" : conf.Format,
            controlWidth: UiTheme.ActionButtonSize * 2f + 8f,
            drawControl: (pos, width) =>
            {
                float actionWidth = theme.Scaled(UiTheme.ActionButtonSize);
                float height = theme.Scaled(UiTheme.ControlHeight);

                if (theme.ToggleAction(
                        $"activity-game-toggle-{game}",
                        pos,
                        conf.Enabled,
                        conf.Enabled ? "Override is active" : "Override is ignored",
                        actionWidth,
                        height))
                {
                    conf.Enabled = !conf.Enabled;
                    Save();
                    toggled = true;
                }

                if (theme.DeleteAction(
                        $"activity-game-del-{game}",
                        new Vector2(pos.X + width - actionWidth, pos.Y),
                        "Remove override",
                        actionWidth,
                        height))
                    remove = true;
            },
            rowWidth: rowWidth);

        if (result.RowClicked && !toggled && !remove)
        {
            editingGame = game;
            pendingScrollTop = true;
        }

        return remove;
    }

    private void DrawGameOverrideAddRow(float innerWidth)
    {
        var origin = ImGui.GetCursorScreenPos();
        float buttonWidth = theme.Scaled(90f);
        float height = theme.Scaled(UiTheme.ControlHeight);
        float gap = theme.Gap();

        theme.TextInput("##activity-game-new", origin, innerWidth - buttonWidth - gap, ref newGameName, 64, "Game name as Discord reports it");

        ImGui.SetCursorScreenPos(new Vector2(origin.X + innerWidth - buttonWidth, origin.Y));
        if (theme.PrimaryButton("Add##activity-game-add", new Vector2(buttonWidth, height)))
        {
            string name = newGameName.Trim();

            if (name.Length > 0 && !Config.GameConfigs.ContainsKey(name))
            {
                Config.GameConfigs[name] = new ActivityTypeConfig
                {
                    Enabled = true,
                    Priority = 0,
                    Format = "Playing {name}",
                };

                newGameName = string.Empty;
                Save();
            }
        }
        theme.HoverHandIfItem();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(innerWidth, height));
    }

    private void DrawGameOverrideEditor(string game, ActivityTypeConfig conf)
    {
        string id = $"activity-game-{game}";

        Layout.Draw($"Override: {game}", "Format used while this game is the reported activity", innerWidth =>
        {
            var back = Row.Draw(
                id: $"{id}-back",
                icon: FontAwesomeIcon.ArrowLeft,
                iconColor: theme.Accent,
                title: "Back to Playing",
                subtitle: "Return to the general Playing settings",
                rowWidth: innerWidth);

            if (back.RowClicked)
                editingGame = null;
        });

        Card.Draw(
            $"{id}-state",
            innerWidth => DrawToggleRow(
                $"{id}-enabled",
                FontAwesomeIcon.PowerOff,
                conf.Enabled ? UiTheme.TileGreen : theme.MutedText,
                "Use this override",
                conf.Enabled ? "Replaces the general Playing format for this game" : "Ignored, the general format is used",
                innerWidth,
                () => conf.Enabled,
                value => conf.Enabled = value),
            label: "Override");

        DrawFormatCard(id, ActivityType.Playing, conf);
        DrawAppearanceCard(id, conf);
        DrawFiltersCard(id, ActivityType.Playing, conf);
    }
}
