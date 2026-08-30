using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Services.Features;
using Crovus.Client;
using Crovus.Events;
using Crovus.Models;
using Lumina.Excel.Sheets;

namespace Cordi.Services.Discord;

public class DiscordSlashCommandService : IDisposable
{
    private readonly CordiPlugin _plugin;
    private readonly ScreenshotService _screenshotService;
    private bool _bound;

    private const int DiscordMaxGuildCommands = 100;
    private const int ReservedSlots = 2;
    private const int MaxUserCommands = DiscordMaxGuildCommands - ReservedSlots;
    private const string ManageCommandName = "cordi";
    private const string EmoteCommandName = "emote";
    private const int MaxAutocompleteChoices = 25;

    public List<CustomSlashCommand> EmoteCommands { get; } = new();

    private CordiLogService Log => _plugin.LogService;
    private const string LogSource = "SlashCommands";

    private ICrovusContext? Context => _plugin.DiscordConnection.Context;

    public DiscordSlashCommandService(CordiPlugin plugin, ScreenshotService screenshotService)
    {
        _plugin = plugin;
        _screenshotService = screenshotService;
    }

    public void Bind()
    {
        if (_bound) return;
        _bound = true;

        _plugin.DiscordConnection.Ready += OnReadyAsync;
        _plugin.DiscordConnection.Register<InteractionCreatedEvent>(OnInteractionCreatedAsync);
    }

    private async Task OnReadyAsync(ReadyEvent e)
    {
        if (!_plugin.Config.SlashCommands.Enabled) return;

        try
        {
            PopulateEmoteCommands();

            await RegisterCommandsAsync();
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Failed to auto-register slash commands: {ex.Message}");
        }
    }

    public void PopulateEmoteCommands()
    {
        var config = _plugin.Config.SlashCommands;

        int removed = config.Commands.RemoveAll(c => c.IsEmote);
        if (removed > 0)
        {
            _plugin.Config.Save();
            Log.Info(LogSource, $"Cleaned {removed} emote commands from config (now in-memory only).");
        }

        EmoteCommands.Clear();

        try
        {
            var emoteSheet = Service.DataManager.GetExcelSheet<Emote>();
            if (emoteSheet == null)
            {
                Log.Warning(LogSource, "Could not load Emote sheet from game data.");
                return;
            }

            var existingNames = new HashSet<string>();

            foreach (var emoteRow in emoteSheet)
            {
                if (!emoteRow.TextCommand.IsValid) continue;

                var textCmd = emoteRow.TextCommand.Value;
                var rawCmd = textCmd.Command.ToString();
                if (string.IsNullOrWhiteSpace(rawCmd)) continue;

                var emoteName = emoteRow.Name.ToString();
                var slashCmd = rawCmd.StartsWith("/") ? rawCmd : "/" + rawCmd;

                if (string.IsNullOrWhiteSpace(emoteName))
                    emoteName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(slashCmd.TrimStart('/'));
                else
                    emoteName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(emoteName);

                var commandsToAdd = new List<(string name, string gameCmd)>();

                var primaryName = slashCmd.TrimStart('/').ToLower();
                if (!string.IsNullOrEmpty(primaryName))
                    commandsToAdd.Add((primaryName, slashCmd));

                var rawAlias = textCmd.Alias.ToString();
                if (!string.IsNullOrWhiteSpace(rawAlias))
                {
                    var aliasCmd = rawAlias.StartsWith("/") ? rawAlias : "/" + rawAlias;
                    var aliasName = aliasCmd.TrimStart('/').ToLower();
                    if (!string.IsNullOrEmpty(aliasName))
                        commandsToAdd.Add((aliasName, aliasCmd));
                }

                foreach (var (cmdName, gameCmd) in commandsToAdd)
                {
                    if (cmdName.Length > 32) continue;
                    if (cmdName.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_')) continue;
                    if (cmdName == ManageCommandName || cmdName == EmoteCommandName) continue;
                    if (existingNames.Contains(cmdName)) continue;

                    EmoteCommands.Add(new CustomSlashCommand
                    {
                        Name = cmdName,
                        Description = $"Perform the {emoteName} emote",
                        GameCommand = gameCmd,
                        IsEmote = true,
                        IsEnabled = true,
                    });

                    existingNames.Add(cmdName);
                }
            }

            Log.Info(LogSource, $"Loaded {EmoteCommands.Count} emote commands from game data (in-memory).");
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Failed to populate emote commands: {ex.Message}");
        }
    }

    private ApplicationCommandRequest BuildManageCommand()
    {
        var nameOption = new DiscordApplicationCommandOption(
            ApplicationCommandOptionType.String, "name", "The command name") { Required = true };

        var groupNameOption = new DiscordApplicationCommandOption(
            ApplicationCommandOptionType.String, "name", "The group name") { Required = true };

        var commandGroup = new DiscordApplicationCommandOption(
            ApplicationCommandOptionType.SubCommandGroup, "command", "Manage individual commands")
        {
            Options = new[]
            {
                new DiscordApplicationCommandOption(
                    ApplicationCommandOptionType.SubCommand, "enable", "Enable a command")
                {
                    Options = new[] { nameOption }
                },
                new DiscordApplicationCommandOption(
                    ApplicationCommandOptionType.SubCommand, "disable", "Disable a command")
                {
                    Options = new[] { nameOption }
                },
                new DiscordApplicationCommandOption(
                    ApplicationCommandOptionType.SubCommand, "list", "List all commands and their status"),
            }
        };

        var groupGroup = new DiscordApplicationCommandOption(
            ApplicationCommandOptionType.SubCommandGroup, "cmdgroup", "Manage command groups")
        {
            Options = new[]
            {
                new DiscordApplicationCommandOption(
                    ApplicationCommandOptionType.SubCommand, "enable", "Enable all commands in a group")
                {
                    Options = new[] { groupNameOption }
                },
                new DiscordApplicationCommandOption(
                    ApplicationCommandOptionType.SubCommand, "disable", "Disable all commands in a group")
                {
                    Options = new[] { groupNameOption }
                },
                new DiscordApplicationCommandOption(
                    ApplicationCommandOptionType.SubCommand, "list", "List all groups and their status"),
            }
        };

        var screenshotCommand = new DiscordApplicationCommandOption(
            ApplicationCommandOptionType.SubCommand, "screenshot",
            "Capture and send a screenshot of the current game state");

        return new ApplicationCommandRequest(ManageCommandName, ApplicationCommandType.ChatInput)
        {
            Description = "Manage Cordi slash commands [Cordi]",
            Options = new[] { commandGroup, groupGroup, screenshotCommand }
        };
    }

    private ApplicationCommandRequest BuildEmoteCommand()
    {
        var nameOption = new DiscordApplicationCommandOption(
            ApplicationCommandOptionType.String, "name", "The emote to perform (e.g. dance, wave, hug)")
        {
            Required = true,
            Autocomplete = true
        };

        return new ApplicationCommandRequest(EmoteCommandName, ApplicationCommandType.ChatInput)
        {
            Description = "Perform any FFXIV emote [Cordi]",
            Options = new[] { nameOption }
        };
    }

    private List<ApplicationCommandRequest> BuildApplicationCommands(out int skipped)
    {
        var config = _plugin.Config.SlashCommands;
        var result = new List<ApplicationCommandRequest>();
        var seen = new HashSet<string>();
        skipped = 0;

        result.Add(BuildManageCommand());
        seen.Add(ManageCommandName);
        result.Add(BuildEmoteCommand());
        seen.Add(EmoteCommandName);

        foreach (var cmd in config.Commands.Where(c => c.IsEnabled))
        {
            var appCmd = BuildSingleCommand(cmd);
            if (appCmd is null) continue;
            if (!seen.Add(appCmd.Name)) continue;
            result.Add(appCmd);
        }

        return result;
    }

    private ApplicationCommandRequest? BuildSingleCommand(CustomSlashCommand cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name) || string.IsNullOrWhiteSpace(cmd.GameCommand))
            return null;

        var description = cmd.Description ?? "";
        if (string.IsNullOrWhiteSpace(description))
            description = $"Executes {cmd.GameCommand}";

        if (!description.EndsWith("[Cordi]"))
            description = description.Length > 90 ? description[..90] + " [Cordi]" : description + " [Cordi]";

        if (description.Length > 100)
            description = description[..93] + " [Cordi]";

        List<DiscordApplicationCommandOption>? options = null;

        if (cmd.Parameters is { Count: > 0 })
        {
            options = new List<DiscordApplicationCommandOption>();
            foreach (var param in cmd.Parameters)
            {
                if (string.IsNullOrWhiteSpace(param.Name)) continue;
                options.Add(new DiscordApplicationCommandOption(
                    ApplicationCommandOptionType.String,
                    param.Name.ToLower(),
                    string.IsNullOrWhiteSpace(param.Description) ? param.Name : param.Description)
                {
                    Required = param.Required
                });
            }
            if (options.Count == 0) options = null;
        }

        return new ApplicationCommandRequest(cmd.Name.ToLower(), ApplicationCommandType.ChatInput)
        {
            Description = description,
            Options = options
        };
    }

    public async Task RegisterCommandsAsync()
    {
        if (Context is not { } context)
        {
            Log.Warning(LogSource, "Cannot register commands: Discord client is null (not connected?).");
            return;
        }

        if (context.ApplicationId is not { } applicationId)
        {
            Log.Warning(LogSource, "Cannot register commands: application id is unknown.");
            return;
        }

        var config = _plugin.Config.SlashCommands;
        if (!config.Enabled)
        {
            Log.Warning(LogSource, "Cannot register commands: slash commands are disabled in settings.");
            return;
        }

        if (string.IsNullOrEmpty(config.GuildId) || !ulong.TryParse(config.GuildId, out var guildId))
        {
            Log.Warning(LogSource, "No guild ID configured for slash command registration.");
            return;
        }

        try
        {
            var appCommands = BuildApplicationCommands(out int skipped);

            Log.Info(LogSource, $"Sending {appCommands.Count} command(s) to Discord (guild {guildId})...");
            foreach (var ac in appCommands)
                Log.Debug(LogSource, $"  -> /{ac.Name}: {ac.Description}");

            await context.Services.Commands.DeployAsync(applicationId, appCommands, guildId);
            Log.Info(LogSource, $"Bulk-registered {appCommands.Count} command(s) with Discord.");

            if (skipped > 0)
                Log.Warning(LogSource, $"{skipped} command(s) skipped — Discord limit is {DiscordMaxGuildCommands} guild commands.");
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Failed to register slash commands: {ex}");
        }
    }

    public async Task RegisterSingleCommandAsync(CustomSlashCommand cmd)
    {
        if (Context is not { } context) return;
        if (context.ApplicationId is not { } applicationId) return;

        var config = _plugin.Config.SlashCommands;
        if (!config.Enabled) return;

        if (string.IsNullOrEmpty(config.GuildId) || !ulong.TryParse(config.GuildId, out var guildId))
        {
            Log.Warning(LogSource, "No guild ID configured for slash command registration.");
            return;
        }

        try
        {
            var appCommands = BuildApplicationCommands(out _);
            await context.Services.Commands.DeployAsync(applicationId, appCommands, guildId);
            Log.Info(LogSource, $"Registered /{cmd.Name} (bulk-synced {appCommands.Count} command(s)).");
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Failed to register /{cmd.Name}: {ex.Message}");
            throw;
        }
    }

    public async Task UnregisterAllCommandsAsync()
    {
        if (Context is not { } context) return;
        if (context.ApplicationId is not { } applicationId) return;

        var config = _plugin.Config.SlashCommands;
        if (string.IsNullOrEmpty(config.GuildId) || !ulong.TryParse(config.GuildId, out var guildId))
            return;

        try
        {
            await context.Services.Commands.ClearAsync(applicationId, guildId);
            Log.Info(LogSource, "Unregistered all guild commands.");
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Failed to unregister commands: {ex.Message}");
        }
    }

    private async Task OnInteractionCreatedAsync(InteractionCreatedEvent e, CancellationToken ct)
    {
        var config = _plugin.Config.SlashCommands;
        if (!config.Enabled) return;

        var interaction = e.Interaction;

        if (interaction.Type == InteractionType.ApplicationCommandAutocomplete)
        {
            await HandleAutocomplete(interaction);
            return;
        }

        if (interaction.Type != InteractionType.ApplicationCommand)
            return;

        var commandName = interaction.CommandName;

        if (string.Equals(commandName, ManageCommandName, StringComparison.OrdinalIgnoreCase))
        {
            await HandleManageCommand(interaction, config);
            return;
        }

        if (string.Equals(commandName, EmoteCommandName, StringComparison.OrdinalIgnoreCase))
        {
            await HandleEmoteCommand(interaction);
            return;
        }

        if (!string.IsNullOrEmpty(config.CommandChannelId) &&
            interaction.ChannelId?.ToString() != config.CommandChannelId)
        {
            await RespondAsync(interaction, "This command can only be used in the designated command channel.", true);
            return;
        }

        var customCommand = config.Commands.FirstOrDefault(c =>
            string.Equals(c.Name, commandName, StringComparison.OrdinalIgnoreCase));

        if (customCommand == null || !customCommand.IsEnabled)
            return;

        Log.Info(LogSource, $"Slash command invoked: /{commandName} by {Describe(interaction)}");
        await ExecuteGameCommand(interaction, customCommand);
    }

    private async Task HandleAutocomplete(DiscordInteraction interaction)
    {
        if (!string.Equals(interaction.CommandName, EmoteCommandName, StringComparison.OrdinalIgnoreCase))
            return;

        if (Context is not { } context) return;

        var focusedOption = interaction.FocusedOption ?? interaction.Option("name");
        var typed = focusedOption?.AsString.ToLower() ?? "";

        var matches = EmoteCommands
            .Where(c =>
                c.Name.Contains(typed, StringComparison.OrdinalIgnoreCase) ||
                (c.Description ?? "").Contains(typed, StringComparison.OrdinalIgnoreCase))
            .Take(MaxAutocompleteChoices)
            .Select(c => DiscordApplicationCommandChoice.Text(BuildChoiceLabel(c), c.Name))
            .ToList();

        try
        {
            await context.Services.Interactions.AutocompleteAsync(interaction, matches);
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Autocomplete failed: {ex.Message}");
        }
    }

    private static string BuildChoiceLabel(CustomSlashCommand command)
    {
        var description = (command.Description ?? "").Replace(" [Cordi]", "").Replace("Perform the ", "");
        var label = $"/{command.Name} — {description}";

        return label.Length > 100 ? $"/{command.Name}" : label;
    }

    private async Task HandleEmoteCommand(DiscordInteraction interaction)
    {
        var emoteName = interaction.Option("name")?.AsString.ToLower().Trim();

        if (string.IsNullOrEmpty(emoteName))
        {
            await RespondAsync(interaction, "Please provide an emote name.", true);
            return;
        }

        var emoteCmd = EmoteCommands.FirstOrDefault(c =>
            string.Equals(c.Name, emoteName, StringComparison.OrdinalIgnoreCase));

        if (emoteCmd == null)
        {
            await RespondAsync(interaction, $"Emote `{emoteName}` not found.", true);
            return;
        }

        Log.Info(LogSource, $"/emote {emoteName} invoked by {Describe(interaction)}");
        await ExecuteGameCommand(interaction, emoteCmd);
    }

    private async Task ExecuteGameCommand(DiscordInteraction interaction, CustomSlashCommand command)
    {
        try
        {
            var gameCommand = command.GameCommand;

            if (!command.IsEmote)
            {
                foreach (var option in interaction.Arguments)
                {
                    var placeholder = $"{{{option.Name}}}";
                    gameCommand = gameCommand.Replace(placeholder, option.HasValue ? option.AsString : "");
                }
            }

            if (command.Parameters != null)
            {
                foreach (var param in command.Parameters)
                    gameCommand = gameCommand.Replace($"{{{param.Name.ToLower()}}}", "");
            }

            gameCommand = gameCommand.Trim();

            if (string.IsNullOrWhiteSpace(gameCommand))
            {
                await RespondAsync(interaction, "Command resulted in an empty game command.", true);
                return;
            }

            if (!gameCommand.StartsWith("/"))
                gameCommand = "/" + gameCommand;

            string? targetName = null;
            var finalGameCommand = gameCommand;

            await CordiPlugin.Framework.RunOnFrameworkThread(() =>
            {
                if (!Service.ClientState.IsLoggedIn)
                {
                    Log.Warning(LogSource, "Cannot execute command: not logged in.");
                    return;
                }

                if (command.IsEmote)
                {
                    var currentTarget = Service.TargetManager.Target;
                    if (currentTarget != null)
                        targetName = currentTarget.Name.TextValue;
                }

                _plugin._chat.SendMessage(finalGameCommand);
                Log.Info(LogSource, $"Executed game command: {finalGameCommand}");
            });

            string responseMessage;
            if (command.IsEmote && !string.IsNullOrEmpty(targetName))
                responseMessage = $"Executed: `{gameCommand}` on **{targetName}**";
            else
                responseMessage = $"Executed: `{gameCommand}`";

            await RespondAsync(interaction, responseMessage, false);
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Error executing /{command.Name}: {ex.Message}");
            await RespondAsync(interaction, $"Error executing command: {ex.Message}", true);
        }
    }

    private async Task HandleManageCommand(DiscordInteraction interaction, SlashCommandConfig config)
    {
        var subGroup = interaction.SubCommandGroup;
        var subCommand = interaction.SubCommand;

        if (subCommand == null)
        {
            await RespondAsync(interaction, "Unknown subcommand.", true);
            return;
        }

        if (subGroup == null)
        {
            if (subCommand == "screenshot")
            {
                Log.Info(LogSource, $"/cordi screenshot invoked by {Describe(interaction)}");
                await HandleScreenshotCommand(interaction);
                return;
            }

            await RespondAsync(interaction, $"Unknown subcommand: `{subCommand}`", true);
            return;
        }

        Log.Info(LogSource, $"/cordi {subGroup} {subCommand} invoked by {Describe(interaction)}");

        switch (subGroup)
        {
            case "command":
                switch (subCommand)
                {
                    case "enable":
                        await HandleEnableDisable(interaction, config, true);
                        break;
                    case "disable":
                        await HandleEnableDisable(interaction, config, false);
                        break;
                    case "list":
                        await HandleListCommands(interaction, config);
                        break;
                    default:
                        await RespondAsync(interaction, $"Unknown subcommand: `{subCommand}`", true);
                        break;
                }
                break;
            case "cmdgroup":
                switch (subCommand)
                {
                    case "enable":
                        await HandleGroupEnableDisable(interaction, config, true);
                        break;
                    case "disable":
                        await HandleGroupEnableDisable(interaction, config, false);
                        break;
                    case "list":
                        await HandleListGroups(interaction, config);
                        break;
                    default:
                        await RespondAsync(interaction, $"Unknown subcommand: `{subCommand}`", true);
                        break;
                }
                break;
            default:
                await RespondAsync(interaction, $"Unknown subcommand group: `{subGroup}`", true);
                break;
        }
    }

    private async Task HandleEnableDisable(DiscordInteraction interaction, SlashCommandConfig config, bool enable)
    {
        var cmdName = interaction.Option("name")?.AsString.ToLower().Trim();

        if (string.IsNullOrEmpty(cmdName))
        {
            await RespondAsync(interaction, "Please provide a command name.", true);
            return;
        }

        var command = config.Commands.FirstOrDefault(c =>
            string.Equals(c.Name, cmdName, StringComparison.OrdinalIgnoreCase));

        if (command == null)
        {
            await RespondAsync(interaction, $"Command `/{cmdName}` not found.", true);
            return;
        }

        if (command.IsEnabled == enable)
        {
            await RespondAsync(interaction, $"`/{cmdName}` is already {(enable ? "enabled" : "disabled")}.", true);
            return;
        }

        if (enable)
        {
            int enabledCount = config.Commands.Count(c => c.IsEnabled);
            if (enabledCount >= MaxUserCommands)
            {
                await RespondAsync(interaction,
                    $"Cannot enable `/{cmdName}` — command limit reached ({enabledCount}/{MaxUserCommands}). Disable another command first.",
                    true);
                return;
            }
        }

        command.IsEnabled = enable;
        _plugin.Config.Save();

        try
        {
            await RegisterCommandsAsync();
            await RespondAsync(interaction,
                $"`/{cmdName}` has been **{(enable ? "enabled" : "disabled")}**. Commands synced.", false);
        }
        catch
        {
            await RespondAsync(interaction,
                $"`/{cmdName}` {(enable ? "enabled" : "disabled")} locally, but Discord sync failed.", true);
        }
    }

    private async Task HandleGroupEnableDisable(DiscordInteraction interaction, SlashCommandConfig config, bool enable)
    {
        var groupName = interaction.Option("name")?.AsString.Trim();

        if (string.IsNullOrEmpty(groupName))
        {
            await RespondAsync(interaction, "Please provide a group name.", true);
            return;
        }

        var group = config.Groups.FirstOrDefault(g =>
            string.Equals(g.Name, groupName, StringComparison.OrdinalIgnoreCase));

        if (group == null)
        {
            await RespondAsync(interaction, $"Group `{groupName}` not found.", true);
            return;
        }

        var cmdsInGroup = config.Commands.Where(c =>
            string.Equals(c.Group, group.Name, StringComparison.OrdinalIgnoreCase)).ToList();

        if (cmdsInGroup.Count == 0)
        {
            await RespondAsync(interaction, $"Group `{group.Name}` has no commands.", true);
            return;
        }

        if (enable)
        {
            int enabledCount = config.Commands.Count(c => c.IsEnabled);
            int toEnable = cmdsInGroup.Count(c => !c.IsEnabled);
            if (enabledCount + toEnable > MaxUserCommands)
            {
                await RespondAsync(interaction,
                    $"Cannot enable group — would exceed limit ({enabledCount} + {toEnable} > {MaxUserCommands}).",
                    true);
                return;
            }
        }

        int changed = 0;
        foreach (var cmd in cmdsInGroup)
        {
            if (cmd.IsEnabled != enable)
            {
                cmd.IsEnabled = enable;
                changed++;
            }
        }
        _plugin.Config.Save();

        try
        {
            await RegisterCommandsAsync();
            await RespondAsync(interaction,
                $"Group `{group.Name}`: **{changed}** command(s) {(enable ? "enabled" : "disabled")}. Synced.", false);
        }
        catch
        {
            await RespondAsync(interaction,
                $"Group `{group.Name}`: {changed} command(s) updated locally, but sync failed.", true);
        }
    }

    private async Task HandleListCommands(DiscordInteraction interaction, SlashCommandConfig config)
    {
        int enabledCount = config.Commands.Count(c => c.IsEnabled);
        var lines = new List<string> { $"**Commands: {enabledCount}/{MaxUserCommands}**", "" };

        var enabled = config.Commands.Where(c => c.IsEnabled).OrderBy(c => c.Name).ToList();
        if (enabled.Count > 0)
        {
            lines.Add("**Enabled:**");
            foreach (var cmd in enabled.Take(30))
            {
                var groupTag = string.IsNullOrEmpty(cmd.Group) ? "" : $" `[{cmd.Group}]`";
                lines.Add($"  /{cmd.Name}{groupTag}");
            }
            if (enabled.Count > 30)
                lines.Add($"  ... and {enabled.Count - 30} more");
        }

        int disabledCount = config.Commands.Count(c => !c.IsEnabled);
        if (disabledCount > 0)
            lines.Add($"\n**Disabled:** {disabledCount} command(s)");

        lines.Add($"\n*{EmoteCommands.Count} emotes always available via `/emote <name>`.*");

        var message = string.Join("\n", lines);
        if (message.Length > 1900) message = message[..1900] + "\n...truncated";
        await RespondAsync(interaction, message, true);
    }

    private async Task HandleListGroups(DiscordInteraction interaction, SlashCommandConfig config)
    {
        if (config.Groups.Count == 0)
        {
            await RespondAsync(interaction, "No groups configured.", true);
            return;
        }

        var lines = new List<string> { "**Groups:**", "" };
        foreach (var group in config.Groups.OrderBy(g => g.Name))
        {
            var cmds = config.Commands.Where(c =>
                string.Equals(c.Group, group.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            int en = cmds.Count(c => c.IsEnabled);
            lines.Add($"**{group.Name}** — {en}/{cmds.Count} enabled");
        }

        await RespondAsync(interaction, string.Join("\n", lines), true);
    }

    private async Task HandleScreenshotCommand(DiscordInteraction interaction)
    {
        if (Context is not { } context) return;

        var interactions = context.Services.Interactions;

        try
        {
            await interactions.DeferAsync(interaction);

            bool isLoggedIn = await CordiPlugin.Framework.RunOnFrameworkThread(() => Service.ClientState.IsLoggedIn);
            if (!isLoggedIn)
            {
                await interactions.EditResponseAsync(interaction,
                    "Cannot capture screenshot: no game is currently active (not logged in).");
                return;
            }

            MemoryStream? screenshotStream = null;
            await CordiPlugin.Framework.RunOnFrameworkThread(() =>
            {
                screenshotStream = _screenshotService.CaptureGameWindow();
            });

            if (screenshotStream == null)
            {
                await interactions.EditResponseAsync(interaction,
                    "Screenshot capture failed. The game window may be minimized or unavailable.");
                return;
            }

            using (screenshotStream)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var fileName = $"ffxiv_screenshot_{timestamp}.png";
                var bytes = screenshotStream.ToArray();

                await interactions.EditResponseAsync(interaction, response => response
                    .WithContent($"Screenshot captured at {DateTime.Now:HH:mm:ss}")
                    .AddFile(fileName, bytes));
            }

            Log.Info(LogSource, "Screenshot sent successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Screenshot command failed: {ex.Message}");
            try
            {
                await interactions.EditResponseAsync(interaction, $"Screenshot failed: {ex.Message}");
            }
            catch
            {
            }
        }
    }

    private async Task RespondAsync(DiscordInteraction interaction, string message, bool ephemeral)
    {
        if (Context is not { } context) return;

        try
        {
            await context.Services.Interactions.RespondAsync(interaction, message, ephemeral);
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Failed to respond to interaction: {ex.Message}");
        }
    }

    private static string Describe(DiscordInteraction interaction) =>
        interaction.Invoker?.Username ?? "unknown";

    public int GetEnabledCommandCount()
    {
        return _plugin.Config.SlashCommands.Commands.Count(c => c.IsEnabled);
    }

    public bool WouldExceedLimit(int additional = 1)
    {
        return GetEnabledCommandCount() + additional > MaxUserCommands;
    }

    public void Dispose()
    {
        if (!_bound) return;
        _bound = false;

        _plugin.DiscordConnection.Ready -= OnReadyAsync;
    }
}
