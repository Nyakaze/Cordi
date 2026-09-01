using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Services.Discord;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Tabs;

public partial class SlashCommandsTab
{
    private static readonly (string Usage, string Description, FontAwesomeIcon Icon)[] ManageSubCommands =
    {
        ("/cordi command list", "Lists your commands and whether each one is on", FontAwesomeIcon.ListUl),
        ("/cordi command enable <name>", "Turns a single command back on", FontAwesomeIcon.ToggleOn),
        ("/cordi command disable <name>", "Turns a single command off", FontAwesomeIcon.ToggleOff),
        ("/cordi cmdgroup list", "Lists your groups and how many commands each one holds", FontAwesomeIcon.Folder),
        ("/cordi cmdgroup enable <name>", "Turns every command in a group on", FontAwesomeIcon.FolderPlus),
        ("/cordi cmdgroup disable <name>", "Turns every command in a group off", FontAwesomeIcon.FolderMinus),
        ("/cordi screenshot", "Sends a screenshot of your game to Discord", FontAwesomeIcon.Camera),
    };

    private void DrawHero()
    {
        Layout.Draw(
            "Slash Commands",
            "Run in-game commands straight from Discord",
            innerWidth => DrawToggleRow(
                "slash-master",
                FontAwesomeIcon.Code,
                Config.Enabled ? UiTheme.TileGreen : theme.MutedText,
                "Expose Cordi commands in Discord",
                Config.Enabled
                    ? "Commands are registered with your server and can be used there"
                    : "Nothing is registered, existing commands stay untouched",
                innerWidth,
                () => Config.Enabled,
                value => Config.Enabled = value));
    }

    private void DrawDiscordCard()
    {
        int enabled = EnabledCount;
        int limit = DiscordSlashCommandService.MaxUserCommands;
        bool ready = Config.Enabled && SlashService is not null;

        Card.Draw(
            "slash-discord",
            innerWidth =>
            {
                Row.Draw(
                    id: "slash-experimental",
                    icon: FontAwesomeIcon.ExclamationTriangle,
                    iconColor: UiTheme.TileAmber,
                    title: "This feature is experimental",
                    subtitle: "The bot needs the applications.commands scope in your server",
                    rowWidth: innerWidth);

                Row.Draw(
                    id: "slash-channel",
                    icon: FontAwesomeIcon.Hashtag,
                    iconColor: string.IsNullOrEmpty(Config.CommandChannelId) ? theme.MutedText : theme.Accent,
                    title: "Restrict to a channel",
                    subtitle: string.IsNullOrEmpty(Config.CommandChannelId)
                        ? "Your commands work in every channel"
                        : "Your commands only answer in this channel",
                    controlWidth: 240f,
                    drawControl: (_, width) => theme.ChannelPicker(
                        "slash-channel-picker",
                        Config.CommandChannelId,
                        plugin.Channels.TextChannels,
                        newId =>
                        {
                            Config.CommandChannelId = newId;
                            Save();
                        },
                        defaultLabel: "Any Channel",
                        showLabel: false,
                        width: width),
                    rowWidth: innerWidth);

                Row.Draw(
                    id: "slash-sync",
                    icon: FontAwesomeIcon.SyncAlt,
                    iconColor: ready ? UiTheme.TileBlue : theme.MutedText,
                    title: "Sync everything with Discord",
                    subtitle: ready
                        ? "Pushes the full command set at once, useful after bulk edits"
                        : "Turn slash commands on first",
                    controlWidth: 150f,
                    drawControl: (pos, width) => DrawRowButton(pos, width, "Sync Now", ready, true, SyncAll),
                    rowWidth: innerWidth);

                Row.Draw(
                    id: "slash-unregister",
                    icon: FontAwesomeIcon.TrashAlt,
                    iconColor: ready ? UiTheme.TileRed : theme.MutedText,
                    title: "Remove every command",
                    subtitle: "Clears all Cordi commands from your server, your list here is kept",
                    controlWidth: 150f,
                    drawControl: (pos, width) => DrawRowButton(pos, width, "Unregister", ready, false, UnregisterAll),
                    rowWidth: innerWidth);
            },
            label: "Discord",
            drawTrailing: anchor => DrawCountChip(
                anchor,
                $"{enabled}/{limit}",
                enabled >= limit ? UiTheme.TileRed : enabled >= limit - 5 ? UiTheme.TileAmber : theme.MutedText));
    }

    private void DrawRowButton(Vector2 pos, float width, string label, bool enabled, bool primary, Action onClick)
    {
        float height = theme.Scaled(32f);
        ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + (theme.Scaled(UiTheme.ControlHeight) - height) * 0.5f));

        using var disabled = ImRaii.Disabled(!enabled);

        bool clicked = primary
            ? theme.PrimaryButton($"{label}##slash-btn-{label}", new Vector2(width, height))
            : theme.SecondaryButton($"{label}##slash-btn-{label}", new Vector2(width, height));

        if (clicked)
            onClick();
    }

    private void DrawBuiltInCard()
    {
        int emotes = SlashService?.EmoteCommands.Count ?? 0;

        Card.Draw(
            "slash-builtin",
            innerWidth =>
            {
                var manage = Row.Draw(
                    id: "slash-builtin-cordi",
                    icon: FontAwesomeIcon.Terminal,
                    iconColor: UiTheme.TilePurple,
                    title: $"/{DiscordSlashCommandService.ManageCommandName}",
                    subtitle: showManageCommands
                        ? "Manages Cordi from Discord, no game client needed"
                        : "Manages Cordi from Discord, open to see every subcommand",
                    showChevron: true,
                    rowWidth: innerWidth,
                    drawTitleBadge: (pos, lineHeight) => DrawChipBadge(
                        pos, lineHeight, $"{ManageSubCommands.Length} subcommands", UiTheme.TilePurple));

                if (manage.RowClicked || manage.ChevronClicked)
                    showManageCommands = !showManageCommands;

                if (showManageCommands)
                    DrawManageSubCommands(innerWidth);

                Row.Draw(
                    id: "slash-builtin-emote",
                    icon: FontAwesomeIcon.TheaterMasks,
                    iconColor: UiTheme.TilePink,
                    title: $"/{DiscordSlashCommandService.EmoteCommandName}",
                    subtitle: emotes > 0
                        ? "Performs any emote, start typing and Discord autocompletes the name"
                        : "Performs any emote, the list is built once you are logged in",
                    rowWidth: innerWidth,
                    drawTitleBadge: (pos, lineHeight) =>
                    {
                        if (emotes > 0)
                            DrawChipBadge(pos, lineHeight, $"{emotes} emotes", UiTheme.TilePink);
                    });
            },
            label: "Built In",
            drawTrailing: anchor => DrawCountChip(
                anchor,
                $"{DiscordSlashCommandService.ReservedSlots} reserved"));
    }

    private void DrawChipBadge(Vector2 pos, float lineHeight, string text, Vector4 color)
    {
        var size = theme.ChipSize(text);
        theme.ChipAt(new Vector2(pos.X, pos.Y + (lineHeight - size.Y) * 0.5f), text, color);
    }

    private void DrawManageSubCommands(float innerWidth)
    {
        float indent = theme.Scaled(UiTheme.IconTileSize) + theme.PadX(0.8f);

        ImGui.Indent(indent);

        foreach (var (usage, description, icon) in ManageSubCommands)
        {
            Row.Draw(
                id: $"slash-builtin-cordi-{usage}",
                icon: icon,
                iconColor: theme.MutedText,
                title: usage,
                subtitle: description,
                rowWidth: innerWidth - indent,
                rowHeight: 46f);
        }

        ImGui.Unindent(indent);
    }

    private void DrawCommandsCard()
    {
        var commands = Config.Commands;

        Card.Draw(
            "slash-commands",
            innerWidth =>
            {
                if (commands.Count > 0)
                {
                    DrawSearchRow(innerWidth);
                    theme.SpacerY(0.3f);
                }

                var groups = Config.Groups
                    .Select(g => NormalizeGroup(g.Name))
                    .Where(g => g.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var command in commands)
                {
                    string group = NormalizeGroup(command.Group);

                    if (group.Length > 0 && !groups.Contains(group, StringComparer.OrdinalIgnoreCase))
                        groups.Add(group);
                }

                int? removeIndex = null;
                string? removeGroup = null;
                bool anyVisible = false;

                foreach (var group in groups)
                {
                    if (DrawGroupSection(group, innerWidth, ref removeIndex, ref anyVisible))
                        removeGroup = group;
                }

                DrawGroupSection(string.Empty, innerWidth, ref removeIndex, ref anyVisible);

                if (!anyVisible)
                {
                    ImGui.TextColored(
                        theme.FaintText,
                        commands.Count == 0
                            ? "No commands yet, add one below."
                            : "No command matches that search.");
                }

                if (removeIndex is { } index)
                    RemoveCommand(index);

                if (removeGroup is { } group2)
                    RemoveGroup(group2);

                theme.SpacerY(0.5f);
                DrawAddGroupRow(innerWidth);
                theme.SpacerY(0.3f);

                if (theme.PrimaryButton("+ Add Command##slash-add", new Vector2(innerWidth, theme.Scaled(34f))))
                    BeginAdd();
                theme.HoverHandIfItem();
            },
            label: "Your Commands",
            drawTrailing: anchor => DrawCountChip(
                anchor,
                commands.Count == 1 ? "1 command" : $"{commands.Count} commands"));
    }

    private void DrawSearchRow(float innerWidth)
    {
        var origin = ImGui.GetCursorScreenPos();
        float height = theme.Scaled(UiTheme.ControlHeight);

        theme.PushInputScope();
        theme.TextInput("##slash-search", origin, innerWidth, ref searchFilter, 64, "Search by name, game command or group");
        theme.PopInputScope();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(innerWidth, height + theme.Gap(0.4f)));
    }

    private bool DrawGroupSection(string group, float innerWidth, ref int? removeIndex, ref bool anyVisible)
    {
        var matches = new List<int>();
        var all = Config.Commands;

        for (int index = 0; index < all.Count; index++)
        {
            if (!string.Equals(NormalizeGroup(all[index].Group), group, StringComparison.OrdinalIgnoreCase))
                continue;

            if (Matches(all[index], group))
                matches.Add(index);
        }

        bool named = group.Length > 0;

        if (matches.Count == 0 && (!named || searchFilter.Length > 0))
            return false;

        anyVisible |= matches.Count > 0;

        bool collapsed = collapsedGroups.Contains(group);
        int enabled = matches.Count(index => all[index].IsEnabled);
        bool allEnabled = matches.Count > 0 && enabled == matches.Count;
        bool removeGroup = false;

        float actions = named ? UiTheme.ActionButtonSize * 2f + 6f : UiTheme.ActionButtonSize;

        var header = Row.Draw(
            id: $"slash-group-{group}",
            icon: named ? FontAwesomeIcon.FolderOpen : FontAwesomeIcon.LayerGroup,
            iconColor: named ? UiTheme.TileBlue : theme.MutedText,
            title: named ? group : "Ungrouped",
            subtitle: matches.Count == 0
                ? "No commands in this group yet"
                : $"{matches.Count} command{(matches.Count == 1 ? "" : "s")}, {enabled} enabled",
            controlWidth: actions,
            drawControl: (pos, width) =>
            {
                float size = theme.Scaled(UiTheme.ActionButtonSize);
                float height = theme.Scaled(UiTheme.ControlHeight);
                float x = pos.X + width - size;

                if (named)
                {
                    if (theme.DeleteAction($"slash-group-del-{group}", new Vector2(x, pos.Y),
                            "Delete this group, its commands become ungrouped", size, height))
                        removeGroup = true;

                    x -= size + theme.Gap(0.6f);
                }

                if (theme.ToggleAction($"slash-group-toggle-{group}", new Vector2(x, pos.Y), allEnabled,
                        allEnabled ? "Disable every command in this group" : "Enable every command in this group",
                        size, height))
                    SetGroupEnabled(group, !allEnabled);
            },
            showChevron: matches.Count > 0,
            rowWidth: innerWidth);

        if (matches.Count > 0 && (header.RowClicked || header.ChevronClicked))
        {
            if (!collapsedGroups.Remove(group))
                collapsedGroups.Add(group);

            collapsed = collapsedGroups.Contains(group);
        }

        if (!collapsed)
        {
            foreach (int index in matches)
            {
                if (DrawCommandRow(index, all[index], innerWidth))
                    removeIndex = index;
            }
        }

        return removeGroup;
    }

    private bool Matches(CustomSlashCommand command, string group)
    {
        if (searchFilter.Length == 0)
            return true;

        return command.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase) ||
               command.GameCommand.Contains(searchFilter, StringComparison.OrdinalIgnoreCase) ||
               (command.Description ?? "").Contains(searchFilter, StringComparison.OrdinalIgnoreCase) ||
               group.Contains(searchFilter, StringComparison.OrdinalIgnoreCase);
    }

    private bool DrawCommandRow(int index, CustomSlashCommand command, float innerWidth)
    {
        bool remove = false;
        int parameters = command.Parameters.Count;

        string subtitle = parameters == 0
            ? command.GameCommand
            : $"{command.GameCommand}  -  {parameters} parameter{(parameters == 1 ? "" : "s")}";

        var result = Row.Draw(
            id: $"slash-cmd-{index}",
            icon: FontAwesomeIcon.Terminal,
            iconColor: command.IsEnabled ? UiTheme.TileGreen : theme.MutedText,
            title: $"/{command.Name}",
            subtitle: subtitle,
            controlWidth: UiTheme.ActionButtonSize * 2f + 6f,
            drawControl: (pos, width) =>
            {
                float size = theme.Scaled(UiTheme.ActionButtonSize);
                float height = theme.Scaled(UiTheme.ControlHeight);

                if (theme.ToggleAction($"slash-cmd-toggle-{index}", pos, command.IsEnabled,
                        command.IsEnabled ? "Disable this command" : "Enable this command", size, height))
                    SetCommandEnabled(command, !command.IsEnabled);

                if (theme.DeleteAction($"slash-cmd-del-{index}", new Vector2(pos.X + width - size, pos.Y),
                        "Delete this command", size, height))
                    remove = true;
            },
            showChevron: true,
            rowWidth: innerWidth);

        if ((result.RowClicked || result.ChevronClicked) && !remove)
            BeginEdit(index);

        return remove;
    }

    private void DrawAddGroupRow(float innerWidth)
    {
        var origin = ImGui.GetCursorScreenPos();
        float height = theme.Scaled(UiTheme.ControlHeight);
        float buttonWidth = theme.Scaled(110f);
        float gap = theme.Gap();

        theme.PushInputScope();
        theme.TextInput("##slash-new-group", origin, innerWidth - buttonWidth - gap, ref newGroupName, 32, "New group name");
        theme.PopInputScope();

        string name = newGroupName.Trim();
        bool valid = name.Length > 0 &&
                     !Config.Groups.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));

        ImGui.SetCursorScreenPos(new Vector2(origin.X + innerWidth - buttonWidth, origin.Y));

        using (ImRaii.Disabled(!valid))
        {
            if (theme.SecondaryButton("Add Group##slash-add-group", new Vector2(buttonWidth, height)))
            {
                Config.Groups.Add(new CommandGroup { Name = name });
                Save();
                newGroupName = string.Empty;
            }
        }
        theme.HoverHandIfItem();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(innerWidth, height));
    }

    private void RemoveGroup(string group)
    {
        Config.Groups.RemoveAll(g => string.Equals(NormalizeGroup(g.Name), group, StringComparison.OrdinalIgnoreCase));

        foreach (var command in Config.Commands)
        {
            if (string.Equals(NormalizeGroup(command.Group), group, StringComparison.OrdinalIgnoreCase))
                command.Group = string.Empty;
        }

        collapsedGroups.Remove(group);
        Save();
    }
}
