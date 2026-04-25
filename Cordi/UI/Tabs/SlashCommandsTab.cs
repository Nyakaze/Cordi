using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Services;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Tabs;

public class SlashCommandsTab : ConfigTabBase
{
    private int? _editingCommandIndex;
    private CustomSlashCommand? _editState;
    private bool _isAddingNew;
    private string _statusMessage = string.Empty;
    private DateTime _statusMessageExpiry = DateTime.MinValue;

    private string _userSearchFilter = string.Empty;

    // Group management state
    private string _newGroupName = string.Empty;
    private readonly HashSet<string> _collapsedGroups = new();

    public override string Label => "Slash Commands";

    public SlashCommandsTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme) { }

    protected override IReadOnlyList<(string Label, Action Draw)> GetSubTabs()
    {
        return new (string, Action)[]
        {
            ("Commands", DrawCommandsList),
            ("Groups", DrawGroupsTab),
            ("Settings", DrawSettings),
        };
    }

    // ─── Settings Tab ───────────────────────────────────────────────────

    private void DrawSettings()
    {
        var config = plugin.Config.SlashCommands;

        bool enabled = config.Enabled;
        theme.DrawPluginCardAuto(
            id: "slash-cmd-settings",
            title: "Discord Slash Commands",
            drawContent: (avail) =>
            {
                ImGui.TextColored(UiTheme.ColorDangerText, "WARNING: This feature is experimental!");
                theme.SpacerY(0.5f);
                ImGui.TextWrapped("Register custom Discord slash commands that execute in-game commands. " +
                                  "Commands are registered per-guild and require the bot to have the 'applications.commands' scope.");
                theme.SpacerY(1f);

                // Guild ID
                ImGui.TextColored(theme.MutedText, "Guild ID:");
                string guildId = config.GuildId;
                ImGui.SetNextItemWidth(avail);
                if (ImGui.InputTextWithHint("##guildId", "Right-click your server > Copy Server ID", ref guildId, 32))
                {
                    config.GuildId = guildId;
                    plugin.Config.Save();
                }

                theme.SpacerY(0.5f);

                // Command Channel restriction
                ImGui.TextColored(theme.MutedText, "Restrict to Channel (optional):");
                var textChannels = plugin.ChannelCache.TextChannels;
                theme.ChannelPicker(
                    "slash-cmd-channel",
                    config.CommandChannelId,
                    textChannels,
                    (newId) =>
                    {
                        config.CommandChannelId = newId;
                        plugin.Config.Save();
                    },
                    defaultLabel: "Any Channel",
                    showLabel: false
                );

                theme.SpacerY(1f);
                ImGui.Separator();
                theme.SpacerY(1f);

                // Register/Unregister buttons
                float btnWidth = avail * 0.48f;

                using (ImRaii.Disabled(!config.Enabled || string.IsNullOrEmpty(config.GuildId) || plugin.SlashCommandService == null))
                {
                    if (theme.PrimaryButton("Register Commands", new Vector2(btnWidth, 0)))
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                await plugin.SlashCommandService!.RegisterCommandsAsync();
                                plugin.NotificationManager.Add("Slash Commands", "Commands registered successfully!", CordiNotificationType.Success);
                                SetStatus("Commands registered successfully!");
                            }
                            catch (Exception ex)
                            {
                                plugin.NotificationManager.Add("Slash Commands", $"Registration failed: {ex.Message}", CordiNotificationType.Error);
                                SetStatus($"Error: {ex.Message}");
                            }
                        });
                    }
                }

                ImGui.SameLine();

                using (ImRaii.Disabled(!config.Enabled || string.IsNullOrEmpty(config.GuildId) || plugin.SlashCommandService == null))
                {
                    if (theme.SecondaryButton("Unregister All", new Vector2(btnWidth, 0)))
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                await plugin.SlashCommandService!.UnregisterAllCommandsAsync();
                                plugin.NotificationManager.Add("Slash Commands", "All Cordi commands unregistered.", CordiNotificationType.Success);
                                SetStatus("All commands unregistered.");
                            }
                            catch (Exception ex)
                            {
                                plugin.NotificationManager.Add("Slash Commands", $"Unregister failed: {ex.Message}", CordiNotificationType.Error);
                                SetStatus($"Error: {ex.Message}");
                            }
                        });
                    }
                }

                // Status message
                if (!string.IsNullOrEmpty(_statusMessage) && DateTime.Now < _statusMessageExpiry)
                {
                    theme.SpacerY(0.5f);
                    var color = _statusMessage.StartsWith("Error") ? UiTheme.ColorDangerText : UiTheme.ColorSuccessText;
                    ImGui.TextColored(color, _statusMessage);
                }
            },
            enabled: ref enabled,
            showCheckbox: true
        );

        if (enabled != config.Enabled)
        {
            config.Enabled = enabled;
            plugin.Config.Save();
        }
    }

    // ─── Groups Tab ─────────────────────────────────────────────────────

    private void DrawGroupsTab()
    {
        var config = plugin.Config.SlashCommands;

        if (!config.Enabled)
        {
            theme.SpacerY(2f);
            ImGui.TextColored(theme.MutedText, "Enable Slash Commands in the Settings tab first.");
            return;
        }

        string? deleteGroup = null;

        bool dummy = true;
        theme.DrawPluginCardAuto(
            id: "group-management",
            title: "Command Groups",
            drawContent: (avail) =>
            {
                ImGui.TextColored(theme.MutedText, "Create groups to organize commands. Enable or disable all commands in a group at once.");
                theme.SpacerY(1f);

                // Add new group
                ImGui.SetNextItemWidth(avail - 110f * ImGuiHelpers.GlobalScale);
                ImGui.InputTextWithHint("##newGroupName", "New group name...", ref _newGroupName, 32);
                ImGui.SameLine();
                using (ImRaii.Disabled(string.IsNullOrWhiteSpace(_newGroupName) ||
                    config.Groups.Any(g => string.Equals(g.Name, _newGroupName.Trim(), StringComparison.OrdinalIgnoreCase))))
                {
                    if (theme.PrimaryButton("Add Group", new Vector2(100f * ImGuiHelpers.GlobalScale, 0)))
                    {
                        config.Groups.Add(new CommandGroup { Name = _newGroupName.Trim() });
                        plugin.Config.Save();
                        _newGroupName = string.Empty;
                    }
                }

                theme.SpacerY(1f);

                if (config.Groups.Count == 0)
                {
                    ImGui.TextColored(theme.MutedText, "No groups yet. Create one above.");
                    return;
                }

                ImGui.Separator();
                theme.SpacerY(0.5f);

                foreach (var group in config.Groups)
                {
                    var cmdsInGroup = config.Commands.Where(c => string.Equals(c.Group, group.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                    int enabledInGroup = cmdsInGroup.Count(c => c.IsEnabled);
                    int totalInGroup = cmdsInGroup.Count;
                    int enabledTotal = config.Commands.Count(c => c.IsEnabled);

                    ImGui.PushID($"group-{group.Name}");

                    // Group header row
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted($"{group.Name}");
                    ImGui.SameLine();
                    ImGui.TextColored(theme.MutedText, $"({enabledInGroup}/{totalInGroup} enabled)");

                    ImGui.SameLine();
                    float rightEdge = ImGui.GetContentRegionAvail().X;
                    float btnW = 70f * ImGuiHelpers.GlobalScale;
                    float delW = 28f * ImGuiHelpers.GlobalScale;
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + rightEdge - btnW * 2 - delW - 16f * ImGuiHelpers.GlobalScale);

                    // Enable All
                    int canEnable = cmdsInGroup.Count(c => !c.IsEnabled);
                    using (ImRaii.Disabled(canEnable == 0 || enabledTotal + canEnable > 98))
                    {
                        if (theme.PrimaryButton("Enable All", new Vector2(btnW, 0)))
                        {
                            foreach (var cmd in cmdsInGroup)
                                cmd.IsEnabled = true;
                            plugin.Config.Save();
                            SyncCommandsWithDiscord();
                        }
                    }
                    if (canEnable > 0 && enabledTotal + canEnable > 98 && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip($"Would exceed 98 command limit ({enabledTotal} + {canEnable} = {enabledTotal + canEnable}).");

                    ImGui.SameLine();

                    // Disable All
                    using (ImRaii.Disabled(enabledInGroup == 0))
                    {
                        if (theme.SecondaryButton("Disable All", new Vector2(btnW, 0)))
                        {
                            foreach (var cmd in cmdsInGroup)
                                cmd.IsEnabled = false;
                            plugin.Config.Save();
                            SyncCommandsWithDiscord();
                        }
                    }

                    ImGui.SameLine();

                    // Delete group
                    if (theme.DangerIconButton($"##del-group", FontAwesomeIcon.Trash, "Delete group"))
                    {
                        deleteGroup = group.Name;
                    }

                    ImGui.PopID();
                    theme.SpacerY(0.5f);
                    ImGui.Separator();
                    theme.SpacerY(0.5f);
                }
            },
            enabled: ref dummy,
            showCheckbox: false
        );

        if (deleteGroup != null)
        {
            // Unassign commands from the deleted group
            foreach (var cmd in config.Commands.Where(c => string.Equals(c.Group, deleteGroup, StringComparison.OrdinalIgnoreCase)))
                cmd.Group = string.Empty;
            config.Groups.RemoveAll(g => string.Equals(g.Name, deleteGroup, StringComparison.OrdinalIgnoreCase));
            plugin.Config.Save();
        }
    }

    // ─── Commands Tab ───────────────────────────────────────────────────

    private void DrawCommandsList()
    {
        var config = plugin.Config.SlashCommands;

        if (!config.Enabled)
        {
            theme.SpacerY(2f);
            ImGui.TextColored(theme.MutedText, "Enable Slash Commands in the Settings tab first.");
            return;
        }

        // Command editor (add/edit)
        if (_isAddingNew || _editingCommandIndex.HasValue)
        {
            DrawCommandEditor();
            return;
        }

        // Enabled command count indicator
        int enabledCount = config.Commands.Count(c => c.IsEnabled);
        var countColor = enabledCount >= 98 ? UiTheme.ColorDangerText : theme.MutedText;
        ImGui.TextColored(countColor, $"Enabled Commands: {enabledCount} / 98");
        ImGui.TextColored(theme.MutedText, $"Emotes are enabled by default!");
        if (enabledCount >= 98)
        {
            ImGui.SameLine();
            ImGui.TextColored(UiTheme.ColorDangerText, " — Limit reached! Disable a command to enable another.");
        }
        theme.SpacerY(0.5f);

        // --- All commands (user + emote), grouped ---
        var allCommands = config.Commands
            .Select((cmd, idx) => (cmd, idx))
            .OrderByDescending(x => x.cmd.IsEnabled)
            .ToList();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(_userSearchFilter))
        {
            var filter = _userSearchFilter.ToLower();
            allCommands = allCommands.Where(x =>
                x.cmd.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                x.cmd.GameCommand.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (x.cmd.Description ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (x.cmd.Group ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Split into grouped and ungrouped
        var grouped = allCommands
            .Where(x => !string.IsNullOrEmpty(x.cmd.Group))
            .GroupBy(x => x.cmd.Group, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key)
            .ToList();

        var ungroupedUser = allCommands.Where(x => string.IsNullOrEmpty(x.cmd.Group)).ToList();

        // Draw grouped sections (mixed user + emote commands)
        foreach (var group in grouped)
        {
            DrawCommandGroup(config, group.Key, group.ToList());
            theme.SpacerY(0.5f);
        }

        // Draw ungrouped user commands
        if (ungroupedUser.Count > 0 || grouped.Count == 0)
        {
            DrawUserCommandsTable(config, ungroupedUser, grouped.Count > 0 ? "Ungrouped" : null);
        }
    }

    private void DrawCommandGroup(SlashCommandConfig config, string groupName, List<(CustomSlashCommand cmd, int idx)> commands)
    {
        int enabledInGroup = commands.Count(x => x.cmd.IsEnabled);
        int enabledTotal = config.Commands.Count(c => c.IsEnabled);
        bool isCollapsed = _collapsedGroups.Contains(groupName);

        var headers = new[] { "", "Command", "Game Command", "Actions" };
        Action setupCols = () =>
        {
            ImGui.TableSetupColumn("##enabled", ImGuiTableColumnFlags.WidthFixed, 30f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthFixed, 140f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Game Command", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 80f * ImGuiHelpers.GlobalScale);
        };

        int? deleteIndex = null;

        Action<(CustomSlashCommand cmd, int idx), int> drawRow = (item, _) =>
        {
            DrawGroupedCommandRow(config, item, ref deleteIndex);
        };

        theme.DrawCollapsableCardWithTable(
            id: $"group-{groupName}",
            title: $"{groupName} ({enabledInGroup}/{commands.Count} enabled)",
            expanded: ref isCollapsed,
            collection: commands,
            drawRow: drawRow,
            headers: headers,
            setupColumns: setupCols,
            showCount: false,
            showHeaders: true,
            collapsible: true,
            drawHeaderRight: () =>
            {
                int canEnable = commands.Count(x => !x.cmd.IsEnabled);

                using (ImRaii.Disabled(canEnable == 0 || enabledTotal + canEnable > 98))
                {
                    if (theme.SecondaryIconButton($"##enall-{groupName}", FontAwesomeIcon.ToggleOn, "Enable all in group"))
                    {
                        foreach (var (cmd, _) in commands)
                            cmd.IsEnabled = true;
                        plugin.Config.Save();
                        SyncCommandsWithDiscord();
                    }
                }
                if (canEnable > 0 && enabledTotal + canEnable > 98 && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip($"Would exceed 98 command limit.");

                ImGui.SameLine();

                using (ImRaii.Disabled(enabledInGroup == 0))
                {
                    if (theme.SecondaryIconButton($"##disall-{groupName}", FontAwesomeIcon.ToggleOff, "Disable all in group"))
                    {
                        foreach (var (cmd, _) in commands)
                            cmd.IsEnabled = false;
                        plugin.Config.Save();
                        SyncCommandsWithDiscord();
                    }
                }
            }
        );

        if (isCollapsed)
            _collapsedGroups.Add(groupName);
        else
            _collapsedGroups.Remove(groupName);

        if (deleteIndex.HasValue)
        {
            config.Commands.RemoveAt(deleteIndex.Value);
            plugin.Config.Save();
            SyncCommandsWithDiscord();
        }
    }

    /// <summary>
    /// Draws a row for a command inside a group section (works for both user and emote commands).
    /// </summary>
    private void DrawGroupedCommandRow(SlashCommandConfig config, (CustomSlashCommand cmd, int idx) item, ref int? deleteIndex)
    {
        var (cmd, realIdx) = item;
        int enabledCount = config.Commands.Count(c => c.IsEnabled);

        // Enable/disable checkbox
        bool isEnabled = cmd.IsEnabled;
        using (ImRaii.Disabled(!isEnabled && enabledCount >= 98))
        {
            if (ImGui.Checkbox($"##en-grp-{realIdx}", ref isEnabled))
            {
                cmd.IsEnabled = isEnabled;
                plugin.Config.Save();
                SyncCommandsWithDiscord();
            }
        }
        if (!isEnabled && enabledCount >= 98 && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Command limit reached (100). Disable another command first.");

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        if (!cmd.IsEnabled) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
        ImGui.TextUnformatted($"/{cmd.Name}");
        if (ImGui.IsItemHovered())
        {
            var desc = (cmd.Description ?? "").Replace(" [Cordi]", "");
            if (cmd.IsEmote && !string.IsNullOrEmpty(desc))
                ImGui.SetTooltip(desc);
            else if (!string.IsNullOrEmpty(cmd.Description))
                ImGui.SetTooltip(cmd.Description);
        }

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(cmd.GameCommand);
        if (!cmd.IsEnabled) ImGui.PopStyleVar();

        ImGui.TableNextColumn();
        if (theme.SecondaryIconButton($"##edit-grp-{realIdx}", FontAwesomeIcon.Pen, "Edit"))
        {
            _editingCommandIndex = realIdx;
            _editState = CloneCommand(cmd);
            _isAddingNew = false;
        }
        ImGui.SameLine();
        if (theme.DangerIconButton($"##del-grp-{realIdx}", FontAwesomeIcon.Trash, "Delete"))
        {
            deleteIndex = realIdx;
        }
    }

    private void DrawUserCommandsTable(SlashCommandConfig config, List<(CustomSlashCommand cmd, int idx)> userCommands, string? title = null)
    {
        var headers = new[] { "", "Command", "Game Command", "Params", "Actions" };
        Action setupCols = () =>
        {
            ImGui.TableSetupColumn("##enabled", ImGuiTableColumnFlags.WidthFixed, 30f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthFixed, 140f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Game Command", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Params", ImGuiTableColumnFlags.WidthFixed, 50f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 80f * ImGuiHelpers.GlobalScale);
        };

        int? deleteIndex = null;

        Action<(CustomSlashCommand cmd, int idx), int> drawRow = (item, _) =>
        {
            DrawUserCommandRow(config, item, ref deleteIndex);
        };

        bool expanded = true;
        theme.DrawCollapsableCardWithTable(
            id: "slashCommandsList",
            title: title ?? "Custom Slash Commands",
            expanded: ref expanded,
            collection: userCommands,
            drawRow: drawRow,
            headers: headers,
            setupColumns: setupCols,
            showCount: true,
            showHeaders: true,
            collapsible: title != null,
            mutedText: title == null ? "Define slash commands that map to in-game commands." : null,
            drawTopContent: (width) =>
            {
                ImGui.SetNextItemWidth(width);
                ImGui.InputTextWithHint("##userCmdSearch", "Search commands...", ref _userSearchFilter, 64);
            },
            drawFooter: (width) =>
            {
                float btnWidth = width * 0.95f;
                float avail = ImGui.GetContentRegionAvail().X;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail - btnWidth) * 0.5f);
                if (theme.SecondaryButton("Add New Command", new Vector2(btnWidth, 0)))
                {
                    _isAddingNew = true;
                    _editingCommandIndex = null;
                    _editState = new CustomSlashCommand();
                }
            }
        );

        if (deleteIndex.HasValue)
        {
            config.Commands.RemoveAt(deleteIndex.Value);
            plugin.Config.Save();
            SyncCommandsWithDiscord();
        }
    }

    private void DrawUserCommandRow(SlashCommandConfig config, (CustomSlashCommand cmd, int idx) item, ref int? deleteIndex)
    {
        var (cmd, realIdx) = item;
        int enabledCount = config.Commands.Count(c => c.IsEnabled);

        // Enable/disable checkbox
        bool isEnabled = cmd.IsEnabled;
        using (ImRaii.Disabled(!isEnabled && enabledCount >= 98))
        {
            if (ImGui.Checkbox($"##en-cmd-{realIdx}", ref isEnabled))
            {
                cmd.IsEnabled = isEnabled;
                plugin.Config.Save();
                SyncCommandsWithDiscord();
            }
        }
        if (!isEnabled && enabledCount >= 98 && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Command limit reached (100). Disable another command first.");

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        if (!cmd.IsEnabled) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
        ImGui.TextUnformatted($"/{cmd.Name}");
        if (ImGui.IsItemHovered())
        {
            var tooltip = string.IsNullOrEmpty(cmd.Description) ? "" : cmd.Description;
            if (!string.IsNullOrEmpty(cmd.Group))
                tooltip = (string.IsNullOrEmpty(tooltip) ? "" : tooltip + "\n") + $"Group: {cmd.Group}";
            if (!string.IsNullOrEmpty(tooltip))
                ImGui.SetTooltip(tooltip);
        }

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(cmd.GameCommand);

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted((cmd.Parameters?.Count ?? 0).ToString());
        if (!cmd.IsEnabled) ImGui.PopStyleVar();

        ImGui.TableNextColumn();
        if (theme.SecondaryIconButton($"##edit-cmd-{realIdx}", FontAwesomeIcon.Pen, "Edit"))
        {
            _editingCommandIndex = realIdx;
            _editState = CloneCommand(cmd);
            _isAddingNew = false;
        }
        ImGui.SameLine();
        if (theme.DangerIconButton($"##del-cmd-{realIdx}", FontAwesomeIcon.Trash, "Delete"))
        {
            deleteIndex = realIdx;
        }
    }

    // ─── Command Editor ─────────────────────────────────────────────────

    private void DrawCommandEditor()
    {
        if (_editState == null) return;

        string cardTitle = _isAddingNew ? "New Command" : $"Edit: /{_editState.Name}";
        bool dummy = true;

        theme.DrawPluginCardAuto(
            id: "cmd-editor",
            title: cardTitle,
            drawContent: (avail) =>
            {
                // Command Name
                ImGui.TextColored(theme.MutedText, "Command Name:");
                string name = _editState.Name;
                ImGui.SetNextItemWidth(avail);
                if (ImGui.InputTextWithHint("##cmdName", "e.g. dance (lowercase, no spaces)", ref name, 32))
                {
                    _editState.Name = SanitizeCommandName(name);
                }

                theme.SpacerY(0.5f);

                // Description
                ImGui.TextColored(theme.MutedText, "Description:");
                string desc = _editState.Description;
                ImGui.SetNextItemWidth(avail);
                if (ImGui.InputTextWithHint("##cmdDesc", "What this command does", ref desc, 100))
                {
                    _editState.Description = desc;
                }

                theme.SpacerY(0.5f);

                // Game Command
                ImGui.TextColored(theme.MutedText, "Game Command:");
                string gameCmd = _editState.GameCommand;
                ImGui.SetNextItemWidth(avail);
                if (ImGui.InputTextWithHint("##cmdGameCmd", "e.g. /dance or /echo {message}", ref gameCmd, 256))
                {
                    _editState.GameCommand = gameCmd;
                }
                ImGui.TextColored(theme.MutedText, "Use {paramname} to insert parameter values.");

                theme.SpacerY(0.5f);

                // Group picker
                DrawGroupPicker(avail);

                theme.SpacerY(1f);
                ImGui.Separator();
                theme.SpacerY(1f);

                // Parameters
                DrawParameterEditor(avail);

                theme.SpacerY(1f);
                ImGui.Separator();
                theme.SpacerY(1f);

                // Validation
                string? validationError = ValidateCommand(_editState);

                // Save / Cancel buttons
                float btnWidth = avail * 0.48f;
                using (ImRaii.Disabled(validationError != null))
                {
                    if (theme.PrimaryButton(_isAddingNew ? "Add Command" : "Save Changes", new Vector2(btnWidth, 0)))
                    {
                        SaveCommand();
                    }
                }
                if (validationError != null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip(validationError);

                ImGui.SameLine();
                if (theme.SecondaryButton("Cancel", new Vector2(btnWidth, 0)))
                {
                    CancelEdit();
                }

                if (validationError != null)
                {
                    theme.SpacerY(0.5f);
                    ImGui.TextColored(UiTheme.ColorDangerText, validationError);
                }
            },
            enabled: ref dummy,
            showCheckbox: false
        );
    }

    private void DrawGroupPicker(float avail)
    {
        if (_editState == null) return;

        var groups = plugin.Config.SlashCommands.Groups;
        ImGui.TextColored(theme.MutedText, "Group (optional):");

        string currentGroup = _editState.Group ?? string.Empty;
        string previewLabel = string.IsNullOrEmpty(currentGroup) ? "None" : currentGroup;

        ImGui.SetNextItemWidth(avail);
        if (ImGui.BeginCombo("##cmdGroup", previewLabel))
        {
            // "None" option
            if (ImGui.Selectable("None", string.IsNullOrEmpty(currentGroup)))
            {
                _editState.Group = string.Empty;
            }

            foreach (var group in groups)
            {
                bool selected = string.Equals(currentGroup, group.Name, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable(group.Name, selected))
                {
                    _editState.Group = group.Name;
                }
            }

            ImGui.EndCombo();
        }
    }

    private void DrawParameterEditor(float avail)
    {
        if (_editState == null) return;

        ImGui.TextUnformatted($"Parameters ({_editState.Parameters.Count})");

        if (_editState.Parameters.Count > 0)
        {
            int? deleteParamIdx = null;

            using (var table = ImRaii.Table("##paramsTable", 4, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp))
            {
                if (table)
                {
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 120f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Required", ImGuiTableColumnFlags.WidthFixed, 60f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("##actions", ImGuiTableColumnFlags.WidthFixed, 35f * ImGuiHelpers.GlobalScale);
                    ImGui.TableHeadersRow();

                    for (int i = 0; i < _editState.Parameters.Count; i++)
                    {
                        var param = _editState.Parameters[i];
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        string pName = param.Name;
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.InputTextWithHint($"##pname-{i}", "name", ref pName, 32))
                        {
                            param.Name = SanitizeCommandName(pName);
                        }

                        ImGui.TableNextColumn();
                        string pDesc = param.Description;
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.InputTextWithHint($"##pdesc-{i}", "description", ref pDesc, 100))
                        {
                            param.Description = pDesc;
                        }

                        ImGui.TableNextColumn();
                        bool pReq = param.Required;
                        if (ImGui.Checkbox($"##preq-{i}", ref pReq))
                        {
                            param.Required = pReq;
                        }
                        theme.HoverHandIfItem();

                        ImGui.TableNextColumn();
                        if (theme.DangerIconButton($"##pdel-{i}", FontAwesomeIcon.Trash, "Remove"))
                        {
                            deleteParamIdx = i;
                        }
                    }
                }
            }

            if (deleteParamIdx.HasValue)
            {
                _editState.Parameters.RemoveAt(deleteParamIdx.Value);
            }
        }

        theme.SpacerY(0.5f);
        float btnWidth = avail * 0.5f;
        float contentAvail = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (contentAvail - btnWidth) * 0.5f);
        if (theme.SecondaryButton("Add Parameter", new Vector2(btnWidth, 0)))
        {
            _editState.Parameters.Add(new SlashCommandParameter());
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    private void SaveCommand()
    {
        if (_editState == null) return;

        var commands = plugin.Config.SlashCommands.Commands;

        // When adding a new enabled command, check the 99 limit (1 reserved for /cordi)
        if (_isAddingNew && _editState.IsEnabled)
        {
            int enabledCount = commands.Count(c => c.IsEnabled);
            if (enabledCount >= 98)
            {
                SetStatus("Error: Command limit reached (100). Disable another command first.");
                return;
            }
        }

        if (_isAddingNew)
        {
            commands.Add(_editState);
        }
        else if (_editingCommandIndex.HasValue && _editingCommandIndex.Value < commands.Count)
        {
            commands[_editingCommandIndex.Value] = _editState;
        }

        plugin.Config.Save();

        // Auto-register the command with Discord immediately
        var savedCommand = _editState;
        if (plugin.Config.SlashCommands.Enabled && plugin.SlashCommandService != null)
        {
            Task.Run(async () =>
            {
                try
                {
                    await plugin.SlashCommandService.RegisterSingleCommandAsync(savedCommand);
                    plugin.NotificationManager.Add("Slash Commands",
                        $"/{savedCommand.Name} registered with Discord.", CordiNotificationType.Success);
                }
                catch (Exception ex)
                {
                    plugin.NotificationManager.Add("Slash Commands",
                        $"Saved locally but Discord registration failed: {ex.Message}", CordiNotificationType.Warning);
                }
            });
        }

        CancelEdit();
    }

    private void CancelEdit()
    {
        _editState = null;
        _editingCommandIndex = null;
        _isAddingNew = false;
    }

    private static string? ValidateCommand(CustomSlashCommand cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name))
            return "Command name is required.";

        if (cmd.Name.Length < 1 || cmd.Name.Length > 32)
            return "Command name must be 1-32 characters.";

        if (cmd.Name.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
            return "Command name can only contain letters, digits, hyphens, and underscores.";

        if (string.IsNullOrWhiteSpace(cmd.GameCommand))
            return "Game command is required.";

        if ((cmd.Description?.Length ?? 0) > 100)
            return "Description must be 100 characters or less.";

        if (cmd.Parameters != null)
        {
            foreach (var param in cmd.Parameters)
            {
                if (string.IsNullOrWhiteSpace(param.Name))
                    return "All parameters must have a name.";

                if (param.Name.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
                    return $"Parameter '{param.Name}' has invalid characters.";
            }

            // Check for duplicate parameter names
            var paramNames = cmd.Parameters.Select(p => p.Name.ToLower()).ToList();
            if (paramNames.Count != paramNames.Distinct().Count())
                return "Parameter names must be unique.";
        }

        return null;
    }

    private static string SanitizeCommandName(string name)
    {
        return new string(name.ToLower().Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
    }

    private static CustomSlashCommand CloneCommand(CustomSlashCommand source)
    {
        return new CustomSlashCommand
        {
            Name = source.Name,
            Description = source.Description,
            GameCommand = source.GameCommand,
            IsEmote = source.IsEmote,
            IsEnabled = source.IsEnabled,
            Group = source.Group,
            Parameters = (source.Parameters ?? new()).Select(p => new SlashCommandParameter
            {
                Name = p.Name,
                Description = p.Description,
                Required = p.Required,
            }).ToList()
        };
    }

    private void SyncCommandsWithDiscord()
    {
        if (!plugin.Config.SlashCommands.Enabled || plugin.SlashCommandService == null) return;
        Task.Run(async () =>
        {
            try { await plugin.SlashCommandService.RegisterCommandsAsync(); }
            catch (Exception ex) { plugin.LogService.Error("SlashCommands", $"Sync failed: {ex.Message}"); }
        });
    }

    private void SetStatus(string message)
    {
        _statusMessage = message;
        _statusMessageExpiry = DateTime.Now.AddSeconds(5);
    }
}
