using System;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Cordi.UI.Tabs;

public partial class PartyRadarTab : ConfigTabBase
{
    private SettingsRow? rowRenderer;
    private PageHeader? layoutRenderer;
    private Panel? panelRenderer;

    private SettingsRow Row => rowRenderer ??= new SettingsRow(theme);
    private PageHeader Layout => layoutRenderer ??= new PageHeader(theme);
    private Panel Card => panelRenderer ??= new Panel(theme);

    private ExcelSheet<ClassJob>? jobSheet;

    private string? expandedKey;
    private string noteDraft = string.Empty;

    private bool showArchive;
    private string archiveSearch = string.Empty;
    private string newPlayerName = string.Empty;
    private string newPlayerWorld = string.Empty;
    private string newPlayerNote = string.Empty;

    public override string Label => "Party Radar";

    public PartyRadarTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
    }

    private PartyConfig Party => plugin.Config.Party;

    private RememberMeConfig Memory => plugin.Config.RememberMe;

    private void Save() => plugin.Config.Save();

    public override void Draw()
    {
        DrawHero();
        DrawRosterCard();
        DrawDiscordCard();
        DrawTriggersCard();
        DrawTrackingCard();
        DrawMemoryCard();
        DrawArchiveCard();
    }

    private void DrawToggleRow(
        string id,
        FontAwesomeIcon icon,
        Vector4 iconColor,
        string title,
        string subtitle,
        float rowWidth,
        Func<bool> get,
        Action<bool> set)
    {
        bool value = get();

        var result = Row.Draw(
            id: id,
            icon: icon,
            iconColor: iconColor,
            title: title,
            subtitle: subtitle,
            toggleValue: value,
            onToggle: newValue =>
            {
                set(newValue);
                Save();
            },
            rowWidth: rowWidth);

        if (result.RowClicked && !result.ToggleChanged)
        {
            set(!value);
            Save();
        }
    }

    private string JobAbbreviation(uint jobId)
    {
        jobSheet ??= Service.DataManager.GetExcelSheet<ClassJob>();

        var job = jobSheet?.GetRow(jobId);
        var abbreviation = job?.Abbreviation.ToString();

        return string.IsNullOrWhiteSpace(abbreviation) ? "?" : abbreviation;
    }

    private static bool TryParsePlayer(string input, out string name, out string world)
    {
        name = string.Empty;
        world = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        int separator = input.IndexOf('@');

        if (separator < 0)
        {
            name = input.Trim();
            return name.Length > 0;
        }

        name = input[..separator].Trim();
        world = input[(separator + 1)..].Trim();

        return name.Length > 0;
    }

    private void OpenNoteEditor(string key, string currentNote)
    {
        if (expandedKey == key)
        {
            expandedKey = null;
            return;
        }

        expandedKey = key;
        noteDraft = currentNote;
    }

    private void DrawNoteEditor(string key, string name, string world, float innerWidth)
    {
        if (expandedKey != key)
            return;

        var origin = ImGui.GetCursorScreenPos();
        float height = theme.Scaled(UiTheme.ControlHeight);
        float gap = theme.Gap();
        float buttonWidth = theme.Scaled(80f);
        float indent = theme.Scaled(UiTheme.IconTileSize) + theme.PadX(0.8f) + theme.Gap(1.2f);
        float fieldWidth = MathF.Max(theme.Scaled(120f), innerWidth - indent - buttonWidth - gap);

        theme.PushInputScope();

        theme.TextInput(
            $"##party-note-{key}",
            new Vector2(origin.X + indent, origin.Y),
            fieldWidth,
            ref noteDraft,
            512,
            "Note, shown in this player's Discord notification");

        ImGui.SetCursorScreenPos(new Vector2(origin.X + innerWidth - buttonWidth, origin.Y));

        if (theme.PrimaryButton($"Save##party-note-save-{key}", new Vector2(buttonWidth, height)))
        {
            SaveNote(name, world, noteDraft.Trim());
            expandedKey = null;
        }
        theme.HoverHandIfItem();

        theme.PopInputScope();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(innerWidth, height + theme.Gap(0.4f)));
    }

    private void SaveNote(string name, string world, string note)
    {
        if (plugin.RememberMe.FindPlayer(name, world) != null)
            plugin.RememberMe.UpdateNotes(name, world, note);
        else if (note.Length > 0)
            plugin.RememberMe.AddOrUpdatePlayer(name, world, null, note);
    }

    private string NoteFor(string name, string world)
        => plugin.RememberMe.FindPlayer(name, world)?.Notes ?? string.Empty;

    private void DrawCountChip(Vector2 rightAnchor, string text, Vector4? color = null)
    {
        var size = theme.ChipSize(text);
        theme.ChipAt(new Vector2(rightAnchor.X - size.X, rightAnchor.Y), text, color);
    }
}
