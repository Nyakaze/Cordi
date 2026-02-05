using System;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.UI.Themes;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Bindings.ImGui;

namespace Cordi.UI.Tabs;

public class RememberMeTab
{
    private readonly CordiPlugin plugin;
    private readonly UiTheme theme;

    private string searchText = string.Empty;
    private string newPlayerName = string.Empty;
    private string newPlayerWorld = string.Empty;
    private string newPlayerNotes = string.Empty;
    private bool showAddNew = false;

    private string editingPlayerKey = string.Empty;
    private string editingNotes = string.Empty;

    public RememberMeTab(CordiPlugin plugin, UiTheme theme)
    {
        this.plugin = plugin;
        this.theme = theme;
    }

    public void Draw()
    {
        bool enabled = true;

        DrawGeneralCard(ref enabled);

        theme.SpacerY(1f);
        ImGui.Separator();
        theme.SpacerY(1f);

        DrawCurrentPartyCard(ref enabled);

        theme.SpacerY(1f);
        ImGui.Separator();
        theme.SpacerY(1f);

        DrawPlayersCard(ref enabled);
    }

    private void DrawGeneralCard(ref bool cardEnabled)
    {
        theme.SpacerY(2f);

        theme.DrawPluginCardAuto(
            id: "rememberme-general",
            title: "General",
            enabled: ref cardEnabled,
            drawContent: (avail) =>
            {
                bool rememberMeEnabled = plugin.Config.RememberMe.Enabled;
                if (ImGui.Checkbox("Enable Remember Me", ref rememberMeEnabled))
                {
                    plugin.Config.RememberMe.Enabled = rememberMeEnabled;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();
                ImGui.TextDisabled("Automatically track party members and display notes in notifications.");

                theme.SpacerY(0.5f);

                var playerCount = plugin.Config.RememberMe.RememberedPlayers.Count;
                ImGui.Text($"Remembered players: {playerCount}");
            }
        );
    }

    private void DrawCurrentPartyCard(ref bool cardEnabled)
    {
        theme.DrawPluginCardAuto(
            id: "rememberme-currentparty",
            title: "Current Party",
            enabled: ref cardEnabled,
            drawContent: (avail) =>
            {
                var partyList = Service.PartyList;

                if (partyList == null || partyList.Length == 0)
                {
                    ImGui.TextDisabled("Not in a party.");
                    return;
                }

                ImGui.Text($"Party size: {partyList.Length}/8");
                theme.SpacerY(0.5f);

                if (ImGui.BeginTable("##currentPartyTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthFixed, 180f);
                    ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthFixed, 60f);
                    ImGui.TableSetupColumn("Notes", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableHeadersRow();

                    for (int i = 0; i < partyList.Length; i++)
                    {
                        var member = partyList[i];
                        if (member == null) continue;

                        var name = member.Name.ToString();
                        var world = member.World.Value.Name.ToString();

                        ImGui.TableNextRow();

                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text($"{name}@{world}");

                        ImGui.TableSetColumnIndex(1);
                        var classJobId = member.ClassJob.RowId;
                        var classJobSheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>();
                        var classJob = classJobSheet?.GetRow(classJobId);
                        var classJobAbbr = classJob?.Abbreviation.ToString() ?? "?";
                        ImGui.Text(classJobAbbr);

                        ImGui.TableSetColumnIndex(2);
                        var rememberedPlayer = plugin.RememberMe.FindPlayer(name, world);
                        if (rememberedPlayer != null && !string.IsNullOrWhiteSpace(rememberedPlayer.Notes))
                        {
                            ImGui.TextWrapped(rememberedPlayer.Notes);
                        }
                        else
                        {
                            ImGui.TextDisabled("(No notes)");
                        }
                    }

                    ImGui.EndTable();
                }
            }
        );
    }

    private void DrawPlayersCard(ref bool cardEnabled)
    {
        theme.DrawPluginCardAuto(
            id: "rememberme-players",
            title: "Remembered Players",
            enabled: ref cardEnabled,
            drawContent: (avail) =>
            {
                ImGui.SetNextItemWidth(avail * 0.6f);
                if (ImGui.InputTextWithHint("##search", "Search by name, world, or notes...", ref searchText, 256))
                {
                }
                theme.HoverHandIfItem();

                ImGui.SameLine();
                if (ImGui.Button(showAddNew ? "Cancel" : "Add New"))
                {
                    showAddNew = !showAddNew;
                    if (!showAddNew)
                    {
                        newPlayerName = string.Empty;
                        newPlayerWorld = string.Empty;
                        newPlayerNotes = string.Empty;
                    }
                }
                theme.HoverHandIfItem();

                if (showAddNew)
                {
                    theme.SpacerY(0.5f);
                    ImGui.Separator();
                    theme.SpacerY(0.5f);

                    DrawAddNewForm(avail);

                    theme.SpacerY(0.5f);
                    ImGui.Separator();
                    theme.SpacerY(0.5f);
                }

                var players = string.IsNullOrWhiteSpace(searchText)
                    ? plugin.RememberMe.GetAllPlayers()
                    : plugin.RememberMe.SearchPlayers(searchText);

                if (players.Count == 0)
                {
                    theme.SpacerY(1f);
                    ImGui.TextDisabled(string.IsNullOrWhiteSpace(searchText)
                        ? "No remembered players yet. Party members will be tracked automatically."
                        : "No players found matching your search.");
                    return;
                }

                theme.SpacerY(0.5f);

                if (ImGui.BeginTable("##playersTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
                {
                    ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthFixed, 180f);
                    ImGui.TableSetupColumn("Last Seen", ImGuiTableColumnFlags.WidthFixed, 120f);
                    ImGui.TableSetupColumn("Notes", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 80f);
                    ImGui.TableSetupScrollFreeze(0, 1);
                    ImGui.TableHeadersRow();

                    foreach (var player in players)
                    {
                        ImGui.TableNextRow();

                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text(player.FullName);

                        ImGui.TableSetColumnIndex(1);
                        ImGui.TextDisabled(player.GetLastSeenRelative());

                        ImGui.TableSetColumnIndex(2);
                        var playerKey = $"{player.Name}@{player.World}";

                        if (editingPlayerKey == playerKey)
                        {
                            ImGui.SetNextItemWidth(-1);
                            if (ImGui.InputTextMultiline($"##editNotes_{playerKey}", ref editingNotes, 1000, new Vector2(-1, 60)))
                            {
                            }
                            theme.HoverHandIfItem();
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(player.Notes))
                            {
                                ImGui.TextDisabled("(No notes)");
                            }
                            else
                            {
                                ImGui.TextWrapped(player.Notes);
                            }
                        }

                        ImGui.TableSetColumnIndex(3);

                        if (editingPlayerKey == playerKey)
                        {
                            if (ImGuiComponents.IconButton($"##save_{playerKey}", FontAwesomeIcon.Check))
                            {
                                plugin.RememberMe.UpdateNotes(player.Name, player.World, editingNotes);
                                editingPlayerKey = string.Empty;
                                editingNotes = string.Empty;
                            }
                            theme.HoverHandIfItem();
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Save");
                            }

                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton($"##cancel_{playerKey}", FontAwesomeIcon.Times))
                            {
                                editingPlayerKey = string.Empty;
                                editingNotes = string.Empty;
                            }
                            theme.HoverHandIfItem();
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Cancel");
                            }
                        }
                        else
                        {
                            if (ImGuiComponents.IconButton($"##edit_{playerKey}", FontAwesomeIcon.Edit))
                            {
                                editingPlayerKey = playerKey;
                                editingNotes = player.Notes;
                            }
                            theme.HoverHandIfItem();
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Edit notes");
                            }

                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton($"##delete_{playerKey}", FontAwesomeIcon.Trash))
                            {
                                plugin.RememberMe.RemovePlayer(player.Name, player.World);
                            }
                            theme.HoverHandIfItem();
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Delete");
                            }
                        }
                    }

                    ImGui.EndTable();
                }
            }
        );
    }

    private void DrawAddNewForm(float avail)
    {
        ImGui.Text("Add New Player:");
        theme.SpacerY(0.3f);

        ImGui.Text("Name:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##newName", "Character Name", ref newPlayerName, 64);
        theme.HoverHandIfItem();

        ImGui.SameLine();
        theme.SpacerX(1f);

        ImGui.Text("World:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##newWorld", "Server", ref newPlayerWorld, 64);
        theme.HoverHandIfItem();

        theme.SpacerY(0.3f);

        ImGui.Text("Notes:");
        ImGui.SetNextItemWidth(avail);
        ImGui.InputTextMultiline("##newNotes", ref newPlayerNotes, 1000, new Vector2(avail, 60));
        theme.HoverHandIfItem();

        theme.SpacerY(0.3f);

        if (ImGui.Button("Save"))
        {
            if (!string.IsNullOrWhiteSpace(newPlayerName) && !string.IsNullOrWhiteSpace(newPlayerWorld))
            {
                plugin.RememberMe.AddOrUpdatePlayer(newPlayerName.Trim(), newPlayerWorld.Trim(), null, newPlayerNotes);
                newPlayerName = string.Empty;
                newPlayerWorld = string.Empty;
                newPlayerNotes = string.Empty;
                showAddNew = false;
            }
        }
        theme.HoverHandIfItem();

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            newPlayerName = string.Empty;
            newPlayerWorld = string.Empty;
            newPlayerNotes = string.Empty;
            showAddNew = false;
        }
        theme.HoverHandIfItem();
    }
}
