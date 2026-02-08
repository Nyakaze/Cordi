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

    private string searchExaminedText = string.Empty;
    private string viewingGlamourPlayer = string.Empty;
    private PlayerGlamour? viewingGlamour = null;

    private bool showRememberedPlayers = true;
    private bool showExaminedPlayers = false;

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

        DrawPlayersCard();

        theme.SpacerY(1f);
        ImGui.Separator();
        theme.SpacerY(1f);

        DrawExaminedCard();

        if (!string.IsNullOrEmpty(viewingGlamourPlayer) && viewingGlamour != null)
        {
            DrawGlamourWindow();
        }
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

                bool examineEnabled = plugin.Config.RememberMe.EnableExamineFeature;
                if (ImGui.Checkbox("Enable Examine Feature", ref examineEnabled))
                {
                    plugin.Config.RememberMe.EnableExamineFeature = examineEnabled;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();
                ImGui.SameLine();
                ImGui.TextColored(UiTheme.ColorDangerText, " (WIP)");

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
                var partyList = Service.PartyList;

                if (partyList == null || partyList.Length == 0)
                {
                    ImGui.TextDisabled("Not in a party.");
                    return;
                }

                ImGui.Text($"Party size: {partyList.Length}/8");
                theme.SpacerY(0.5f);

                if (ImGui.BeginTable("##currentPartyTable", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.RowBg))
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

    private void DrawExaminedCard()
    {
        var players = plugin.RememberMe.GetAllExaminedPlayers();

        if (!string.IsNullOrWhiteSpace(searchExaminedText))
        {
            players = plugin.RememberMe.SearchExaminedPlayers(searchExaminedText);
        }

        Action<RememberedPlayerEntry> onDelete = (player) =>
        {
            plugin.RememberMe.RemoveExaminedPlayer(player.Name, player.World);
        };

        Action<RememberedPlayerEntry> onShowGlamour = (player) =>
        {
            viewingGlamourPlayer = player.FullName;
            viewingGlamour = player.Glamour;
        };

        theme.DrawPlayerTable(
            "rememberme-examined",
            "Examined Players",
            ref showExaminedPlayers,
            players,
            onDelete,
            null, // No note editing
            onShowGlamour,
            null, // No footer
            searchExaminedText,
            (val) => searchExaminedText = val
        );
    }

    private void DrawGlamourWindow()
    {
        bool open = true;
        ImGui.SetNextWindowSize(new Vector2(300, 400), ImGuiCond.FirstUseEver);
        if (ImGui.Begin($"Glamour: {viewingGlamourPlayer}###glamour_window", ref open))
        {
            if (!open)
            {
                viewingGlamourPlayer = string.Empty;
                viewingGlamour = null;
            }
            else
            {
                if (ImGui.Button("Close"))
                {
                    viewingGlamourPlayer = string.Empty;
                    viewingGlamour = null;
                    ImGui.End();
                    return;
                }

                ImGui.Separator();

                if (viewingGlamour != null)
                {
                    ImGui.Text($"Captured: {viewingGlamour.CapturedAt}");
                    theme.SpacerY(0.5f);

                    DrawGlamourItem("Main Hand", viewingGlamour.MainHand);
                    DrawGlamourItem("Off Hand", viewingGlamour.OffHand);
                    DrawGlamourItem("Head", viewingGlamour.Head);
                    DrawGlamourItem("Body", viewingGlamour.Body);
                    DrawGlamourItem("Hands", viewingGlamour.Hands);
                    DrawGlamourItem("Legs", viewingGlamour.Legs);
                    DrawGlamourItem("Feet", viewingGlamour.Feet);
                    DrawGlamourItem("Ears", viewingGlamour.Ears);
                    DrawGlamourItem("Neck", viewingGlamour.Neck);
                    DrawGlamourItem("Wrists", viewingGlamour.Wrists);
                    DrawGlamourItem("Left Ring", viewingGlamour.LeftRing);
                    DrawGlamourItem("Right Ring", viewingGlamour.RightRing);
                }
            }
            ImGui.End();
        }
        else
        {
            // If Begin returns false but open is true, it means collapsed, just end.
            ImGui.End();
            if (!open)
            {
                viewingGlamourPlayer = string.Empty;
                viewingGlamour = null;
            }
        }
    }

    private void DrawGlamourItem(string slotName, PlayerGlamour.GearItem item)
    {
        if (item.ItemId == 0) return;

        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), slotName);
        ImGui.SameLine();

        var itemSheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        var itemData = itemSheet?.GetRow(item.ItemId);
        // If name exists use it, otherwise show Item #ID
        string itemName = itemData?.Name.ToString() ?? "";
        bool isValidItem = !string.IsNullOrWhiteSpace(itemName);

        if (isValidItem)
        {
            if (ImGui.Button($"{itemName}##{slotName}_{item.ItemId}"))
            {
                try
                {
                    var payload = new Dalamud.Game.Text.SeStringHandling.Payloads.ItemPayload(item.ItemId, item.IsHq);
                    var text = new Dalamud.Game.Text.SeStringHandling.Payloads.TextPayload($"{itemName}");
                    var seString = new Dalamud.Game.Text.SeStringHandling.SeString(payload, text, Dalamud.Game.Text.SeStringHandling.Payloads.RawPayload.LinkTerminator);
                    Service.Chat.Print(seString);
                }
                catch { }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"Click to print item link. (ID: {item.ItemId})");
            }
        }
        else
        {
            ImGui.Text($"Unknown Item (ID: {item.ItemId})");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Could not resolve Item ID to Name. Might be a Model ID.");
        }
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
