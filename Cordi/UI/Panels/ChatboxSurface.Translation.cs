using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Configuration;
using Cordi.Services.Chatbox;
using Cordi.Services.Translation;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Cordi.UI.Panels;

public sealed partial class ChatboxSurface
{
    private const string OutgoingLanguagePopupId = "##chatbox-translate-language";
    private const string MessageLanguagePopupId = "##chatbox-message-language";

    private static readonly IReadOnlyList<DropdownItem> LanguageOptions = BuildLanguageOptions();

    private readonly ConcurrentQueue<long> _translationDirty = new();
    private readonly ConcurrentQueue<OutgoingSend> _outgoingSends = new();
    private readonly object _outgoingGate = new();
    private Task _outgoingChain = Task.CompletedTask;
    private bool _translationHooked;
    private int _outgoingPending;

    private ChatboxMessage? _languageMenuMessage;
    private Vector2 _languageMenuMin;
    private Vector2 _languageMenuMax;
    private bool _openLanguageMenu;

    private readonly record struct OutgoingSend(string ChannelId, string Text, ChatboxReplyRef? Reply);

    private TranslationConfig Translation => _plugin.Config.Translation;

    private bool OutgoingButtonVisible => Translation.Enabled && Translation.OutgoingButton;

    private string OutgoingTarget => TranslationLanguages.Normalize(Translation.OutgoingLanguage);

    private bool AutoTranslateOn => Translation.AutoTranslateOutgoing;

    private static IReadOnlyList<DropdownItem> BuildLanguageOptions()
    {
        var options = new List<DropdownItem>(TranslationLanguages.All.Count);

        foreach (var language in TranslationLanguages.All)
            options.Add(new DropdownItem { Key = language.Iso, Label = language.Name });

        return options;
    }

    private void DrawOutgoingButton(float spacing)
    {
        ImGui.SameLine(0, spacing);

        var target = OutgoingTarget;
        var busy = Volatile.Read(ref _outgoingPending) > 0;
        var icon = busy ? FontAwesomeIcon.Spinner : FontAwesomeIcon.Language;
        var language = TranslationLanguages.NameOf(target);

        var tooltip = AutoTranslateOn
            ? $"Auto translate is on - messages are sent in {language}.\nRight click to pick a language."
            : $"Auto translate is off.\nRight click to pick a language.";

        var clicked = AutoTranslateOn
            ? _theme.SuccessIconButton("##chatbox-translate", icon, tooltip)
            : _theme.SecondaryIconButton("##chatbox-translate", icon, tooltip);

        var buttonMin = ImGui.GetItemRectMin();
        var buttonMax = ImGui.GetItemRectMax();

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) ImGui.OpenPopup(OutgoingLanguagePopupId);

        if (clicked)
        {
            Translation.AutoTranslateOutgoing = !AutoTranslateOn;
            _plugin.Config.Save();
        }

        _theme.OptionMenu(
            OutgoingLanguagePopupId,
            buttonMin,
            buttonMax,
            _theme.Scaled(220f),
            LanguageOptions,
            target,
            SetOutgoingLanguage,
            above: true);
    }

    private void SetOutgoingLanguage(string iso)
    {
        Translation.OutgoingLanguage = TranslationLanguages.Normalize(iso);
        _plugin.Config.Save();
    }

    private bool AutoTranslateReady(string text)
    {
        if (!OutgoingButtonVisible || !AutoTranslateOn) return false;
        if (OutgoingTarget.Length == 0) return false;

        var trimmed = text.TrimStart();

        return trimmed.Length > 0 && !trimmed.StartsWith('/');
    }

    private void QueueOutgoingTranslation(string channelId, string text, ChatboxReplyRef? reply)
    {
        var target = OutgoingTarget;

        Interlocked.Increment(ref _outgoingPending);

        lock (_outgoingGate)
        {
            _outgoingChain = _outgoingChain
                .ContinueWith(_ => TranslateOutgoingAsync(channelId, text, reply, target), TaskScheduler.Default)
                .Unwrap();
        }
    }

    private async Task TranslateOutgoingAsync(string channelId, string text, ChatboxReplyRef? reply, string target)
    {
        var final = text;

        try
        {
            var result = await Chatbox.Translator.TranslateTextAsync(text, target).ConfigureAwait(false);
            if (result.Success && result.Text.Length > 0) final = result.Text;
        }
        catch (Exception ex)
        {
            _plugin.LogService.Error("UI", "Failed to translate the outgoing message", ex);
        }
        finally
        {
            Interlocked.Decrement(ref _outgoingPending);
            _outgoingSends.Enqueue(new OutgoingSend(channelId, final, reply));
        }
    }

    private void DrainOutgoingSends()
    {
        while (_outgoingSends.TryDequeue(out var pending))
            Chatbox.Send(pending.ChannelId, pending.Text, pending.Reply);
    }

    private void DrawOutgoingMarker(Vector2 inputMin, Vector2 inputMax)
    {
        if (!OutgoingButtonVisible) return;

        var on = AutoTranslateOn;
        var label = on
            ? $"Auto Translate: ON · {TranslationLanguages.NameOf(OutgoingTarget)}"
            : "Auto Translate: OFF";

        _theme.ApplyFontScale(Dropdown.CaptionFontScale);

        var size = ImGui.CalcTextSize(label);
        ImGui.GetWindowDrawList().AddText(
            new Vector2(inputMax.X - size.X, inputMin.Y - size.Y - _theme.Scaled(3f)),
            ImGui.GetColorU32(on ? Translation.TranslationColor : _theme.FaintText),
            label);

        _theme.ApplyFontScale();
    }

    private void HookTranslation()
    {
        if (_translationHooked) return;

        _translationHooked = true;
        Chatbox.Translator.Translated += OnTranslated;
    }

    private void UnhookTranslation()
    {
        if (!_translationHooked) return;

        _translationHooked = false;
        Chatbox.Translator.Translated -= OnTranslated;
    }

    private void OnTranslated(ChatboxMessage message) => _translationDirty.Enqueue(message.Seq);

    private void DrainTranslationDirty()
    {
        while (_translationDirty.TryDequeue(out var seq))
            ForgetRow(seq);
    }

    private bool ReplacesContent(ChatboxMessage message) =>
        Translation.Enabled
        && Translation.Display == TranslationDisplayMode.ReplaceMessage
        && message.HasTranslation;

    private void DrawTranslationBadge(ChatboxMessage message)
    {
        if (!Translation.ShowSourceLanguage || string.IsNullOrEmpty(message.TranslationSource)) return;

        var tint = Translation.TranslationColor;

        _flow.Pill(
            TranslationLanguages.NameOf(message.TranslationSource),
            DimColor(tint, 0.22f),
            tint,
            _theme.Radius(0.4f));
        _flow.Text(" ", tint);
    }

    private void DrawTranslatedContent(ChatboxMessage message)
    {
        DrawTranslationBadge(message);
        _flow.Text(message.TranslatedText ?? string.Empty, Translation.TranslationColor);
    }

    private void DrawTranslationLine(ChatboxMessage message, float width)
    {
        if (!Translation.Enabled) return;
        if (Translation.Display != TranslationDisplayMode.BelowMessage) return;

        if (message.TranslationState == TranslationState.Pending)
        {
            _flow.Begin(
                MathF.Max(width, 60f),
                ImGui.GetTextLineHeight(),
                Config.LineSpacing * ImGuiHelpers.GlobalScale);
            _flow.Indent(TranslationIndent());
            _flow.Text("translating...", _theme.FaintText);
            _flow.End();
            return;
        }

        if (message.TranslationState == TranslationState.Failed && !message.HasTranslation)
        {
            _flow.Begin(
                MathF.Max(width, 60f),
                ImGui.GetTextLineHeight(),
                Config.LineSpacing * ImGuiHelpers.GlobalScale);
            _flow.Indent(TranslationIndent());
            _flow.Text(TranslationFailureText(), UiTheme.TileRed);
            _flow.End();
            return;
        }

        if (!message.HasTranslation) return;

        _flow.Begin(
            MathF.Max(width, 60f),
            ImGui.GetTextLineHeight(),
            Config.LineSpacing * ImGuiHelpers.GlobalScale);
        _flow.Indent(TranslationIndent());
        DrawTranslatedContent(message);
        _flow.End();
    }

    private void DrawTranslationTooltip(ChatboxMessage message)
    {
        if (!Translation.Enabled || !message.HasTranslation) return;

        var body = Translation.Display switch
        {
            TranslationDisplayMode.TooltipOnly => message.TranslatedText,
            TranslationDisplayMode.ReplaceMessage => message.RawContent,
            _ => null,
        };

        if (string.IsNullOrEmpty(body)) return;

        var heading = Translation.Display == TranslationDisplayMode.TooltipOnly
            ? BuildTranslationHeading(message)
            : "Original";

        ImGui.SetTooltip($"{heading}\n{body}");
    }

    private string BuildTranslationHeading(ChatboxMessage message)
    {
        var language = Translation.ShowSourceLanguage && !string.IsNullOrEmpty(message.TranslationSource)
            ? TranslationLanguages.NameOf(message.TranslationSource)
            : string.Empty;

        return language.Length > 0 ? $"Translation - {language}" : "Translation";
    }

    private float TranslationIndent() => _theme.Gap(0.75f);

    private string TranslationFailureText()
    {
        var cooldown = Chatbox.Translator.CooldownRemaining;

        return cooldown > TimeSpan.Zero
            ? $"translation failed - {Chatbox.Translator.ActiveProviderName} is rate limited for {cooldown.TotalSeconds:0}s"
            : "translation failed";
    }

    private bool ManualTranslationReady(ChatboxMessage message) =>
        Translation.TranslatesOnDemand
        && !message.IsSystemLine
        && message.TranslationState != TranslationState.Pending
        && message.RawContent.Length > 0;

    private void RequestTranslation(ChatboxMessage message)
    {
        Chatbox.Translator.Request(message);
        ForgetRow(message.Seq);
    }

    private void OpenMessageLanguageMenu(ChatboxMessage message, Vector2 anchorMin, Vector2 anchorMax)
    {
        _languageMenuMessage = message;
        _languageMenuMin = anchorMin;
        _languageMenuMax = anchorMax;
        _openLanguageMenu = true;
    }

    private void DrawMessageLanguageMenu()
    {
        if (_openLanguageMenu)
        {
            _openLanguageMenu = false;
            ImGui.OpenPopup(MessageLanguagePopupId);
        }

        var message = _languageMenuMessage;
        if (message == null) return;

        var viewport = ImGui.GetMainViewport();
        var above = _languageMenuMin.Y > viewport.Pos.Y + viewport.Size.Y * 0.55f;

        _theme.OptionMenu(
            MessageLanguagePopupId,
            _languageMenuMin,
            _languageMenuMax,
            _theme.Scaled(220f),
            LanguageOptions,
            TranslationLanguages.Normalize(message.TranslationSource ?? string.Empty),
            iso => RetranslateMessage(message, iso),
            above);

        if (!ImGui.IsPopupOpen(MessageLanguagePopupId)) _languageMenuMessage = null;
    }

    private void RetranslateMessage(ChatboxMessage message, string iso)
    {
        var source = TranslationLanguages.Normalize(iso);
        if (source.Length == 0) return;

        Chatbox.Translator.Request(message, source);
        ForgetRow(message.Seq);
    }

    private int TranslationMetricsKey() => HashCode.Combine(
        Translation.Enabled,
        (int)Translation.Mode,
        (int)Translation.Display,
        Translation.ShowSourceLanguage);
}
