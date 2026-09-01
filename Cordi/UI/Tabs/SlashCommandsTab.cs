using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Services;
using Cordi.Services.Discord;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class SlashCommandsTab : ConfigTabBase
{
    private static readonly Regex PlaceholderPattern = new(@"\{([^{}]+)\}", RegexOptions.Compiled);

    private SettingsRow? rowRenderer;
    private PageHeader? layoutRenderer;
    private Panel? panelRenderer;
    private ListPanel? listRenderer;

    private SettingsRow Row => rowRenderer ??= new SettingsRow(theme);
    private PageHeader Layout => layoutRenderer ??= new PageHeader(theme);
    private Panel Card => panelRenderer ??= new Panel(theme);
    private ListPanel List => listRenderer ??= new ListPanel(theme);

    private CustomSlashCommand? editState;
    private int editingIndex = -1;
    private bool isAddingNew;
    private string validationError = string.Empty;

    private bool showManageCommands;
    private string searchFilter = string.Empty;
    private string newGroupName = string.Empty;
    private readonly HashSet<string> collapsedGroups = new(StringComparer.OrdinalIgnoreCase);
    private bool pendingScrollTop;

    public override string Label => "Slash Commands";

    public SlashCommandsTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
    }

    private SlashCommandConfig Config => plugin.Config.SlashCommands;

    private DiscordSlashCommandService? SlashService => plugin.SlashCommandService;

    private int EnabledCount => Config.Commands.Count(c => c.IsEnabled);

    private void Save() => plugin.Config.Save();

    public override void Draw()
    {
        if (pendingScrollTop)
        {
            pendingScrollTop = false;
            ImGui.SetScrollY(0f);
        }

        if (editState is not null)
        {
            DrawEditorPage();
            return;
        }

        DrawHero();
        DrawDiscordCard();
        DrawBuiltInCard();
        DrawCommandsCard();
    }

    private void DrawToggleRow(
        string id,
        FontAwesomeIcon icon,
        Vector4 iconColor,
        string title,
        string subtitle,
        float rowWidth,
        Func<bool> get,
        Action<bool> set)
    {
        bool value = get();

        var result = Row.Draw(
            id: id,
            icon: icon,
            iconColor: iconColor,
            title: title,
            subtitle: subtitle,
            toggleValue: value,
            onToggle: newValue =>
            {
                set(newValue);
                Save();
            },
            rowWidth: rowWidth);

        if (result.RowClicked && !result.ToggleChanged)
        {
            set(!value);
            Save();
        }
    }

    private void DrawCountChip(Vector2 rightAnchor, string text, Vector4? color = null)
    {
        var size = theme.ChipSize(text);
        theme.ChipAt(new Vector2(rightAnchor.X - size.X, rightAnchor.Y), text, color);
    }

    private void BeginAdd()
    {
        editState = new CustomSlashCommand();
        editingIndex = -1;
        isAddingNew = true;
        validationError = string.Empty;
        pendingScrollTop = true;
    }

    private void BeginEdit(int index)
    {
        if (index < 0 || index >= Config.Commands.Count)
            return;

        editState = CloneCommand(Config.Commands[index]);
        editingIndex = index;
        isAddingNew = false;
        validationError = string.Empty;
        pendingScrollTop = true;
    }

    private void CancelEdit()
    {
        editState = null;
        editingIndex = -1;
        isAddingNew = false;
        validationError = string.Empty;
        pendingScrollTop = true;
    }

    private void SaveCommand()
    {
        if (editState is null)
            return;

        validationError = ValidateCommand(editState) ?? string.Empty;

        if (validationError.Length > 0)
            return;

        var commands = Config.Commands;
        string? previousName = null;

        if (isAddingNew)
        {
            commands.Add(editState);
        }
        else if (editingIndex >= 0 && editingIndex < commands.Count)
        {
            previousName = commands[editingIndex].Name;
            commands[editingIndex] = editState;
        }
        else
        {
            CancelEdit();
            return;
        }

        Save();

        var saved = editState;

        if (previousName is { Length: > 0 } &&
            !string.Equals(previousName, saved.Name, StringComparison.OrdinalIgnoreCase))
            PushRemoval(previousName);

        if (saved.IsEnabled)
            PushCommand(saved);
        else
            PushRemoval(saved.Name);

        CancelEdit();
    }

    private void RemoveCommand(int index)
    {
        if (index < 0 || index >= Config.Commands.Count)
            return;

        string name = Config.Commands[index].Name;

        Config.Commands.RemoveAt(index);
        Save();

        PushRemoval(name);
    }

    private void SetCommandEnabled(CustomSlashCommand command, bool enabled)
    {
        if (enabled && command.IsEnabled == false && EnabledCount >= DiscordSlashCommandService.MaxUserCommands)
        {
            plugin.NotificationManager.Add(
                "Slash Commands",
                $"Command limit reached ({DiscordSlashCommandService.MaxUserCommands}). Disable another command first.",
                CordiNotificationType.Warning);
            return;
        }

        command.IsEnabled = enabled;
        Save();

        if (enabled)
            PushCommand(command);
        else
            PushRemoval(command.Name);
    }

    private void SetGroupEnabled(string group, bool enabled)
    {
        foreach (var command in CommandsInGroup(group))
        {
            if (command.IsEnabled == enabled)
                continue;

            SetCommandEnabled(command, enabled);
        }
    }

    private void PushCommand(CustomSlashCommand command)
    {
        if (!Config.Enabled || SlashService is not { } service)
            return;

        var snapshot = CloneCommand(command);

        Task.Run(async () =>
        {
            try
            {
                await service.RegisterSingleCommandAsync(snapshot);
            }
            catch (Exception ex)
            {
                plugin.NotificationManager.Add(
                    "Slash Commands",
                    $"Saved locally but Discord rejected /{snapshot.Name}: {ex.Message}",
                    CordiNotificationType.Warning);
            }
        });
    }

    private void PushRemoval(string commandName)
    {
        if (!Config.Enabled || SlashService is not { } service)
            return;

        Task.Run(async () =>
        {
            try
            {
                await service.UnregisterSingleCommandAsync(commandName);
            }
            catch (Exception ex)
            {
                plugin.NotificationManager.Add(
                    "Slash Commands",
                    $"Could not remove /{commandName} from Discord: {ex.Message}",
                    CordiNotificationType.Warning);
            }
        });
    }

    private void SyncAll()
    {
        if (SlashService is not { } service)
            return;

        Task.Run(async () =>
        {
            try
            {
                await service.RegisterCommandsAsync();
                plugin.NotificationManager.Add(
                    "Slash Commands", "Commands synced with Discord.", CordiNotificationType.Success);
            }
            catch (Exception ex)
            {
                plugin.NotificationManager.Add(
                    "Slash Commands", $"Sync failed: {ex.Message}", CordiNotificationType.Error);
            }
        });
    }

    private void UnregisterAll()
    {
        if (SlashService is not { } service)
            return;

        Task.Run(async () =>
        {
            try
            {
                await service.UnregisterAllCommandsAsync();
                plugin.NotificationManager.Add(
                    "Slash Commands", "All Cordi commands removed from Discord.", CordiNotificationType.Success);
            }
            catch (Exception ex)
            {
                plugin.NotificationManager.Add(
                    "Slash Commands", $"Removal failed: {ex.Message}", CordiNotificationType.Error);
            }
        });
    }

    private IEnumerable<CustomSlashCommand> CommandsInGroup(string group) =>
        Config.Commands.Where(c => string.Equals(NormalizeGroup(c.Group), group, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeGroup(string? group) =>
        string.IsNullOrWhiteSpace(group) ? string.Empty : group.Trim();

    private string? ValidateCommand(CustomSlashCommand cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name))
            return "Command name is required.";

        if (cmd.Name.Length > 32)
            return "Command name must be 32 characters or less.";

        if (cmd.Name.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
            return "Command name can only contain letters, digits, hyphens and underscores.";

        if (string.Equals(cmd.Name, DiscordSlashCommandService.ManageCommandName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(cmd.Name, DiscordSlashCommandService.EmoteCommandName, StringComparison.OrdinalIgnoreCase))
            return $"/{cmd.Name} is reserved by Cordi.";

        for (int index = 0; index < Config.Commands.Count; index++)
        {
            if (index == editingIndex)
                continue;

            if (string.Equals(Config.Commands[index].Name, cmd.Name, StringComparison.OrdinalIgnoreCase))
                return $"/{cmd.Name} already exists.";
        }

        if (string.IsNullOrWhiteSpace(cmd.GameCommand))
            return "Game command is required.";

        if ((cmd.Description?.Length ?? 0) > 100)
            return "Description must be 100 characters or less.";

        var declared = new List<string>();

        foreach (var param in cmd.Parameters)
        {
            if (string.IsNullOrWhiteSpace(param.Name))
                return "Every parameter needs a name.";

            if (param.Name.Length > 32)
                return $"Parameter '{param.Name}' must be 32 characters or less.";

            if (param.Name.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
                return $"Parameter '{param.Name}' can only contain letters, digits, hyphens and underscores.";

            declared.Add(param.Name.ToLower());
        }

        if (declared.Count != declared.Distinct().Count())
            return "Parameter names must be unique.";

        var used = PlaceholderPattern.Matches(cmd.GameCommand)
            .Select(match => match.Groups[1].Value.Trim().ToLower())
            .ToList();

        foreach (var placeholder in used.Distinct())
        {
            if (!declared.Contains(placeholder))
                return $"The game command uses {{{placeholder}}} but no parameter is named '{placeholder}'.";
        }

        foreach (var name in declared)
        {
            if (!used.Contains(name))
                return $"Parameter '{name}' is never used, add {{{name}}} to the game command.";
        }

        if (cmd.IsEnabled)
        {
            int others = Config.Commands
                .Where((_, index) => index != editingIndex)
                .Count(c => c.IsEnabled);

            if (others >= DiscordSlashCommandService.MaxUserCommands)
                return $"Command limit reached ({DiscordSlashCommandService.MaxUserCommands}). Disable another command first.";
        }

        return null;
    }

    private static string SanitizeCommandName(string name) =>
        new(name.ToLower().Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());

    private static CustomSlashCommand CloneCommand(CustomSlashCommand source) => new()
    {
        Name = source.Name,
        Description = source.Description,
        GameCommand = source.GameCommand,
        IsEmote = source.IsEmote,
        IsEnabled = source.IsEnabled,
        Group = source.Group,
        Parameters = source.Parameters.Select(p => new SlashCommandParameter
        {
            Name = p.Name,
            Description = p.Description,
            Required = p.Required,
            Type = p.Type,
        }).ToList(),
    };
}
