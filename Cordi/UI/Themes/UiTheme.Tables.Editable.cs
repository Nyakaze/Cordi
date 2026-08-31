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
    private Dictionary<string, (int Index, string Value)> _stringEditStates = new();

    public void DrawStringTable(
        string id,
        string title,
        ref bool expanded,
        IList<string> list,
        Action onListModified,
        bool allowAdd = true,
        string itemName = "Item")
    {

        var headers = new[] { itemName, "Actions" };
        Action setupCols = () =>
        {
            ImGui.TableSetupColumn(itemName, ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, ScaledActionsWidth);
        };

        var postDrawActions = new List<Action>();

        Action<string, int> drawRow = (item, idx) =>
        {
            var editKey = id;
            bool isEditing = _stringEditStates.TryGetValue(editKey, out var state) && state.Index == idx;

            // Item Column
            if (isEditing)
            {
                float inputWidth = ImGui.GetContentRegionAvail().X;
                ImGui.SetNextItemWidth(inputWidth);

                // Focus newly added items
                if (string.IsNullOrEmpty(item) && ImGui.IsWindowAppearing())
                    ImGui.SetKeyboardFocusHere();

                string editValue = state.Value;
                if (ImGui.InputText($"##edit-{id}-{idx}", ref editValue, 256))
                {
                    _stringEditStates[editKey] = (idx, editValue);
                }
            }
            else
            {
                ImGui.Text(item);
            }

            ImGui.TableNextColumn();

            // Actions Column
            if (isEditing)
            {
                if (SuccessIconButton($"##save-{id}-{idx}", FontAwesomeIcon.Check, tooltip: "Save"))
                {
                    list[idx] = _stringEditStates[editKey].Value;
                    _stringEditStates.Remove(editKey);
                    onListModified();
                }

                ImGui.SameLine();

                if (SecondaryIconButton($"##cancel-{id}-{idx}", FontAwesomeIcon.Times, tooltip: "Cancel"))
                {
                    if (string.IsNullOrEmpty(list[idx]))
                    {
                        postDrawActions.Add(() =>
                        {
                            list.RemoveAt(idx);
                            onListModified();
                        });
                    }
                    _stringEditStates.Remove(editKey);
                }
            }
            else
            {

                if (SecondaryIconButton($"##edit-{id}-{idx}", FontAwesomeIcon.Edit, tooltip: "Edit Pattern"))
                {
                    _stringEditStates[editKey] = (idx, item);
                }
                ImGui.SameLine();

                if (DangerIconButton($"##del-{id}-{idx}", FontAwesomeIcon.Trash, tooltip: "Delete Pattern"))
                {
                    postDrawActions.Add(() =>
                    {
                        list.RemoveAt(idx);
                        onListModified();
                        // If we deleted the item being edited, clear state
                        if (_stringEditStates.TryGetValue(editKey, out var s) && s.Index == idx)
                            _stringEditStates.Remove(editKey);
                    });
                }
            }
        };

        Action drawFooter = () =>
        {
            if (allowAdd)
            {
                float avail = ImGui.GetContentRegionAvail().X;
                float width = avail * 0.95f;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((avail - width) * 0.5f));
                if (SecondaryButton($"Add new {itemName}##{id}", new Vector2(width, 0)))
                {
                    list.Add("");
                    _stringEditStates[id] = (list.Count - 1, "");
                    onListModified();
                }

            }
        };

        DrawCollapsableCardWithTable(
            id,
            title,
            ref expanded,
            list,
            drawRow,
            headers,
            setupColumns: setupCols,
            drawFooter: allowAdd ? (w) => drawFooter() : null
        );

        foreach (var action in postDrawActions) action();
    }

    public delegate void DrawDictionaryEditUI(string key, ref string currentValue, Action cancel);

    private Dictionary<string, (string Key, string Value)> _dictEditStates = new();
    private Dictionary<string, (string NewKey, string NewValue)> _dictAddStates = new();

    public void DrawDictionaryTable(
        string id,
        string title,
        ref bool expanded,
        IDictionary<string, string> dictionary,
        Action onModified,
        string[] headers,
        Action? setupColumns = null,
        Func<string, string, string>? getDisplayValue = null,
        DrawDictionaryEditUI? drawEditUI = null,
        Action<float>? drawFooter = null,
        bool allowAdd = false,
        bool collapsible = true)
    {
        var list = dictionary.ToList();
        var postDrawActions = new List<Action>();

        Action<KeyValuePair<string, string>, int> drawRow = (kvp, idx) =>
        {
            var key = kvp.Key;
            var value = kvp.Value;
            var editKey = id;

            bool isEditing = _dictEditStates.TryGetValue(editKey, out var state) && state.Key == key;

            ImGui.AlignTextToFramePadding();
            ImGui.Text(key);

            ImGui.TableNextColumn();

            if (isEditing)
            {
                // If custom edit UI is provided (e.g. for Dropdowns), use it.
                // Otherwise default to InputText.
                // We pass a 'cancel' action to the custom UI if needed.
                string currentEditValue = state.Value;

                if (drawEditUI != null)
                {
                    Action cancelAction = () => { _dictEditStates.Remove(editKey); };
                    drawEditUI(key, ref currentEditValue, cancelAction);

                    if (currentEditValue != state.Value)
                    {
                        _dictEditStates[editKey] = (key, currentEditValue);
                    }
                }
                else
                {
                    float inputWidth = ImGui.GetContentRegionAvail().X;
                    ImGui.SetNextItemWidth(inputWidth);
                    ImGui.InputText($"##edit-{id}-{key}", ref currentEditValue, 512);
                    _dictEditStates[editKey] = (key, currentEditValue);
                }
            }
            else
            {
                string display = getDisplayValue != null ? getDisplayValue(key, value) : value;
                ImGui.AlignTextToFramePadding();
                ImGui.Text(display);
            }

            ImGui.TableNextColumn();

            if (isEditing)
            {
                if (SuccessIconButton($"##save-{id}-{idx}", FontAwesomeIcon.Check, "Save"))
                {
                    dictionary[key] = state.Value;
                    _dictEditStates.Remove(editKey);
                    onModified();
                }

                ImGui.SameLine();

                if (SecondaryIconButton($"##cancel-{id}-{idx}", FontAwesomeIcon.Times, "Cancel"))
                {
                    _dictEditStates.Remove(editKey);
                }
            }
            else
            {
                if (SecondaryIconButton($"##edit-{id}-{idx}", FontAwesomeIcon.Pen, "Edit"))
                {
                    _dictEditStates[editKey] = (key, value);
                    _dictAddStates.Remove(id);
                }

                ImGui.SameLine();

                if (DangerIconButton($"##del-{id}-{idx}", FontAwesomeIcon.Trash, "Delete"))
                {
                    postDrawActions.Add(() =>
                    {
                        dictionary.Remove(key);
                        onModified();
                        if (_dictEditStates.TryGetValue(editKey, out var s) && s.Key == key)
                            _dictEditStates.Remove(editKey);
                    });
                }
            }
        };

        Action<float>? internalFooter = drawFooter;

        if (allowAdd)
        {
            internalFooter = (totalWidth) =>
            {
                if (drawFooter != null) drawFooter(totalWidth);
                float avail = totalWidth;

                if (_dictAddStates.TryGetValue(id, out var addState))
                {
                    float pad = PadX(0.6f);
                    float actionsWidth = ScaledActionsWidth;
                    float inputsTotalWidth = avail - actionsWidth - pad;
                    float keyWidth = inputsTotalWidth * 0.35f;
                    float valWidth = inputsTotalWidth - keyWidth - Gap();

                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + pad);

                    string nKey = addState.NewKey;
                    string nVal = addState.NewValue;

                    using (ImRaii.ItemWidth(keyWidth))
                    {
                        ImGui.InputTextWithHint($"##add-key-{id}", headers.Length > 0 ? headers[0] : "Key", ref nKey, 128);
                    }

                    ImGui.SameLine();
                    using (ImRaii.ItemWidth(valWidth))
                    {
                        ImGui.InputTextWithHint($"##add-val-{id}", headers.Length > 1 ? headers[1] : "Value", ref nVal, 512);
                    }

                    _dictAddStates[id] = (nKey, nVal);

                    ImGui.SameLine();
                    var saved = SuccessIconButton($"##mod-save-{id}", FontAwesomeIcon.Check,
                        dictionary.ContainsKey(nKey) ? "Key already exists" : "Add");
                    if (saved)
                    {
                        if (!string.IsNullOrWhiteSpace(nKey) && !dictionary.ContainsKey(nKey))
                        {
                            dictionary[nKey] = nVal;
                            _dictAddStates.Remove(id);
                            onModified();
                        }
                    }

                    ImGui.SameLine();
                    if (SecondaryIconButton($"##cancel-{id}", FontAwesomeIcon.Times, "Cancel"))
                    {
                        _dictAddStates.Remove(id);
                    }

                    SpacerY(1f);
                }
                float width = avail * 0.95f;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((avail - width) * 0.5f));
                if (SecondaryButton($"Add new {title.Split(' ')[0]} {title.Split(' ')[1][..^2]}", new Vector2(width, 0)))
                {
                    _dictAddStates[id] = ("", "");
                    _dictEditStates.Remove(id);
                }
            }
                ;
        }

        DrawCollapsableCardWithTable(
            id,
            title,
            ref expanded,
            list,
            drawRow,
            headers,
            explicitCount: list.Count,
            setupColumns: setupColumns,
            drawFooter: internalFooter,
            collapsible: collapsible
        );

        foreach (var action in postDrawActions) action();
    }


}
