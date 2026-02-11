using System;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
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

                ImGui.Text($"Party size: {partyList.Count}/8");
                theme.SpacerY(0.5f);

                if (ImGui.BeginTable("##currentPartyTable", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.RowBg))
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
                        ImGui.Text($"{name}@{world}");

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
        ImGui.SetNextWindowSize(new Vector2(500, 550), ImGuiCond.FirstUseEver);
        if (ImGui.Begin($"Glamour: {viewingGlamourPlayer}###glamour_window", ref open))
        {
            if (!open)
            {
                viewingGlamourPlayer = string.Empty;
                viewingGlamour = null;
            }
            else
            {
                if (viewingGlamour != null)
                {
                    ImGui.Text($"Captured: {viewingGlamour.CapturedAt}");
                    theme.SpacerY(0.5f);

                    if (ImGui.BeginTable("glamour_table", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
                    {
                        ImGui.TableSetupColumn("Left", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Right", ImGuiTableColumnFlags.WidthStretch);

                        // Row 1: Main / Off
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn(); DrawGlamourItem("Main Hand", viewingGlamour.MainHand);
                        ImGui.TableNextColumn(); DrawGlamourItem("Off Hand", viewingGlamour.OffHand);

                        // Row 2: Head / Ears
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn(); DrawGlamourItem("Head", viewingGlamour.Head);
                        ImGui.TableNextColumn(); DrawGlamourItem("Earrings", viewingGlamour.Ears);

                        // Row 3: Body / Neck
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn(); DrawGlamourItem("Body", viewingGlamour.Body);
                        ImGui.TableNextColumn(); DrawGlamourItem("Necklace", viewingGlamour.Neck);

                        // Row 4: Hands / Wrists
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn(); DrawGlamourItem("Hands", viewingGlamour.Hands);
                        ImGui.TableNextColumn(); DrawGlamourItem("Bracelets", viewingGlamour.Wrists);

                        // Row 5: Legs / Right Ring
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn(); DrawGlamourItem("Legs", viewingGlamour.Legs);
                        ImGui.TableNextColumn(); DrawGlamourItem("Right Ring", viewingGlamour.RightRing);

                        // Row 6: Feet / Left Ring
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn(); DrawGlamourItem("Feet", viewingGlamour.Feet);
                        ImGui.TableNextColumn(); DrawGlamourItem("Left Ring", viewingGlamour.LeftRing);

                        ImGui.EndTable();
                    }
                }
            }
            ImGui.End();
        }
        else
        {
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

        ImGui.BeginGroup();
        // Slot name in muted text
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), slotName);

        var itemSheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        var itemData = itemSheet?.GetRow(item.ItemId);
        string itemName = itemData?.Name.ToString() ?? "";
        ushort iconId = itemData?.Icon ?? 0;
        byte rarity = itemData?.Rarity ?? 1;
        ushort ilvl = (ushort)(itemData?.LevelItem.Value.RowId ?? 0);

        Vector4 rarityColor = GetRarityColor(rarity);

        bool isValidItem = !string.IsNullOrWhiteSpace(itemName);

        if (isValidItem)
        {
            bool iconDrawn = false;
            // Draw Icon with border
            if (iconId > 0)
            {
                try
                {
                    var tex = Service.TextureProvider.GetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(iconId));
                    var wrap = tex.GetWrapOrEmpty();

                    if (wrap != null && wrap.Handle != IntPtr.Zero)
                    {
                        ImGui.PushID($"btn_{slotName}_{item.ItemId}");
                        // Transparent background for button, providing our own border
                        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);

                        // Icon size
                        Vector2 iconSize = new Vector2(40, 40);

                        // Draw border manually
                        var p = ImGui.GetCursorScreenPos();
                        var drawList = ImGui.GetWindowDrawList();

                        if (ImGui.ImageButton(wrap.Handle, iconSize))
                        {
                            TryOnItem(item, slotName);
                        }
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                        {
                            PrintItemLink(item, itemName);
                        }

                        // Draw rarity border
                        drawList.AddRect(p, p + iconSize + new Vector2(ImGui.GetStyle().FramePadding.X * 2, ImGui.GetStyle().FramePadding.Y * 2), ImGui.GetColorU32(rarityColor), 4f, ImDrawFlags.None, 2f);

                        ImGui.PopStyleColor(3);
                        ImGui.PopID();

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip($"{itemName}\nItem Level: {ilvl}\nL-Click: Try On\nR-Click: Link Chat");
                            theme.HoverHandIfItem();
                        }

                        ImGui.SameLine();

                        // Item Details Column
                        ImGui.BeginGroup();
                        ImGui.TextColored(rarityColor, itemName);
                        ImGui.TextDisabled($"iLvl {ilvl}");

                        // Stain / Dye 1
                        if (item.StainId > 0)
                        {
                            var stainSheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Stain>();
                            var stainData = stainSheet?.GetRow(item.StainId);
                            if (stainData != null)
                            {
                                string stainName = stainData.Value.Name.ToString();
                                uint stainColorInt = stainData.Value.Color;
                                stainColorInt |= 0xFF000000;
                                float r = ((stainColorInt >> 16) & 0xFF) / 255f;
                                float g = ((stainColorInt >> 8) & 0xFF) / 255f;
                                float b = (stainColorInt & 0xFF) / 255f;
                                Vector4 stainColorVec = new Vector4(r, g, b, 1f);

                                ImGui.SameLine();
                                ImGui.ColorButton($"##stain1_{item.ItemId}", stainColorVec, ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoDragDrop | ImGuiColorEditFlags.NoPicker, new Vector2(10, 10));
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip($"Dye 1: {stainName}");
                                }
                            }
                        }

                        // Stain / Dye 2
                        if (item.StainId2 > 0)
                        {
                            var stainSheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Stain>();
                            var stainData = stainSheet?.GetRow(item.StainId2);
                            if (stainData != null)
                            {
                                string stainName = stainData.Value.Name.ToString();
                                uint stainColorInt = stainData.Value.Color;
                                stainColorInt |= 0xFF000000;
                                float r = ((stainColorInt >> 16) & 0xFF) / 255f;
                                float g = ((stainColorInt >> 8) & 0xFF) / 255f;
                                float b = (stainColorInt & 0xFF) / 255f;
                                Vector4 stainColorVec = new Vector4(r, g, b, 1f);

                                ImGui.SameLine();
                                ImGui.ColorButton($"##stain2_{item.ItemId}", stainColorVec, ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoDragDrop | ImGuiColorEditFlags.NoPicker, new Vector2(10, 10));
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip($"Dye 2: {stainName}");
                                }
                            }
                        }

                        ImGui.EndGroup();

                        iconDrawn = true;
                    }
                }
                catch { }
            }

            if (!iconDrawn)
            {
                if (ImGui.Button($"{itemName}##{slotName}_{item.ItemId}"))
                {
                    TryOnItem(item, slotName);
                }
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    PrintItemLink(item, itemName);
                }
            }
        }
        else
        {
            ImGui.Text($"Unknown Item (ID: {item.ItemId})");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Could not resolve Item ID to Name. Might be a Model ID.");
        }
        ImGui.EndGroup();
    }

    private Vector4 GetRarityColor(byte rarity)
    {
        return rarity switch
        {
            1 => new Vector4(1f, 1f, 1f, 1f),       // Common (White)
            2 => new Vector4(0.7f, 1f, 0.7f, 1f),   // Uncomon (Green)
            3 => new Vector4(0.4f, 0.6f, 1f, 1f),   // Rare (Blue)
            4 => new Vector4(0.8f, 0.5f, 1f, 1f),   // Relic (Purple)
            7 => new Vector4(1f, 0.5f, 0.8f, 1f),   // Aetherial (Pink)
            _ => new Vector4(1f, 1f, 1f, 1f)
        };
    }

    private unsafe void TryOnItem(PlayerGlamour.GearItem item, string slotName)
    {
        try
        {
            var itemSheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
            var itemData = itemSheet?.GetRow(item.ItemId);

            if (itemData == null) return;

            // EquipSlotCategory.Row seems to match the slot ID expected by TryOn
            var equipSlotCategory = itemData.Value.EquipSlotCategory.RowId;

            // AgentTryon.TryOn(itemId, stainId, equipSlot, 0, 0, isHq)
            // Using EquipSlotCategory as 3rd arg
            Service.Log.Debug($"[Cordi] TryOnItem: ID={item.ItemId} Stain={item.StainId} Slot={equipSlotCategory} (Category)");
            AgentTryon.TryOn(0xFF, item.ItemId, item.StainId, 0, 0);
            // AgentTryon.TryOn(item.ItemId, item.StainId, (byte)equipSlotCategory, 0, 0, item.IsHq);
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Failed to try on item.");
        }
    }

    private void PrintItemLink(PlayerGlamour.GearItem item, string itemName)
    {
        try
        {
            var payload = new Dalamud.Game.Text.SeStringHandling.Payloads.ItemPayload(item.ItemId, item.IsHq);
            var text = new Dalamud.Game.Text.SeStringHandling.Payloads.TextPayload($"{itemName}");
            // Add a link terminator to properly close the item link
            var seString = new Dalamud.Game.Text.SeStringHandling.SeString(
                payload,
                text,
                Dalamud.Game.Text.SeStringHandling.Payloads.RawPayload.LinkTerminator
            );
            Service.Chat.Print(seString);
        }
        catch { }
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
