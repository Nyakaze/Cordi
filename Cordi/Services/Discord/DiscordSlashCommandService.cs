using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Cordi.Configuration;
using Cordi.Core;
using Dalamud.Plugin.Services;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Lumina.Excel.Sheets;

namespace Cordi.Services.Discord;

public class DiscordSlashCommandService : IDisposable
{
    private static readonly IPluginLog Logger = Service.Log;
    private readonly CordiPlugin _plugin;
    private DiscordClient? _client;
    private bool _bound;

    private const int DiscordMaxGuildCommands = 100;
    // 2 slots reserved: /cordi (management) + /emote (universal emote)
    private const int ReservedSlots = 2;
    private const int MaxUserCommands = DiscordMaxGuildCommands - ReservedSlots;
    private const string ManageCommandName = "cordi";
    private const string EmoteCommandName = "emote";

    /// <summary>
    /// In-memory list of all emote commands loaded from game data. Not persisted to config.
    /// </summary>
    public List<CustomSlashCommand> EmoteCommands { get; } = new();

    private CordiLogService Log => _plugin.LogService;
    private const string LogSource = "SlashCommands";

    public DiscordSlashCommandService(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public void Bind(DiscordClient client)
    {
        if (_bound) Unbind();
        _client = client;
        _client.InteractionCreated += OnInteractionCreated;
        _bound = true;
    }

    public void Unbind()
    {
        if (_client != null && _bound)
        {
            _client.InteractionCreated -= OnInteractionCreated;
        }
        _bound = false;
        _client = null;
    }

    /// <summary>
    /// Populates the in-memory emote list from FFXIV game data.
    /// Emotes are embedded in the plugin and not persisted to config.
    /// Also cleans any leftover emote entries from config (migration).
    /// </summary>
    public void PopulateEmoteCommands()
    {
        var config = _plugin.Config.SlashCommands;

        // Migration: remove any emote commands that were previously stored in config
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

                // Register primary command and alias
                var commandsToAdd = new List<(string name, string gameCmd)>();

                // Primary
                var primaryName = slashCmd.TrimStart('/').ToLower();
                if (!string.IsNullOrEmpty(primaryName))
                    commandsToAdd.Add((primaryName, slashCmd));

                // Alias
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

    // ─── Command Building ───────────────────────────────────────────────

    /// <summary>
    /// Builds the built-in /cordi management command.
    /// </summary>
    private DiscordApplicationCommand BuildManageCommand()
    {
        var commandOption = new DiscordApplicationCommandOption(
            "command", "The command name", ApplicationCommandOptionType.String, true);
        var groupOption = new DiscordApplicationCommandOption(
            "group", "The group name", ApplicationCommandOptionType.String, true);

        var subcommands = new List<DiscordApplicationCommandOption>
        {
            new("enable", "Enable a command", ApplicationCommandOptionType.SubCommand, false, null,
                new[] { commandOption }),
            new("disable", "Disable a command", ApplicationCommandOptionType.SubCommand, false, null,
                new[] { commandOption }),
            new("enable-group", "Enable all commands in a group", ApplicationCommandOptionType.SubCommand, false, null,
                new[] { groupOption }),
            new("disable-group", "Disable all commands in a group", ApplicationCommandOptionType.SubCommand, false,
                null, new[] { groupOption }),
            new("list", "List all commands and their status", ApplicationCommandOptionType.SubCommand),
            new("groups", "List all groups and their status", ApplicationCommandOptionType.SubCommand),
        };

        return new DiscordApplicationCommand(ManageCommandName, "Manage Cordi slash commands [Cordi]", subcommands);
    }

    /// <summary>
    /// Builds the built-in /emote command with autocomplete for emote name.
    /// </summary>
    private DiscordApplicationCommand BuildEmoteCommand()
    {
        var nameOption = new DiscordApplicationCommandOption(
            "name", "The emote to perform (e.g. dance, wave, hug)",
            ApplicationCommandOptionType.String,
            required: true,
            choices: null,
            options: null,
            channelTypes: null,
            autocomplete: true
        );

        return new DiscordApplicationCommand(EmoteCommandName, "Perform any FFXIV emote [Cordi]",
            new[] { nameOption });
    }

    /// <summary>
    /// Builds Discord application command objects for all enabled commands in config,
    /// plus the built-in /cordi and /emote commands.
    /// </summary>
    private List<DiscordApplicationCommand> BuildApplicationCommands(out int skipped)
    {
        var config = _plugin.Config.SlashCommands;
        var result = new List<DiscordApplicationCommand>();
        var seen = new HashSet<string>();
        skipped = 0;

        // Always include built-in commands first
        result.Add(BuildManageCommand());
        seen.Add(ManageCommandName);
        result.Add(BuildEmoteCommand());
        seen.Add(EmoteCommandName);

        // User commands (enabled)
        foreach (var cmd in config.Commands.Where(c => c.IsEnabled))
        {
            var appCmd = BuildSingleCommand(cmd);
            if (appCmd is null) continue;
            if (!seen.Add(appCmd.Name)) continue;
            result.Add(appCmd);
        }
        // Emotes are always available via /emote and not registered individually

        return result;
    }

    private DiscordApplicationCommand? BuildSingleCommand(CustomSlashCommand cmd)
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
                    param.Name.ToLower(),
                    string.IsNullOrWhiteSpace(param.Description) ? param.Name : param.Description,
                    ApplicationCommandOptionType.String,
                    param.Required
                ));
            }
            if (options.Count == 0) options = null;
        }

        return new DiscordApplicationCommand(cmd.Name.ToLower(), description, options);
    }

    // ─── Registration ───────────────────────────────────────────────────

    public async Task RegisterCommandsAsync()
    {
        if (_client == null)
        {
            Log.Warning(LogSource, "Cannot register commands: Discord client is null (not connected?).");
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

            await _client.BulkOverwriteGuildApplicationCommandsAsync(guildId, appCommands);
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
        if (_client == null) return;
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
            await _client.BulkOverwriteGuildApplicationCommandsAsync(guildId, appCommands);
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
        if (_client == null) return;
        var config = _plugin.Config.SlashCommands;
        if (string.IsNullOrEmpty(config.GuildId) || !ulong.TryParse(config.GuildId, out var guildId))
            return;

        try
        {
            await _client.BulkOverwriteGuildApplicationCommandsAsync(guildId, Array.Empty<DiscordApplicationCommand>());
            Log.Info(LogSource, "Unregistered all guild commands.");
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Failed to unregister commands: {ex.Message}");
        }
    }

    // ─── Interaction Handling ───────────────────────────────────────────

    private async Task OnInteractionCreated(DiscordClient sender, InteractionCreateEventArgs e)
    {
        var config = _plugin.Config.SlashCommands;
        if (!config.Enabled) return;

        // Handle autocomplete for /emote
        if (e.Interaction.Type == InteractionType.AutoComplete)
        {
            await HandleAutocomplete(e.Interaction, config);
            return;
        }

        if (e.Interaction.Type != InteractionType.ApplicationCommand)
            return;

        var commandName = e.Interaction.Data.Name;

        // Built-in: /cordi management
        if (string.Equals(commandName, ManageCommandName, StringComparison.OrdinalIgnoreCase))
        {
            await HandleManageCommand(e.Interaction, config);
            return;
        }

        // Built-in: /emote <name>
        if (string.Equals(commandName, EmoteCommandName, StringComparison.OrdinalIgnoreCase))
        {
            await HandleEmoteCommand(e.Interaction, config);
            return;
        }

        // Channel restriction (does not apply to /cordi or /emote)
        if (!string.IsNullOrEmpty(config.CommandChannelId) &&
            e.Interaction.ChannelId.ToString() != config.CommandChannelId)
        {
            await RespondAsync(e.Interaction, "This command can only be used in the designated command channel.", true);
            return;
        }

        // Custom commands
        var customCommand = config.Commands.FirstOrDefault(c =>
            string.Equals(c.Name, commandName, StringComparison.OrdinalIgnoreCase));

        if (customCommand == null || !customCommand.IsEnabled)
            return;

        Log.Info(LogSource, $"Slash command invoked: /{commandName} by {e.Interaction.User.Username}");
        await ExecuteGameCommand(e.Interaction, customCommand);
    }

    // ─── /emote Command ─────────────────────────────────────────────────

    private async Task HandleAutocomplete(DiscordInteraction interaction, SlashCommandConfig config)
    {
        if (!string.Equals(interaction.Data.Name, EmoteCommandName, StringComparison.OrdinalIgnoreCase))
            return;

        var focusedOption = interaction.Data.Options?.FirstOrDefault(o => o.Name == "name");
        var typed = focusedOption?.Value?.ToString()?.ToLower() ?? "";

        // Search emotes matching what the user typed
        var matches = EmoteCommands
            .Where(c =>
                c.Name.Contains(typed, StringComparison.OrdinalIgnoreCase) ||
                (c.Description ?? "").Contains(typed, StringComparison.OrdinalIgnoreCase))
            .Take(25) // Discord allows max 25 autocomplete choices
            .Select(c => new DiscordAutoCompleteChoice(
                $"/{c.Name} — {(c.Description ?? "").Replace(" [Cordi]", "").Replace("Perform the ", "")}".Length > 100
                    ? $"/{c.Name}"
                    : $"/{c.Name} — {(c.Description ?? "").Replace(" [Cordi]", "").Replace("Perform the ", "")}",
                c.Name))
            .ToList();

        try
        {
            await interaction.CreateResponseAsync(InteractionResponseType.AutoCompleteResult,
                new DiscordInteractionResponseBuilder().AddAutoCompleteChoices(matches));
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Autocomplete failed: {ex.Message}");
        }
    }

    private async Task HandleEmoteCommand(DiscordInteraction interaction, SlashCommandConfig config)
    {
        var nameOption = interaction.Data.Options?.FirstOrDefault(o => o.Name == "name");
        var emoteName = nameOption?.Value?.ToString()?.ToLower()?.Trim();

        if (string.IsNullOrEmpty(emoteName))
        {
            await RespondAsync(interaction, "Please provide an emote name.", true);
            return;
        }

        // Look up the emote in the in-memory list
        var emoteCmd = EmoteCommands.FirstOrDefault(c =>
            string.Equals(c.Name, emoteName, StringComparison.OrdinalIgnoreCase));

        if (emoteCmd == null)
        {
            await RespondAsync(interaction, $"Emote `{emoteName}` not found.", true);
            return;
        }

        Log.Info(LogSource, $"/emote {emoteName} invoked by {interaction.User.Username}");
        await ExecuteGameCommand(interaction, emoteCmd);
    }

    // ─── Game Command Execution ─────────────────────────────────────────

    private async Task ExecuteGameCommand(DiscordInteraction interaction, CustomSlashCommand command)
    {
        try
        {
            var gameCommand = command.GameCommand;

            if (interaction.Data.Options != null && !command.IsEmote)
            {
                foreach (var option in interaction.Data.Options)
                {
                    // Skip the "name" option from /emote
                    if (string.Equals(interaction.Data.Name, EmoteCommandName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var placeholder = $"{{{option.Name}}}";
                    gameCommand = gameCommand.Replace(placeholder, option.Value?.ToString() ?? "");
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

    // ─── /cordi Management Command ──────────────────────────────────────

    private async Task HandleManageCommand(DiscordInteraction interaction, SlashCommandConfig config)
    {
        var subcommand = interaction.Data.Options?.FirstOrDefault();
        if (subcommand == null)
        {
            await RespondAsync(interaction, "Unknown subcommand.", true);
            return;
        }

        Log.Info(LogSource, $"/cordi {subcommand.Name} invoked by {interaction.User.Username}");

        switch (subcommand.Name)
        {
            case "enable":
                await HandleEnableDisable(interaction, config, subcommand, true);
                break;
            case "disable":
                await HandleEnableDisable(interaction, config, subcommand, false);
                break;
            case "enable-group":
                await HandleGroupEnableDisable(interaction, config, subcommand, true);
                break;
            case "disable-group":
                await HandleGroupEnableDisable(interaction, config, subcommand, false);
                break;
            case "list":
                await HandleListCommands(interaction, config);
                break;
            case "groups":
                await HandleListGroups(interaction, config);
                break;
            default:
                await RespondAsync(interaction, $"Unknown subcommand: `{subcommand.Name}`", true);
                break;
        }
    }

    private async Task HandleEnableDisable(DiscordInteraction interaction, SlashCommandConfig config,
        DiscordInteractionDataOption subcommand, bool enable)
    {
        var cmdNameOption = subcommand.Options?.FirstOrDefault(o => o.Name == "command");
        var cmdName = cmdNameOption?.Value?.ToString()?.ToLower()?.Trim();

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

    private async Task HandleGroupEnableDisable(DiscordInteraction interaction, SlashCommandConfig config,
        DiscordInteractionDataOption subcommand, bool enable)
    {
        var groupOption = subcommand.Options?.FirstOrDefault(o => o.Name == "group");
        var groupName = groupOption?.Value?.ToString()?.Trim();

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

    // ─── Helpers ────────────────────────────────────────────────────────

    private async Task RespondAsync(DiscordInteraction interaction, string message, bool ephemeral)
    {
        try
        {
            await interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent(message)
                    .AsEphemeral(ephemeral));
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Failed to respond to interaction: {ex.Message}");
        }
    }

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
        Unbind();
    }
}
