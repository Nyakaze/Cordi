using System;
using System.Numerics;
using Cordi.Extensions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Themes;
using Cordi.Configuration;
using Dalamud.Interface.Components;

public sealed partial class UiTheme
{
    private Dictionary<string, (string Key, string Value)> _playerNoteEditStates = new();
    private Dictionary<string, (string NameWorld, string Note, bool FirstFrame)> _playerAddStates = new();

    public void DrawPlayerTable(
        string id,
        string title,
        ref bool expanded,
        IEnumerable<RememberedPlayerEntry> players,
        Action<RememberedPlayerEntry> onDelete,
        Action<RememberedPlayerEntry, string>? onSaveNote = null,
        Action<RememberedPlayerEntry>? onShowGlamour = null,
        Action<float>? drawFooter = null,
        string search = "",
        Action<string>? onSearch = null,
        Action<string, string>? onAdd = null,
        string emptyText = "No players found.",
        float maxTableHeight = 350)
    {
        var headers = new[] { "Player", "Last Seen", "Actions" };

        Action setupColumns = () =>
        {
            ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Last Seen", ImGuiTableColumnFlags.WidthFixed, 220f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, ScaledActionsWidth);
        };

        Action<RememberedPlayerEntry, int> drawRow = (player, idx) =>
        {
            // Column 1: Player Name & Notes/Info
            ImGui.TableSetColumnIndex(0);
            ImGui.Text(player.FullName);

            string editKey = $"{id}_{player.FullName}";
            bool isEditing = _playerNoteEditStates.TryGetValue(editKey, out var state);

            if (isEditing)
            {
                // Edit Note Input
                float inputWidth = ImGui.GetContentRegionAvail().X;
                string currentNote = state.Value;
                ImGui.SetNextItemWidth(inputWidth);
                if (ImGui.InputTextMultiline($"##editNote_{editKey}", ref currentNote, 1000, new Vector2(-1, 60)))
                {
                    _playerNoteEditStates[editKey] = (state.Key, currentNote);
                }
                HoverHandIfItem();
            }
            else
            {
                // Display Note if present
                if (onSaveNote != null && !string.IsNullOrWhiteSpace(player.Notes))
                {
                    ImGui.TextDisabled("Notes: ");
                    ImGui.SameLine();
                    ImGui.TextDisabled(player.Notes);
                }
            }

            // Column 2: Last Seen
            ImGui.TableSetColumnIndex(1);
            ImGui.TextDisabled(player.GetLastSeenRelative());

            // Column 3: Actions
            ImGui.TableSetColumnIndex(2);

            if (isEditing)
            {
                if (SuccessIconButton($"##save_{editKey}", FontAwesomeIcon.Check, "Save"))
                {
                    onSaveNote?.Invoke(player, state.Value);
                    _playerNoteEditStates.Remove(editKey);
                }

                ImGui.SameLine();

                if (SecondaryIconButton($"##cancel_{editKey}", FontAwesomeIcon.Times, "Cancel"))
                {
                    _playerNoteEditStates.Remove(editKey);
                }
            }
            else
            {
                bool shownAction = false;

                if (onSaveNote != null)
                {
                    if (SecondaryIconButton($"##edit_{editKey}", FontAwesomeIcon.Edit, "Edit Note"))
                    {
                        _playerNoteEditStates[editKey] = (player.FullName, player.Notes);
                    }
                    shownAction = true;
                }
                else if (onShowGlamour != null && player.Glamour != null)
                {
                    if (SecondaryIconButton($"##glamour_{editKey}", FontAwesomeIcon.Tshirt, "Show Glamour"))
                    {
                        onShowGlamour(player);
                    }
                    shownAction = true;
                }

                if (shownAction) ImGui.SameLine();

                if (DangerIconButton($"##del_{editKey}", FontAwesomeIcon.Trash, "Delete"))
                {
                    onDelete(player);
                    if (isEditing) _playerNoteEditStates.Remove(editKey);
                }
            }
        };

        Action? extraRows = null;
        bool isAdding = _playerAddStates.ContainsKey(id);

        if (isAdding)
        {
            extraRows = () =>
            {
                var s = _playerAddStates[id];
                string newNameWorld = s.NameWorld;
                string newNote = s.Note;

                ImGui.TableNextRow();

                // Column 0: Name and Note Input
                ImGui.TableSetColumnIndex(0);
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

                if (s.FirstFrame)
                {
                    ImGui.SetKeyboardFocusHere();
                    ImGui.SetScrollHereY(1.0f);
                    _playerAddStates[id] = (newNameWorld, newNote, false);
                }

                if (ImGui.InputTextWithHint($"##addName_{id}", "Name@World", ref newNameWorld, 100))
                {
                    _playerAddStates[id] = (newNameWorld, newNote, false);
                }

                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.InputTextWithHint($"##addNote_{id}", "Notes (Optional)...", ref newNote, 1000))
                {
                    _playerAddStates[id] = (newNameWorld, newNote, false);
                }

                // Column 1: Last Seen ("Now")
                ImGui.TableSetColumnIndex(1);
                ImGui.TextDisabled("Now");

                // Column 2: Actions (Save/Cancel)
                ImGui.TableSetColumnIndex(2);
                if (SuccessIconButton($"##saveAdd_{id}", FontAwesomeIcon.Check, "Add Player"))
                {
                    if (!string.IsNullOrWhiteSpace(newNameWorld))
                    {
                        onAdd?.Invoke(newNameWorld, newNote);
                        _playerAddStates.Remove(id);
                    }
                }

                ImGui.SameLine();

                if (SecondaryIconButton($"##cancelAdd_{id}", FontAwesomeIcon.Times, "Cancel"))
                {
                    _playerAddStates.Remove(id);
                }
            };
        }

        Action<float>? internalFooter = drawFooter;
        if (onAdd != null)
        {
            internalFooter = (w) =>
            {
                drawFooter?.Invoke(w);
                if (!isAdding)
                {
                    if (ImGui.Button("Add New Player", new Vector2(w, 0)))
                    {
                        _playerAddStates[id] = ("", "", true);
                    }
                    HoverHandIfItem();
                }
            };
        }

        Action drawSearch = () =>
        {
            string tempSearch = search;
            if (ImGui.InputTextWithHint($"##search_{id}", "Search...", ref tempSearch, 256))
            {
                onSearch?.Invoke(tempSearch);
            }
        };

        DrawCollapsableCardWithTable(
            id,
            title,
            ref expanded,
            players,
            drawRow,
            headers,
            true, // showCount
            null,
            setupColumns,
            true, // showHeaders
            internalFooter,
            (w) => drawSearch(),
            extraRows,
            maxTableHeight
        );
    }

}
