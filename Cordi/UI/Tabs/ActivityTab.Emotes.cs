using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Services.Activity;
using Cordi.Services.Chatbox;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ActivityTab
{
    private static readonly string[] EmotePlaceholders = { "{total}", "{session}", "{combo}", "{emote}", "{player}" };

    private static readonly DropdownItem[] DirectionItems =
    {
        new() { Key = nameof(EmoteDirection.Receiving), Label = "Emotes aimed at me" },
        new() { Key = nameof(EmoteDirection.Giving), Label = "Emotes I aim at others" },
    };

    private const string EmoteTitleHelp =
        "An emote title always beats your Discord activity title while it is on screen.\n" +
        "It disappears on its own, and the Discord title comes straight back.\n" +
        "Placeholders: {total} lifetime count, {session} since login, {combo} within five seconds,\n" +
        "{emote} the emote name, {player} the other person.";

    private List<DropdownItem>? emoteItems;

    private string? editingRuleId;
    private string emoteImportMessage = string.Empty;

    private EmoteTitleSettings EmoteSettings => Config.EmoteTitles;

    private EmoteTitleService? EmoteTitles => plugin.EmoteTitles;

    private IReadOnlyList<DropdownItem> EmoteItems => emoteItems ??= EmoteCatalog.All
        .Select(entry => new DropdownItem
        {
            Key = entry.Id.ToString(),
            Label = string.IsNullOrEmpty(entry.Command) ? entry.Name : $"{entry.Name}  {entry.Command}",
        })
        .ToList();

    private void DrawEmotesPage()
    {
        if (pendingScrollTop)
        {
            pendingScrollTop = false;
            ImGui.SetScrollY(0f);
        }

        if (editingRuleId is { } id)
        {
            if (EmoteSettings.Rules.FirstOrDefault(rule => rule.Id == id) is { } editing)
            {
                DrawEmoteRuleEditor(editing);
                return;
            }

            editingRuleId = null;
        }

        Layout.Draw(
            "Emote Titles",
            "Short lived titles driven by emotes, shown on top of your Discord title",
            innerWidth =>
            {
                theme.PushInputScope();

                DrawToggleRow(
                    "activity-emote-enabled",
                    FontAwesomeIcon.HandHoldingHeart,
                    EmoteSettings.Enabled ? UiTheme.TileGreen : theme.MutedText,
                    "Emote titles",
                    EmoteSettings.Enabled
                        ? "A matching emote takes the title over for a moment"
                        : "Emotes are ignored and only your Discord title is used",
                    innerWidth,
                    () => EmoteSettings.Enabled,
                    value => EmoteSettings.Enabled = value);

                DrawNumberRow(
                    "activity-emote-seconds",
                    FontAwesomeIcon.Stopwatch,
                    UiTheme.TileTeal,
                    "Seconds on screen",
                    "How long an emote title stays before your Discord title returns",
                    innerWidth,
                    EmoteTitleSettings.MinDisplaySeconds,
                    EmoteTitleSettings.MaxDisplaySeconds,
                    () => EmoteSettings.DisplaySeconds,
                    value => EmoteSettings.DisplaySeconds = value);

                DrawToggleRow(
                    "activity-emote-self",
                    FontAwesomeIcon.Vial,
                    EmoteSettings.TrackSelfEmotes ? UiTheme.TileAmber : theme.MutedText,
                    "Track my emotes on myself",
                    "Target yourself and use the emote to test a rule without anyone else",
                    innerWidth,
                    () => EmoteSettings.TrackSelfEmotes,
                    value => EmoteSettings.TrackSelfEmotes = value);

                theme.PopInputScope();
            });

        DrawEmoteRulesCard();
        DrawEmoteImportCard();
        DrawEmoteCountersCard();
    }

    private void DrawEmoteRulesCard()
    {
        Card.Draw(
            "activity-emote-rules",
            innerWidth =>
            {
                theme.PushInputScope();

                if (EmoteSettings.Rules.Count == 0)
                    ImGui.TextColored(theme.FaintText, "No rules yet. Add one, or import from PatMe below.");

                string? remove = null;

                foreach (var rule in EmoteSettings.Rules.ToList())
                {
                    if (DrawEmoteRuleRow(rule, innerWidth)) remove = rule.Id;
                }

                if (remove != null)
                {
                    EmoteSettings.Rules.RemoveAll(rule => rule.Id == remove);

                    if (editingRuleId == remove) editingRuleId = null;

                    Save();
                }

                theme.SpacerY(0.5f);

                if (theme.SecondaryButton("+ Add Rule##activity-emote-add", new Vector2(innerWidth, theme.Scaled(32f))))
                {
                    var created = new EmoteTitleRule();

                    EmoteSettings.Rules.Add(created);
                    editingRuleId = created.Id;
                    pendingScrollTop = true;

                    Save();
                }

                theme.HoverHandIfItem();

                theme.PopInputScope();
            },
            label: "Rules",
            drawTrailing: anchor => DrawCountChip(anchor, EmoteSettings.Rules.Count, "rule"));
    }

    private bool DrawEmoteRuleRow(EmoteTitleRule rule, float rowWidth)
    {
        bool remove = false;
        bool handled = false;

        var result = Row.Draw(
            id: $"activity-emote-rule-{rule.Id}",
            icon: rule.Direction == EmoteDirection.Receiving ? FontAwesomeIcon.HandHoldingHeart : FontAwesomeIcon.HandPointRight,
            iconColor: rule.Enabled ? UiTheme.TilePink : theme.MutedText,
            title: string.IsNullOrWhiteSpace(rule.Name) ? "Unnamed rule" : rule.Name,
            subtitle: EmoteRuleSummary(rule),
            controlWidth: 80f,
            drawControl: (pos, width) =>
            {
                var size = theme.Scaled(UiTheme.ActionButtonSize);

                if (theme.ToggleAction($"##activity-emote-power-{rule.Id}", pos, rule.Enabled,
                        rule.Enabled ? "Disable rule" : "Enable rule"))
                {
                    rule.Enabled = !rule.Enabled;
                    handled = true;
                    Save();
                }

                var next = new Vector2(pos.X + width - size, pos.Y);

                if (theme.DeleteAction($"##activity-emote-del-{rule.Id}", next, "Delete rule"))
                {
                    remove = true;
                    handled = true;
                }
            },
            showChevron: true,
            rowWidth: rowWidth);

        if ((result.RowClicked || result.ChevronClicked) && !handled)
        {
            editingRuleId = rule.Id;
            pendingScrollTop = true;
        }

        return remove;
    }

    private string EmoteRuleSummary(EmoteTitleRule rule)
    {
        var direction = rule.Direction == EmoteDirection.Receiving ? "Received" : "Given";

        if (rule.EmoteIds.Count == 0) return $"{direction}  -  any emote";

        var names = rule.EmoteIds.Take(3).Select(EmoteCatalog.NameFor);
        var label = string.Join(", ", names);

        if (rule.EmoteIds.Count > 3) label += $" +{rule.EmoteIds.Count - 3}";

        return $"{direction}  -  {label}";
    }

    private void DrawEmoteRuleEditor(EmoteTitleRule rule)
    {
        Layout.Draw(
            string.IsNullOrWhiteSpace(rule.Name) ? "Unnamed rule" : rule.Name,
            EmoteRuleSummary(rule),
            innerWidth =>
            {
                var back = Row.Draw(
                    id: "activity-emote-back",
                    icon: FontAwesomeIcon.ArrowLeft,
                    iconColor: theme.Accent,
                    title: "Back to Rules",
                    subtitle: "Return to the rule list",
                    rowWidth: innerWidth);

                if (back.RowClicked)
                {
                    editingRuleId = null;
                    pendingScrollTop = true;
                }
            });

        Card.Draw(
            $"activity-emote-{rule.Id}-rule",
            innerWidth =>
            {
                theme.PushInputScope();

                DrawToggleRow(
                    $"activity-emote-{rule.Id}-enabled",
                    FontAwesomeIcon.PowerOff,
                    rule.Enabled ? UiTheme.TileGreen : theme.MutedText,
                    "Rule active",
                    rule.Enabled ? "This rule can take over your title" : "This rule is ignored",
                    innerWidth,
                    () => rule.Enabled,
                    value => rule.Enabled = value);

                Row.Draw(
                    id: $"activity-emote-{rule.Id}-name",
                    icon: FontAwesomeIcon.Tag,
                    iconColor: UiTheme.TileAmber,
                    title: "Rule name",
                    subtitle: "Only used to tell your rules apart",
                    controlWidth: 220f,
                    drawControl: (pos, width) =>
                    {
                        var name = rule.Name;

                        theme.TextInput($"##activity-emote-{rule.Id}-name-input", pos, width, ref name, 48, "Receiving Pet");

                        rule.Name = name;

                        if (ImGui.IsItemDeactivatedAfterEdit()) Save();
                    },
                    rowWidth: innerWidth);

                Row.Draw(
                    id: $"activity-emote-{rule.Id}-direction",
                    icon: FontAwesomeIcon.ExchangeAlt,
                    iconColor: UiTheme.TilePurple,
                    title: "Direction",
                    subtitle: "Whether the rule watches emotes you get or emotes you send",
                    controlWidth: 220f,
                    drawControl: (pos, width) =>
                    {
                        ImGui.SetCursorScreenPos(pos);
                        theme.OptionPicker(
                            $"activity-emote-{rule.Id}-direction-picker",
                            rule.Direction.ToString(),
                            DirectionItems,
                            key =>
                            {
                                if (!Enum.TryParse<EmoteDirection>(key, out var direction)) return;

                                rule.Direction = direction;
                                Save();
                            },
                            width: width);
                    },
                    rowWidth: innerWidth);

                Row.Draw(
                    id: $"activity-emote-{rule.Id}-emotes",
                    icon: FontAwesomeIcon.Smile,
                    iconColor: UiTheme.TileTeal,
                    title: "Emotes",
                    subtitle: "Which emotes trigger this rule. Counts are shared across everything picked here.",
                    controlWidth: 240f,
                    drawControl: (pos, width) =>
                    {
                        ImGui.SetCursorScreenPos(pos);
                        theme.MultiPicker(
                            $"activity-emote-{rule.Id}-emote-picker",
                            EmotePickerPreview(rule),
                            rule.EmoteIds.Count > 0,
                            EmoteItems,
                            key => ushort.TryParse(key, out var id) && rule.EmoteIds.Contains(id),
                            key =>
                            {
                                if (!ushort.TryParse(key, out var id)) return;

                                if (!rule.EmoteIds.Remove(id)) rule.EmoteIds.Add(id);

                                Save();
                            },
                            width: width);
                    },
                    rowWidth: innerWidth);

                DrawEmoteCharacterRow(rule, innerWidth);

                theme.PopInputScope();
            },
            label: "Rule");

        DrawEmoteFormatCard(rule);
        DrawEmoteRuleAppearanceCard(rule);
    }

    private void DrawEmoteCharacterRow(EmoteTitleRule rule, float innerWidth)
    {
        var current = ChatboxPlayerActions.LocalContentId();
        var limited = rule.CharacterIds.Count > 0;

        DrawToggleRow(
            $"activity-emote-{rule.Id}-scoped",
            FontAwesomeIcon.User,
            limited ? UiTheme.TileBlue : theme.MutedText,
            "Only on this character",
            limited
                ? $"Runs on {rule.CharacterIds.Count} saved character(s)"
                : "Runs on every character you log in with",
            innerWidth,
            () => limited,
            value =>
            {
                rule.CharacterIds.Clear();

                if (value && current != 0) rule.CharacterIds.Add(current);
            });

        if (!limited || current == 0 || rule.CharacterIds.Contains(current)) return;

        DrawButtonRow(
            $"activity-emote-{rule.Id}-add-character",
            FontAwesomeIcon.UserPlus,
            UiTheme.TileBlue,
            "Add this character",
            "The character you are logged in on is not on the list yet",
            "Add",
            innerWidth,
            () =>
            {
                rule.CharacterIds.Add(current);
                Save();
            },
            controlWidth: 110f);
    }

    private string EmotePickerPreview(EmoteTitleRule rule)
    {
        if (rule.EmoteIds.Count == 0) return "Any emote";
        if (rule.EmoteIds.Count == 1) return EmoteCatalog.NameFor(rule.EmoteIds[0]);

        return $"{EmoteCatalog.NameFor(rule.EmoteIds[0])} +{rule.EmoteIds.Count - 1}";
    }

    private void DrawEmoteFormatCard(EmoteTitleRule rule)
    {
        Card.Draw(
            $"activity-emote-{rule.Id}-format",
            innerWidth =>
            {
                theme.PushInputScope();

                var origin = ImGui.GetCursorScreenPos();
                var format = rule.Format ?? string.Empty;

                theme.TextInput(
                    $"##activity-emote-{rule.Id}-format-input",
                    origin,
                    innerWidth,
                    ref format,
                    128,
                    "Patted {total} times");

                var edited = ImGui.IsItemDeactivatedAfterEdit();
                var dropped = AcceptPlaceholderDrop(ref format);

                if (!string.Equals(format, rule.Format, StringComparison.Ordinal)) rule.Format = format;

                if (edited || dropped) Save();

                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(innerWidth, theme.Scaled(UiTheme.ControlHeight) + theme.Gap(0.6f)));

                DrawEmotePlaceholderChips(rule, innerWidth);

                theme.SpacerY(0.6f);

                var preview = EmoteTitles?.Preview(rule) ?? string.Empty;

                ImGui.TextColored(
                    theme.FaintText,
                    string.IsNullOrWhiteSpace(preview) ? "Preview: nothing would be shown" : $"Preview: {preview}");

                theme.PopInputScope();
            },
            label: "Title Format",
            drawTrailing: anchor => theme.HelpPill($"activity-emote-{rule.Id}-help", anchor, EmoteTitleHelp));
    }

    private void DrawEmotePlaceholderChips(EmoteTitleRule rule, float innerWidth)
    {
        var origin = ImGui.GetCursorScreenPos();
        var gap = theme.Gap(0.5f);
        var x = origin.X;
        var y = origin.Y;
        var lineHeight = 0f;

        foreach (var placeholder in EmotePlaceholders)
        {
            var size = theme.ChipSize(placeholder);

            if (x > origin.X && x + size.X > origin.X + innerWidth)
            {
                x = origin.X;
                y += size.Y + gap;
            }

            var pos = new Vector2(x, y);

            ImGui.SetCursorScreenPos(pos);
            ImGui.InvisibleButton($"##activity-emote-{rule.Id}-chip-{placeholder}", size);

            var hovered = ImGui.IsItemHovered();

            if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

            if (ImGui.BeginDragDropSource())
            {
                draggedPlaceholder = placeholder;
                ImGui.SetDragDropPayload(PlaceholderPayload, PlaceholderPayloadData, ImGuiCond.Always);
                ImGui.TextUnformatted(placeholder);
                ImGui.EndDragDropSource();
            }
            else if (hovered)
            {
                theme.Tooltip("Drag onto the format box to insert this placeholder.");
            }

            theme.ChipAt(pos, placeholder, hovered ? theme.Accent : theme.MutedText);

            x += size.X + gap;
            lineHeight = size.Y;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(innerWidth, y - origin.Y + lineHeight + theme.Gap(0.4f)));
    }

    private void DrawEmoteRuleAppearanceCard(EmoteTitleRule rule)
    {
        Card.Draw(
            $"activity-emote-{rule.Id}-appearance",
            innerWidth =>
            {
                theme.PushInputScope();

                DrawToggleRow(
                    $"activity-emote-{rule.Id}-prefix",
                    FontAwesomeIcon.AlignLeft,
                    rule.PrefixTitle ? UiTheme.TileBlue : theme.MutedText,
                    "Show above the name",
                    "Off puts the title under your name instead",
                    innerWidth,
                    () => rule.PrefixTitle,
                    value => rule.PrefixTitle = value);

                DrawColorRow(
                    $"activity-emote-{rule.Id}-color",
                    "Title colour",
                    innerWidth,
                    () => rule.Color,
                    value => rule.Color = value);

                DrawColorRow(
                    $"activity-emote-{rule.Id}-glow",
                    "Title glow",
                    innerWidth,
                    () => rule.Glow,
                    value => rule.Glow = value);

                DrawToggleRow(
                    $"activity-emote-{rule.Id}-gradient",
                    FontAwesomeIcon.Rainbow,
                    rule.GradientColourSet.HasValue ? UiTheme.TilePink : theme.MutedText,
                    "Gradient title",
                    "Animate the title through a Honorific gradient set",
                    innerWidth,
                    () => rule.GradientColourSet.HasValue,
                    value =>
                    {
                        rule.GradientColourSet = value ? 0 : null;
                        rule.GradientAnimationStyle = value ? 0 : null;
                    });

                if (!rule.GradientColourSet.HasValue)
                {
                    theme.PopInputScope();
                    return;
                }

                DrawNumberRow(
                    $"activity-emote-{rule.Id}-gradient-set",
                    FontAwesomeIcon.Swatchbook,
                    UiTheme.TilePink,
                    "Colour set",
                    "Honorific gradient index, -1 uses the title colour and glow above",
                    innerWidth,
                    -1,
                    99,
                    () => rule.GradientColourSet ?? 0,
                    value => rule.GradientColourSet = value);

                Row.Draw(
                    id: $"activity-emote-{rule.Id}-gradient-anim",
                    icon: FontAwesomeIcon.Wind,
                    iconColor: UiTheme.TilePink,
                    title: "Animation",
                    subtitle: "How the gradient moves across the title",
                    controlWidth: 150f,
                    drawControl: (pos, width) =>
                    {
                        ImGui.SetCursorScreenPos(pos);
                        theme.OptionPicker(
                            $"activity-emote-{rule.Id}-gradient-anim-picker",
                            (rule.GradientAnimationStyle ?? 0).ToString(),
                            GradientAnimationItems,
                            key =>
                            {
                                if (!int.TryParse(key, out var style)) return;

                                rule.GradientAnimationStyle = style;
                                Save();
                            },
                            width: width);
                    },
                    rowWidth: innerWidth);

                theme.PopInputScope();
            },
            label: "Appearance");
    }

    private void DrawEmoteImportCard()
    {
        Card.Draw(
            "activity-emote-import",
            innerWidth =>
            {
                theme.PushInputScope();

                var patMe = EmoteTitleImporter.PatMeAvailable;
                var honorific = EmoteTitleImporter.PatMeHonorificAvailable;

                DrawButtonRow(
                    "activity-emote-import-patme",
                    FontAwesomeIcon.FileImport,
                    patMe ? UiTheme.TileGreen : theme.MutedText,
                    "Import counts from PatMe",
                    patMe
                        ? "Brings your pat, dote and hug totals across. Running it twice is safe."
                        : "No PatMe config found next to Cordi's own.",
                    "Import",
                    innerWidth,
                    () => RunEmoteImport(importer => importer.ImportPatMe()),
                    controlWidth: 120f);

                DrawButtonRow(
                    "activity-emote-import-honorific",
                    FontAwesomeIcon.FileImport,
                    honorific ? UiTheme.TileGreen : theme.MutedText,
                    "Import rules from PatMeHonorific",
                    honorific
                        ? "Brings across its rules and counts. Rules you already have are skipped."
                        : "No PatMeHonorific config found next to Cordi's own.",
                    "Import",
                    innerWidth,
                    () => RunEmoteImport(importer => importer.ImportPatMeHonorific()),
                    controlWidth: 120f);

                if (!string.IsNullOrEmpty(emoteImportMessage))
                {
                    theme.SpacerY(0.4f);
                    theme.WrappedText(emoteImportMessage, innerWidth, theme.FaintText);
                }

                theme.PopInputScope();
            },
            label: "Import");
    }

    private void RunEmoteImport(Func<EmoteTitleImporter, EmoteImportResult> run)
    {
        if (EmoteTitles is not { } service)
        {
            emoteImportMessage = "Emote titles are not running yet.";
            return;
        }

        var result = run(new EmoteTitleImporter(plugin, service.Counters));

        emoteImportMessage = result.Message;
    }

    private void DrawEmoteCountersCard()
    {
        if (EmoteTitles is not { } service) return;

        var snapshot = service.Counters.Snapshot();
        var current = ChatboxPlayerActions.LocalContentId();

        var mine = snapshot
            .Where(entry => entry.Key.CharacterId == current && entry.Value > 0)
            .OrderByDescending(entry => entry.Value)
            .ToList();

        Card.Draw(
            "activity-emote-counters",
            innerWidth =>
            {
                theme.PushInputScope();

                if (mine.Count == 0)
                {
                    ImGui.TextColored(
                        theme.FaintText,
                        current == 0
                            ? "Log in to see this character's counts."
                            : "Nothing counted for this character yet.");
                }

                foreach (var entry in mine)
                {
                    Row.Draw(
                        id: $"activity-emote-count-{entry.Key.Direction}-{entry.Key.EmoteId}",
                        icon: entry.Key.Direction == EmoteDirection.Receiving
                            ? FontAwesomeIcon.HandHoldingHeart
                            : FontAwesomeIcon.HandPointRight,
                        iconColor: UiTheme.TilePink,
                        title: EmoteCatalog.NameFor(entry.Key.EmoteId),
                        subtitle: entry.Key.Direction == EmoteDirection.Receiving ? "Received" : "Given",
                        controlWidth: 90f,
                        drawControl: (pos, width) =>
                        {
                            ImGui.SetCursorScreenPos(pos);
                            theme.FittedText(entry.Value.ToString("N0"), width);
                        },
                        rowWidth: innerWidth);
                }

                theme.PopInputScope();
            },
            label: "Counts",
            drawTrailing: anchor => DrawCountChip(anchor, mine.Count, "emote"));
    }
}
