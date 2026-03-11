using System;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Tabs;

public class RememberMeTab : ConfigTabBase
{
    private string searchText = string.Empty;
    private string newPlayerName = string.Empty;
    private string newPlayerWorld = string.Empty;
    private string newPlayerNotes = string.Empty;
    private bool showAddNew = false;

    private string editingPlayerKey = string.Empty;
    private string editingNotes = string.Empty;

    private bool showRememberedPlayers = true;

    public override string Label => "Remember Me";

    public RememberMeTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
    }

    public override void Draw()
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

        DrawPlayersCard();
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
                var partyList = plugin.PartyService.PartyMembers;

                if (partyList == null || partyList.Count == 0)
                {
                    ImGui.TextDisabled("Not in a party.");
                    return;
                }

                ImGui.TextUnformatted("Party size: "); ImGui.SameLine(0, 0); ImGui.TextUnformatted(partyList.Count.ToString()); ImGui.SameLine(0, 0); ImGui.TextUnformatted("/8");
                theme.SpacerY(0.5f);

                using (var table = ImRaii.Table("##currentPartyTable", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.RowBg))
                {
                    if (table)
                    {
                        ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthFixed, 180f);
                        ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthFixed, 60f);
                        ImGui.TableSetupColumn("Notes", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableHeadersRow();

                        for (int i = 0; i < partyList.Count; i++)
                        {
                            var member = partyList[i];
                            if (member == null) continue;

                            var name = member.Name;
                            var world = member.World;

                            ImGui.TableNextRow();

                            ImGui.TableSetColumnIndex(0);
                            ImGui.TextUnformatted(name); ImGui.SameLine(0, 0); ImGui.TextUnformatted("@"); ImGui.SameLine(0, 0); ImGui.TextUnformatted(world);

                            ImGui.TableSetColumnIndex(1);
                            var classJobId = member.JobId;
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
                    }
                }
            }
        );
    }

    private void DrawPlayersCard()
    {


        if (showAddNew)
        {
            theme.SpacerY(0.5f);
            ImGui.Separator();
            theme.SpacerY(0.5f);

            DrawAddNewForm(ImGui.GetContentRegionAvail().X);

            theme.SpacerY(0.5f);
            ImGui.Separator();
            theme.SpacerY(0.5f);
        }

        theme.SpacerY(0.5f);

        var players = string.IsNullOrWhiteSpace(searchText)
            ? plugin.RememberMe.GetAllPlayers()
            : plugin.RememberMe.SearchPlayers(searchText);

        Action<RememberedPlayerEntry> onDelete = (player) =>
        {
            plugin.RememberMe.RemovePlayer(player.Name, player.World);
        };

        Action<RememberedPlayerEntry, string> onSaveNote = (player, note) =>
        {
            plugin.RememberMe.UpdateNotes(player.Name, player.World, note);
        };

        Action<string, string> onAdd = (nameWorld, note) =>
        {
            var parts = nameWorld.Split(new[] { '@', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                string name = parts[0] + " " + parts[1];
                string world = parts.Length > 2 ? parts[2] : parts[1]; // Handle "First Last World" vs "First Last" (ambiguous without world)
                // Actually user probably types "First Last@World" or "First Last World".
                // If "First Last", world is missing.
                // If "First Last World", standard format.
                // Let's assume input is "Name World" or "Name@World".
                // If input "First Last", we might need world.
                // If input "First Last@World", split by @ gives "First Last" and "World".
            }

            // Better parsing logic:
            string pName = nameWorld;
            string pWorld = "";
            if (nameWorld.Contains("@"))
            {
                var s = nameWorld.Split('@');
                pName = s[0].Trim();
                if (s.Length > 1) pWorld = s[1].Trim();
            }
            else
            {
                // Try to find world at end? Or just require @ for explicit world.
                // Or split by spaces.
                var s = nameWorld.Split(' ');
                if (s.Length >= 3)
                {
                    pWorld = s.Last();
                    pName = string.Join(" ", s.Take(s.Length - 1));
                }
                else
                {
                    // Fallback or error?
                    // Verify if world is valid?
                    // For now, let's just try to add.
                    pName = nameWorld; // And world empty?
                }
            }

            if (!string.IsNullOrWhiteSpace(pName) && !string.IsNullOrWhiteSpace(pWorld))
            {
                plugin.RememberMe.AddOrUpdatePlayer(pName, pWorld, null, note);
            }
        };

        theme.DrawPlayerTable(
            "rememberme-players",
            "Remembered Players",
            ref showRememberedPlayers,
            players,
            onDelete,
            onSaveNote,
            null, // No glamour button for remembered list
            null, // No Footer, built-in
            searchText,
            (val) => searchText = val,
            onAdd
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
