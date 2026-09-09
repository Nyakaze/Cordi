using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.Configuration;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Tabs;

public sealed class BlacklistFlagColumn<T>
{
    public required string Id { get; init; }
    public required string Tooltip { get; init; }
    public required FontAwesomeIcon Icon { get; init; }
    public required Func<T, bool> Get { get; init; }
    public required Action<T, bool> Set { get; init; }
}

public partial class WatchersTab
{
    private sealed class BlacklistDraft
    {
        public string Name = string.Empty;
        public string World = string.Empty;
    }

    private readonly Dictionary<string, BlacklistDraft> addDrafts = new();
    private readonly Dictionary<string, BlacklistDraft> editDrafts = new();
    private readonly Dictionary<string, int> editIndices = new();

    private BlacklistDraft Draft(Dictionary<string, BlacklistDraft> store, string id)
    {
        if (!store.TryGetValue(id, out var draft))
        {
            draft = new BlacklistDraft();
            store[id] = draft;
        }

        return draft;
    }

    private void DrawBlacklistPanel<T>(
        string id,
        string label,
        string emptyHint,
        List<T> list,
        Func<string, string, T> createEntry,
        IReadOnlyList<BlacklistFlagColumn<T>>? extraColumns = null)
        where T : IBlacklistEntry
    {
        Card.Draw(
            id,
            innerWidth =>
            {
                ImGui.TextColored(theme.MutedText, emptyHint);
                theme.SpacerY(0.5f);

                DrawBlacklistAddRow(id, innerWidth, list, createEntry);
                theme.SpacerY(0.5f);

                if (list.Count == 0)
                {
                    ImGui.TextColored(theme.FaintText, "No characters blacklisted yet.");
                    return;
                }

                int? removeIndex = null;
                bool editing = editIndices.TryGetValue(id, out int editIndex);

                for (int index = 0; index < list.Count; index++)
                {
                    if (editing && editIndex == index)
                    {
                        DrawBlacklistEditRow(id, innerWidth, list[index], index);
                        continue;
                    }

                    if (DrawBlacklistEntryRow(id, innerWidth, list[index], index, extraColumns))
                        removeIndex = index;
                }

                if (removeIndex is not { } target)
                    return;

                list.RemoveAt(target);
                editIndices.Remove(id);
                Save();
            },
            label: label,
            drawTrailing: anchor => DrawCountChip(anchor, list.Count, "player"));
    }

    private void DrawBlacklistAddRow<T>(string id, float width, List<T> list, Func<string, string, T> createEntry)
        where T : IBlacklistEntry
    {
        var draft = Draft(addDrafts, id);

        float buttonWidth = theme.Scaled(80f);
        float rowHeight = theme.Scaled(UiTheme.ControlHeight);
        float fieldsWidth = width - buttonWidth - theme.Gap();
        float nameWidth = fieldsWidth * 0.55f - theme.Gap() * 0.5f;
        float worldWidth = fieldsWidth - nameWidth - theme.Gap();

        var origin = ImGui.GetCursorScreenPos();

        string name = draft.Name;
        if (theme.TextInput($"##{id}-add-name", origin, nameWidth, ref name, 64, "Character name"))
            draft.Name = name;

        string world = draft.World;
        var worldPos = new Vector2(origin.X + nameWidth + theme.Gap(), origin.Y);
        if (theme.TextInput($"##{id}-add-world", worldPos, worldWidth, ref world, 64, "World"))
            draft.World = world;

        ImGui.SetCursorScreenPos(new Vector2(origin.X + width - buttonWidth, origin.Y));
        if (theme.PrimaryButton($"Add##{id}-add", new Vector2(buttonWidth, rowHeight)))
        {
            if (!string.IsNullOrWhiteSpace(draft.Name))
            {
                list.Add(createEntry(draft.Name.Trim(), draft.World.Trim()));
                draft.Name = string.Empty;
                draft.World = string.Empty;
                Save();
            }
        }
        theme.HoverHandIfItem();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private void DrawBlacklistEditRow<T>(string id, float width, T entry, int index)
        where T : IBlacklistEntry
    {
        var draft = Draft(editDrafts, id);

        float actionWidth = theme.Scaled(UiTheme.ActionButtonSize);
        float rowHeight = theme.Scaled(UiTheme.ControlHeight);
        float fieldsWidth = width - actionWidth * 2f - theme.Gap(2f);
        float nameWidth = fieldsWidth * 0.55f - theme.Gap() * 0.5f;
        float worldWidth = fieldsWidth - nameWidth - theme.Gap();

        var origin = ImGui.GetCursorScreenPos();

        string name = draft.Name;
        if (theme.TextInput($"##{id}-edit-name", origin, nameWidth, ref name, 64, "Character name"))
            draft.Name = name;

        string world = draft.World;
        var worldPos = new Vector2(origin.X + nameWidth + theme.Gap(), origin.Y);
        if (theme.TextInput($"##{id}-edit-world", worldPos, worldWidth, ref world, 64, "World"))
            draft.World = world;

        var savePos = new Vector2(origin.X + width - actionWidth * 2f - theme.Gap(), origin.Y);
        if (theme.IconAction($"{id}-edit-save-{index}", savePos, FontAwesomeIcon.Check, UiTheme.TileGreen, "Save", actionWidth, rowHeight))
        {
            if (!string.IsNullOrWhiteSpace(draft.Name))
            {
                entry.Name = draft.Name.Trim();
                entry.World = draft.World.Trim();
                editIndices.Remove(id);
                Save();
            }
        }

        var cancelPos = new Vector2(origin.X + width - actionWidth, origin.Y);
        if (theme.IconAction($"{id}-edit-cancel-{index}", cancelPos, FontAwesomeIcon.Times, UiTheme.TileRed, "Cancel", actionWidth, rowHeight))
            editIndices.Remove(id);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + theme.Gap(0.4f)));
    }

    private bool DrawBlacklistEntryRow<T>(
        string id,
        float width,
        T entry,
        int index,
        IReadOnlyList<BlacklistFlagColumn<T>>? extraColumns)
        where T : IBlacklistEntry
    {
        var toggleSize = theme.ToggleSize();
        float iconWidth = theme.Scaled(16f);
        float flagWidth = iconWidth + theme.Gap(0.5f) + toggleSize.X;
        float actionWidth = theme.Scaled(UiTheme.ActionButtonSize);
        int flagCount = (extraColumns?.Count ?? 0) + 1;
        float controlWidth = flagCount * (flagWidth + theme.Gap(1.5f)) + actionWidth * 2f + theme.Gap();

        bool remove = false;

        Row.Draw(
            id: $"{id}-entry-{index}",
            icon: FontAwesomeIcon.User,
            iconColor: theme.Accent,
            title: entry.Name,
            subtitle: string.IsNullOrWhiteSpace(entry.World) ? "Any world" : entry.World,
            controlWidth: controlWidth,
            drawControl: (pos, _) =>
            {
                float x = pos.X;
                float rowHeight = theme.Scaled(UiTheme.ControlHeight);

                if (extraColumns != null)
                {
                    foreach (var column in extraColumns)
                    {
                        bool value = column.Get(entry);
                        if (DrawFlagToggle($"{id}-{column.Id}-{index}", new Vector2(x, pos.Y), iconWidth, rowHeight, column.Icon, column.Tooltip, ref value))
                        {
                            column.Set(entry, value);
                            Save();
                        }

                        x += flagWidth + theme.Gap(1.5f);
                    }
                }

                bool noDiscord = entry.DisableDiscord;
                if (DrawFlagToggle($"{id}-no-discord-{index}", new Vector2(x, pos.Y), iconWidth, rowHeight, FontAwesomeIcon.BellSlash, "No Discord notification", ref noDiscord))
                {
                    entry.DisableDiscord = noDiscord;
                    Save();
                }

                x += flagWidth + theme.Gap(1.5f);

                if (theme.IconAction($"{id}-edit-{index}", new Vector2(x, pos.Y), FontAwesomeIcon.Pen, theme.MutedText, "Edit", actionWidth, rowHeight))
                {
                    var draft = Draft(editDrafts, id);
                    draft.Name = entry.Name;
                    draft.World = entry.World;
                    editIndices[id] = index;
                }

                x += actionWidth + theme.Gap();

                if (theme.DeleteAction($"{id}-remove-{index}", new Vector2(x, pos.Y), "Remove", actionWidth, rowHeight))
                    remove = true;
            },
            rowWidth: width);

        return remove;
    }

    private bool DrawFlagToggle(
        string id,
        Vector2 pos,
        float iconWidth,
        float rowHeight,
        FontAwesomeIcon icon,
        string tooltip,
        ref bool value)
    {
        var toggleSize = theme.ToggleSize();

        theme.IconGlyph(pos, new Vector2(iconWidth, rowHeight), icon, value ? theme.Accent : theme.FaintText);

        var iconMin = new Vector2(pos.X, pos.Y + (rowHeight - iconWidth) * 0.5f);
        if (ImGui.IsMouseHoveringRect(iconMin, iconMin + new Vector2(iconWidth, iconWidth)) && !string.IsNullOrEmpty(tooltip))
            theme.Tooltip(tooltip);

        var togglePos = new Vector2(pos.X + iconWidth + theme.Gap(0.5f), pos.Y + (rowHeight - toggleSize.Y) * 0.5f);
        bool changed = theme.ToggleSwitch($"##{id}-toggle", togglePos, ref value);

        if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(tooltip))
            theme.Tooltip(tooltip);

        return changed;
    }
}
