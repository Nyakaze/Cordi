using System;
using System.Collections.Generic;
using System.Linq;
using Cordi.Configuration;
using Cordi.Services.Chatbox;
using Cordi.Services.Translation;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab
{
    private const int TranslationCommonLanguages = 9;

    private static readonly string[] TranslationSampleTexts =
    {
        "こんにちは、よろしくお願いします。",
        "Wo treffen wir uns für den Raid?",
        "Merci beaucoup pour l'aide !",
    };

    private string translationTestResult = string.Empty;
    private bool translationTestBusy;

    private TranslationConfig Tcfg => plugin.Config.Translation;

    private TranslationService Translator => plugin.Chatbox.Translator;

    public void DrawTranslation()
    {
        ConsumeScroll();

        Layout.Draw(
            "Auto Translate",
            "Translates incoming game chat through an online translator.",
            DrawTranslationMaster);

        if (!Tcfg.Enabled)
            return;

        DrawTranslationProviderCard();
        DrawTranslationLanguageCard();
        DrawTranslationChatTypeCard();
        DrawTranslationScopeCard();
        DrawTranslationDisplayCard();
        DrawTranslationOutgoingCard();
        DrawTranslationCacheCard();
    }

    private static readonly IReadOnlyList<DropdownItem> TranslationModeOptions = new List<DropdownItem>
    {
        new() { Key = "off", Label = "Off" },
        new() { Key = "automatic", Label = "Automatic" },
        new() { Key = "manual", Label = "Manual" },
        new() { Key = "both", Label = "Both" },
    };

    private void DrawTranslationMaster(float innerWidth)
    {
        var enabled = Tcfg.Enabled;

        Row.Draw(
            id: "translation-master",
            icon: FontAwesomeIcon.Language,
            iconColor: enabled ? UiTheme.TileGreen : theme.MutedText,
            title: TranslationModeTitle(),
            subtitle: TranslationModeSubtitle(),
            controlWidth: 220f,
            drawControl: (pos, width) =>
            {
                ImGui.SetCursorScreenPos(pos);
                theme.OptionPicker(
                    "translation-master-picker",
                    TranslationModeKey(),
                    TranslationModeOptions,
                    SetTranslationMode,
                    width);
            },
            rowWidth: innerWidth);
    }

    private string TranslationModeTitle()
    {
        if (!Tcfg.Enabled) return "Auto Translate is off";

        return Tcfg.Mode switch
        {
            TranslationMode.Manual => "Translating on request",
            TranslationMode.Both => "Translating automatically and on request",
            _ => "Auto Translate is running",
        };
    }

    private string TranslationModeSubtitle()
    {
        if (!Tcfg.Enabled) return "Nothing gets translated.";

        return Tcfg.Mode switch
        {
            TranslationMode.Manual => "Messages keep a translate button you press yourself.",
            TranslationMode.Both => "Messages arrive translated and keep a translate button as well.",
            _ => "Foreign chat messages get a translation attached as they arrive.",
        };
    }

    private string TranslationModeKey()
    {
        if (!Tcfg.Enabled) return "off";

        return Tcfg.Mode switch
        {
            TranslationMode.Manual => "manual",
            TranslationMode.Both => "both",
            _ => "automatic",
        };
    }

    private void SetTranslationMode(string key)
    {
        if (string.Equals(key, "off", StringComparison.Ordinal))
        {
            Tcfg.Enabled = false;
            Save();
            return;
        }

        Tcfg.Enabled = true;
        Tcfg.Mode = key switch
        {
            "manual" => TranslationMode.Manual,
            "both" => TranslationMode.Both,
            _ => TranslationMode.Automatic,
        };

        Save();
    }

    private void DrawTranslationProviderCard() =>
        Card.Draw("translation-provider", innerWidth =>
        {
            DrawOptionRow(
                "translation-service", FontAwesomeIcon.Server,
                "Service", "Which translator handles the request.",
                innerWidth, () => Tcfg.Provider, v => Tcfg.Provider = v,
                Options(
                    (TranslationProviderKind.Google, "Google (free)"),
                    (TranslationProviderKind.DeepL, "DeepL"),
                    (TranslationProviderKind.Llm, "LLM")));

            switch (Tcfg.Provider)
            {
                case TranslationProviderKind.DeepL:
                    DrawTextRow(
                        "translation-deepl-key", FontAwesomeIcon.Key,
                        "DeepL API Key", "Free and Pro keys both work. Stored in your Cordi config.",
                        innerWidth, () => Tcfg.DeepLApiKey, v => Tcfg.DeepLApiKey = v.Trim(),
                        maxLength: 128, hint: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx:fx");

                    DrawToggleRow(
                        "translation-deepl-pro", FontAwesomeIcon.Crown,
                        "Pro Account", "Uses the paid api.deepl.com endpoint instead of the free one.",
                        innerWidth, () => Tcfg.DeepLPro, v => Tcfg.DeepLPro = v);
                    break;

                case TranslationProviderKind.Llm:
                    DrawTextRow(
                        "translation-llm-endpoint", FontAwesomeIcon.Link,
                        "Endpoint", "Any OpenAI-compatible chat completions URL.",
                        innerWidth, () => Tcfg.LlmEndpoint, v => Tcfg.LlmEndpoint = v.Trim(),
                        maxLength: 256, hint: "https://openrouter.ai/api/v1/chat/completions");

                    DrawTextRow(
                        "translation-llm-key", FontAwesomeIcon.Key,
                        "API Key", "Sent as a bearer token.",
                        innerWidth, () => Tcfg.LlmApiKey, v => Tcfg.LlmApiKey = v.Trim(),
                        maxLength: 256, hint: "sk-...");

                    DrawTextRow(
                        "translation-llm-model", FontAwesomeIcon.Robot,
                        "Model", "Model name as your provider spells it.",
                        innerWidth, () => Tcfg.LlmModel, v => Tcfg.LlmModel = v.Trim(),
                        maxLength: 128, hint: "google/gemini-2.0-flash-001");

                    DrawTextRow(
                        "translation-llm-prompt", FontAwesomeIcon.Comment,
                        "Custom Prompt", "Leave empty to use the built-in FFXIV translation prompt.",
                        innerWidth, () => Tcfg.LlmPrompt, v => Tcfg.LlmPrompt = v,
                        maxLength: 1024, hint: "Built-in prompt");

                    DrawToggleRow(
                        "translation-llm-context", FontAwesomeIcon.History,
                        "Send recent Chat as Context",
                        "The last few lines go along with the request so the model keeps the thread.",
                        innerWidth, () => Tcfg.LlmSendContext, v => Tcfg.LlmSendContext = v);
                    break;
            }

            if (Tcfg.Provider != TranslationProviderKind.Google)
            {
                DrawInfoRow(
                    "translation-fallback", FontAwesomeIcon.LifeRing,
                    "Google is the Fallback",
                    "If the selected service is unreachable or unconfigured, the free Google endpoint answers instead.",
                    innerWidth);
            }

            DrawActionRow(
                "translation-test", FontAwesomeIcon.Vial,
                theme.Accent,
                "Test the Service",
                translationTestResult.Length > 0 ? translationTestResult : "Sends one sample line and shows what comes back.",
                translationTestBusy ? "Testing…" : "Run Test",
                innerWidth,
                RunTranslationTest);
        }, "Service");

    private void RunTranslationTest()
    {
        if (translationTestBusy) return;

        translationTestBusy = true;
        translationTestResult = string.Empty;

        var sample = TranslationSampleTexts[Environment.TickCount % TranslationSampleTexts.Length];

        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var result = await Translator.TranslateTextAsync(sample, Tcfg.TargetLanguage);

                translationTestResult = result.Success
                    ? $"{sample} → {result.Text}"
                    : "The service did not answer. Check the log for details.";
            }
            catch (Exception ex)
            {
                translationTestResult = $"Test failed: {ex.Message}";
            }
            finally
            {
                translationTestBusy = false;
            }
        });
    }

    private void DrawTranslationLanguageCard() =>
        Card.Draw("translation-languages", innerWidth =>
        {
            DrawLanguageRow(
                "translation-target", FontAwesomeIcon.Flag,
                "Translate into", "Every foreign message ends up in this language.",
                innerWidth, () => Tcfg.TargetLanguage, v => Tcfg.TargetLanguage = v);

            DrawOptionRow(
                "translation-source-mode", FontAwesomeIcon.Filter,
                "What to translate", "Either everything you do not read, or only the languages you pick.",
                innerWidth, () => Tcfg.SourceMode, v => Tcfg.SourceMode = v,
                Options(
                    (TranslationSourceMode.Foreign, "Anything I don't read"),
                    (TranslationSourceMode.Selected, "Only picked languages")),
                controlWidth: 240f);

            DrawOptionRow(
                "translation-detection", FontAwesomeIcon.Search,
                "Language Detection",
                "Local guesses from the script and common words. Online asks the translator and is more accurate.",
                innerWidth, () => Tcfg.DetectionSource, v => Tcfg.DetectionSource = v,
                Options(
                    (TranslationDetectionSource.Online, "Online"),
                    (TranslationDetectionSource.Local, "Local")));

            theme.SpacerY(0.6f);

            bool selecting = Tcfg.SourceMode == TranslationSourceMode.Selected;
            var list = selecting ? Tcfg.SourceLanguages : Tcfg.KnownLanguages;

            theme.WrappedText(
                selecting
                    ? "Only messages detected as one of these languages get translated."
                    : "Messages in these languages are left alone. Everything else gets translated.",
                innerWidth,
                theme.MutedText);
            theme.SpacerY(0.4f);

            Chips.Draw(
                selecting ? "translation-source-langs" : "translation-known-langs",
                innerWidth,
                BuildLanguageGroups(),
                key => list.Contains(key, StringComparer.OrdinalIgnoreCase),
                key => ToggleLanguage(list, key),
                (keys, enabled) => SetLanguages(list, keys, enabled),
                noun: "language");
        }, "Languages");

    private void DrawLanguageRow(
        string id,
        FontAwesomeIcon icon,
        string title,
        string subtitle,
        float rowWidth,
        Func<string> get,
        Action<string> set) =>
        Row.Draw(
            id: id,
            icon: icon,
            iconColor: theme.Accent,
            title: title,
            subtitle: subtitle,
            controlWidth: 220f,
            drawControl: (pos, width) =>
            {
                ImGui.SetCursorScreenPos(pos);
                theme.OptionPicker(
                    $"{id}-picker",
                    TranslationLanguages.Normalize(get()),
                    LanguageOptions(),
                    value =>
                    {
                        set(value);
                        Save();
                    },
                    width);
            },
            rowWidth: rowWidth);

    private static IReadOnlyList<DropdownItem> LanguageOptions() =>
        TranslationLanguages.All
            .Select(language => new DropdownItem { Key = language.Iso, Label = language.Name })
            .ToList();

    private static List<ChipSelectorGroup> BuildLanguageGroups() =>
    [
        new ChipSelectorGroup
        {
            Label = "Common",
            Items = TranslationLanguages.All
                .Take(TranslationCommonLanguages)
                .Select(language => new DropdownItem { Key = language.Iso, Label = language.Name })
                .ToList(),
        },
        new ChipSelectorGroup
        {
            Label = "More",
            Items = TranslationLanguages.All
                .Skip(TranslationCommonLanguages)
                .Select(language => new DropdownItem { Key = language.Iso, Label = language.Name })
                .ToList(),
        },
    ];

    private void ToggleLanguage(List<string> list, string iso)
    {
        if (RemoveLanguage(list, iso) == 0) list.Add(iso);

        Save();
    }

    private void SetLanguages(List<string> list, IReadOnlyList<string> keys, bool enabled)
    {
        foreach (var key in keys)
        {
            RemoveLanguage(list, key);
            if (enabled) list.Add(key);
        }

        Save();
    }

    private static int RemoveLanguage(List<string> list, string iso) =>
        list.RemoveAll(value => string.Equals(value, iso, StringComparison.OrdinalIgnoreCase));

    private void DrawTranslationChatTypeCard() =>
        Card.Draw("translation-chat-types", innerWidth =>
        {
            theme.WrappedText(
                "Only messages of these chat types are sent to the translator.",
                innerWidth,
                theme.MutedText);
            theme.SpacerY(0.4f);

            Chips.Draw(
                "translation-types",
                innerWidth,
                BuildSelectableChatGroups(true),
                key => IsTranslationChatKeySelected(key),
                ToggleTranslationChatKey,
                SetTranslationChatKeys);
        }, "Chat Types");

    private bool IsTranslationChatKeySelected(string key) =>
        ResolveChatTypes(key) is { Count: > 0 } types && types.All(Tcfg.ChatTypes.Contains);

    private void ToggleTranslationChatKey(string key)
    {
        var types = ResolveChatTypes(key);
        SetTranslationChatTypes(types, !IsTranslationChatKeySelected(key));
    }

    private void SetTranslationChatKeys(IReadOnlyList<string> keys, bool enabled)
    {
        foreach (var key in keys)
            SetTranslationChatTypes(ResolveChatTypes(key), enabled, false);

        Save();
    }

    private void SetTranslationChatTypes(IReadOnlyList<XivChatType> types, bool enabled, bool save = true)
    {
        foreach (var type in types)
        {
            Tcfg.ChatTypes.RemoveAll(existing => existing == type);
            if (enabled) Tcfg.ChatTypes.Add(type);
        }

        if (save) Save();
    }

    private void DrawTranslationScopeCard() =>
        Card.Draw("translation-scope", innerWidth =>
        {
            DrawToggleRow(
                "translation-own", FontAwesomeIcon.User,
                "Translate my own Messages", "Useful to check what your macros look like to others.",
                innerWidth, () => Tcfg.TranslateOwnMessages, v => Tcfg.TranslateOwnMessages = v);

            DrawToggleRow(
                "translation-duty", FontAwesomeIcon.Dungeon,
                "Translate in Duties", "Turn this off to keep instances quiet and save requests.",
                innerWidth, () => Tcfg.TranslateInDuty, v => Tcfg.TranslateInDuty = v);

            DrawToggleRow(
                "translation-macro", FontAwesomeIcon.Bolt,
                "Skip Macro Spam", "Lines that arrive within a fraction of a second from the same player are ignored.",
                innerWidth, () => Tcfg.SkipMacroSpam, v => Tcfg.SkipMacroSpam = v);

            DrawToggleRow(
                "translation-filtered", FontAwesomeIcon.Ban,
                "Skip filtered Advertisements", "Messages the advertisement filter already blocked stay untranslated.",
                innerWidth, () => Tcfg.SkipFilteredMessages, v => Tcfg.SkipFilteredMessages = v);

            DrawIntSliderRow(
                "translation-min-length", FontAwesomeIcon.TextWidth,
                "Minimum Length", "Shorter messages are ignored.",
                innerWidth, 1, 20, () => Tcfg.MinimumLength, v => Tcfg.MinimumLength = v, " chars");
        }, "Scope");

    private void DrawTranslationDisplayCard() =>
        Card.Draw("translation-display", innerWidth =>
        {
            DrawOptionRow(
                "translation-display-mode", FontAwesomeIcon.Eye,
                "Show Translation", "Where the translated text appears.",
                innerWidth, () => Tcfg.Display, v => Tcfg.Display = v,
                Options(
                    (TranslationDisplayMode.BelowMessage, "Below the message"),
                    (TranslationDisplayMode.ReplaceMessage, "Instead of the message"),
                    (TranslationDisplayMode.TooltipOnly, "Only on hover")),
                controlWidth: 240f);

            DrawToggleRow(
                "translation-show-language", FontAwesomeIcon.Globe,
                "Show detected Language", "A small chip in front of the translation.",
                innerWidth, () => Tcfg.ShowSourceLanguage, v => Tcfg.ShowSourceLanguage = v);

            DrawColorRow(
                "translation-color",
                "Translation Colour", "Colour of the translated text.",
                innerWidth, () => Tcfg.TranslationColor, v => Tcfg.TranslationColor = v);
        }, "Display");

    private void DrawTranslationOutgoingCard() =>
        Card.Draw("translation-outgoing", innerWidth =>
        {
            DrawToggleRow(
                "translation-outgoing-button", FontAwesomeIcon.Language,
                "Translate Button in the Chatbox",
                "Adds a button left of the emoji picker that translates what you typed.",
                innerWidth, () => Tcfg.OutgoingButton, v => Tcfg.OutgoingButton = v);

            if (!Tcfg.OutgoingButton)
                return;

            DrawLanguageRow(
                "translation-outgoing-language", FontAwesomeIcon.PaperPlane,
                "Translate my Message into", "Your text in the input is replaced with this language.",
                innerWidth, () => Tcfg.OutgoingLanguage, v => Tcfg.OutgoingLanguage = v);
        }, "Outgoing Messages");

    private void DrawTranslationCacheCard() =>
        Card.Draw("translation-cache", innerWidth =>
        {
            DrawToggleRow(
                "translation-cache-enabled", FontAwesomeIcon.Database,
                "Remember Translations", "Repeated lines are answered from disk instead of costing another request.",
                innerWidth, () => Tcfg.CacheEnabled, v => Tcfg.CacheEnabled = v);

            DrawIntSliderRow(
                "translation-cache-size", FontAwesomeIcon.Archive,
                "Cache Size", "How many translations are kept before the oldest are dropped.",
                innerWidth, 50, 2000, () => Tcfg.CacheSize, v => Tcfg.CacheSize = v, " entries");

            DrawIntSliderRow(
                "translation-parallel", FontAwesomeIcon.Random,
                "Parallel Requests", "Takes effect after a restart.",
                innerWidth, 1, 8, () => Tcfg.MaxParallelRequests, v => Tcfg.MaxParallelRequests = v);

            DrawIntSliderRow(
                "translation-timeout", FontAwesomeIcon.Clock,
                "Request Timeout", "Takes effect after a restart.",
                innerWidth, 5, 60, () => Tcfg.RequestTimeoutSeconds, v => Tcfg.RequestTimeoutSeconds = v, "s");

            DrawInfoRow(
                "translation-stats", FontAwesomeIcon.ChartBar,
                "This Session",
                $"{Translator.Stats.Translated} translated · {Translator.Stats.CacheHits} from cache · {Translator.Stats.Failed} failed",
                innerWidth);

            var cooldown = Translator.CooldownRemaining;

            if (cooldown > TimeSpan.Zero)
            {
                DrawInfoRow(
                    "translation-cooldown", FontAwesomeIcon.HourglassHalf,
                    "Rate Limited",
                    $"{Translator.ActiveProviderName} asked us to slow down. Resuming in {cooldown.TotalSeconds:0}s.",
                    innerWidth);
            }

            DrawActionRow(
                "translation-clear-cache", FontAwesomeIcon.Trash,
                theme.MutedText,
                "Clear Cache",
                $"{Translator.CachedEntries} translations stored right now.",
                "Clear",
                innerWidth,
                Translator.ClearCache);
        }, "Requests & Cache");
}
