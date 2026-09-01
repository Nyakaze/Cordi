using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class SlashCommandsTab
{
    private static readonly (SlashCommandParameterType Type, string Label)[] ParameterTypes =
    {
        (SlashCommandParameterType.Text, "Text"),
        (SlashCommandParameterType.Integer, "Whole number"),
        (SlashCommandParameterType.Decimal, "Decimal"),
        (SlashCommandParameterType.Boolean, "Yes / No"),
        (SlashCommandParameterType.User, "User"),
        (SlashCommandParameterType.Channel, "Channel"),
        (SlashCommandParameterType.Role, "Role"),
        (SlashCommandParameterType.Mentionable, "User or role"),
    };

    private void DrawEditorPage()
    {
        if (editState is not { } command)
            return;

        string title = isAddingNew
            ? "New command"
            : $"/{(command.Name.Length > 0 ? command.Name : "command")}";

        Layout.Draw(title, "Maps a Discord slash command onto an in-game command", innerWidth =>
        {
            var back = Row.Draw(
                id: "slash-editor-back",
                icon: FontAwesomeIcon.ArrowLeft,
                iconColor: theme.Accent,
                title: "Back to Slash Commands",
                subtitle: "Discards anything you have not saved",
                rowWidth: innerWidth);

            if (back.RowClicked)
                CancelEdit();
        });

        DrawEditorFieldsCard(command);
        DrawParametersPanel(command);
        DrawEditorActions();
    }

    private void DrawEditorFieldsCard(CustomSlashCommand command)
    {
        Card.Draw(
            "slash-editor-fields",
            innerWidth =>
            {
                DrawToggleRow(
                    "slash-editor-enabled",
                    FontAwesomeIcon.PowerOff,
                    command.IsEnabled ? UiTheme.TileGreen : theme.MutedText,
                    "Register this command",
                    command.IsEnabled
                        ? "Discord users can run it once you save"
                        : "Kept here but not registered with Discord",
                    innerWidth,
                    () => command.IsEnabled,
                    value => command.IsEnabled = value);

                DrawFieldRow(
                    "slash-editor-name",
                    FontAwesomeIcon.Terminal,
                    "Command name",
                    "Lowercase letters, digits, hyphens and underscores",
                    innerWidth,
                    command.Name,
                    32,
                    "roll",
                    value => command.Name = SanitizeCommandName(value));

                DrawFieldRow(
                    "slash-editor-description",
                    FontAwesomeIcon.AlignLeft,
                    "Description",
                    "Shown next to the command in Discord",
                    innerWidth,
                    command.Description,
                    100,
                    "Rolls a die in game",
                    value => command.Description = value);

                DrawFieldRow(
                    "slash-editor-game",
                    FontAwesomeIcon.Gamepad,
                    "Game command",
                    "Use {name} to drop a parameter into the command",
                    innerWidth,
                    command.GameCommand,
                    256,
                    "/random {max}",
                    value => command.GameCommand = value);

                Row.Draw(
                    id: "slash-editor-group",
                    icon: FontAwesomeIcon.FolderOpen,
                    iconColor: string.IsNullOrEmpty(command.Group) ? theme.MutedText : UiTheme.TileBlue,
                    title: "Group",
                    subtitle: "Groups can be enabled or disabled all at once",
                    controlWidth: 200f,
                    drawControl: (_, width) => theme.OptionPicker(
                        "slash-editor-group-picker",
                        NormalizeGroup(command.Group),
                        GroupOptions(),
                        value => command.Group = value,
                        width),
                    rowWidth: innerWidth);
            },
            label: "Command");
    }

    private IReadOnlyList<DropdownItem> GroupOptions()
    {
        var items = new List<DropdownItem> { new() { Key = string.Empty, Label = "No group" } };

        foreach (var group in Config.Groups)
        {
            string name = NormalizeGroup(group.Name);

            if (name.Length > 0 && !items.Any(i => string.Equals(i.Key, name, StringComparison.OrdinalIgnoreCase)))
                items.Add(new DropdownItem { Key = name, Label = name });
        }

        return items;
    }

    private void DrawFieldRow(
        string id,
        FontAwesomeIcon icon,
        string title,
        string subtitle,
        float innerWidth,
        string current,
        int maxLength,
        string hint,
        Action<string> set)
    {
        Row.Draw(
            id: id,
            icon: icon,
            iconColor: string.IsNullOrWhiteSpace(current) ? theme.MutedText : theme.Accent,
            title: title,
            subtitle: subtitle,
            controlWidth: 280f,
            drawControl: (pos, width) =>
            {
                string value = current;

                theme.PushInputScope();
                if (theme.TextInput($"##{id}-input", pos, width, ref value, maxLength, hint))
                {
                    set(value);
                    validationError = string.Empty;
                }
                theme.PopInputScope();
            },
            rowWidth: innerWidth);
    }

    private void DrawParametersPanel(CustomSlashCommand command)
    {
        var typeOptions = ParameterTypes
            .Select(entry => new DropdownItem { Key = entry.Type.ToString(), Label = entry.Label })
            .ToList();

        var requiredOptions = new List<DropdownItem>
        {
            new() { Key = "optional", Label = "Optional" },
            new() { Key = "required", Label = "Required" },
        };

        var columns = new List<ListColumn<SlashCommandParameter>>
        {
            new()
            {
                Id = "name",
                Header = "Name",
                Hint = "name",
                Weight = 1.1f,
                MaxLength = 32,
                GetText = p => p.Name,
                SetText = (p, value) => p.Name = SanitizeCommandName(value),
            },
            new()
            {
                Id = "description",
                Header = "Description",
                Hint = "Shown in Discord",
                Weight = 1.6f,
                MaxLength = 100,
                GetText = p => p.Description,
                SetText = (p, value) => p.Description = value,
            },
            new()
            {
                Id = "type",
                Kind = ListColumnKind.Option,
                Header = "Type",
                FixedWidth = 140f,
                Options = typeOptions,
                GetText = p => p.Type.ToString(),
                SetText = (p, value) => p.Type = Enum.TryParse<SlashCommandParameterType>(value, out var parsed)
                    ? parsed
                    : SlashCommandParameterType.Text,
            },
            new()
            {
                Id = "required",
                Kind = ListColumnKind.Option,
                Header = "Needed",
                FixedWidth = 120f,
                Options = requiredOptions,
                GetText = p => p.Required ? "required" : "optional",
                SetText = (p, value) => p.Required = value == "required",
            },
        };

        List.Draw(
            "slash-editor-parameters",
            "Parameters",
            "Each parameter becomes an option in Discord and replaces its {name} placeholder",
            command.Parameters,
            columns,
            () => new SlashCommandParameter(),
            () => validationError = string.Empty,
            addLabel: "Add Parameter",
            emptyText: "No parameters, the game command runs exactly as written.",
            removeTooltip: "Remove this parameter");
    }

    private void DrawEditorActions()
    {
        Card.Draw(
            "slash-editor-actions",
            innerWidth =>
            {
                if (validationError.Length > 0)
                {
                    Row.Draw(
                        id: "slash-editor-error",
                        icon: FontAwesomeIcon.ExclamationCircle,
                        iconColor: UiTheme.TileRed,
                        title: "This command cannot be saved yet",
                        subtitle: validationError,
                        rowWidth: innerWidth);
                }

                var origin = ImGui.GetCursorScreenPos();
                float height = theme.Scaled(UiTheme.ControlHeight);
                float gap = theme.Gap();
                float buttonWidth = (innerWidth - gap) * 0.5f;

                if (theme.PrimaryButton("Save Command##slash-editor-save", new Vector2(buttonWidth, height)))
                    SaveCommand();
                theme.HoverHandIfItem();

                ImGui.SetCursorScreenPos(new Vector2(origin.X + buttonWidth + gap, origin.Y));

                if (theme.SecondaryButton("Cancel##slash-editor-cancel", new Vector2(buttonWidth, height)))
                    CancelEdit();
                theme.HoverHandIfItem();

                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(innerWidth, height));
            },
            label: isAddingNew ? "Create" : "Save Changes");
    }
}
