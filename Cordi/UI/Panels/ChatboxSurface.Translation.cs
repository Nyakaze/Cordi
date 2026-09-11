using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading.Tasks;
using Cordi.Configuration;
using Cordi.Services.Chatbox;
using Cordi.Services.Translation;
using Cordi.UI.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Panels;

public sealed partial class ChatboxSurface
{
    private readonly ConcurrentQueue<long> _translationDirty = new();
    private readonly ConcurrentQueue<string> _outgoingResults = new();
    private bool _translationHooked;
    private volatile bool _outgoingBusy;
    private string _outgoingMarker = string.Empty;

    private TranslationConfig Translation => _plugin.Config.Translation;

    private bool OutgoingButtonVisible => Translation.Enabled && Translation.OutgoingButton;

    private string OutgoingTarget => TranslationLanguages.Normalize(Translation.OutgoingLanguage);

    private void DrawOutgoingButton(float spacing)
    {
        ImGui.SameLine(0, spacing);

        var target = OutgoingTarget;
        var ready = !_outgoingBusy && target.Length > 0 && _input.Trim().Length > 0;

        using var disabled = ImRaii.Disabled(!ready);

        var clicked = _theme.IconButton(
            "##chatbox-translate",
            _outgoingBusy ? FontAwesomeIcon.Spinner : FontAwesomeIcon.Language,
            $"Translate your message into {TranslationLanguages.NameOf(target)}");

        if (clicked) BeginOutgoingTranslation(target);
    }

    private void BeginOutgoingTranslation(string target)
    {
        var source = _emoteFont.Expand(_input).Trim();
        if (source.Length == 0) return;

        _outgoingBusy = true;

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await Chatbox.Translator.TranslateTextAsync(source, target).ConfigureAwait(false);
                if (result.Success) _outgoingResults.Enqueue(result.Text);
            }
            catch (Exception ex)
            {
                _plugin.LogService.Error("UI", "Failed to translate the chatbox input", ex);
            }
            finally
            {
                _outgoingBusy = false;
            }
        });
    }

    private void DrainOutgoingTranslation()
    {
        if (!_outgoingResults.TryDequeue(out var text) || text.Length == 0) return;

        _input = text;
        _outgoingMarker = text;
        _focusInput = true;
        _silentFocus = true;
    }

    private void DrawOutgoingMarker(Vector2 inputMin, Vector2 inputMax)
    {
        if (_outgoingMarker.Length == 0) return;

        if (!string.Equals(_input, _outgoingMarker, StringComparison.Ordinal))
        {
            _outgoingMarker = string.Empty;
            return;
        }

        var label = $"translated · {TranslationLanguages.NameOf(OutgoingTarget)}";

        _theme.ApplyFontScale(Dropdown.CaptionFontScale);

        var size = ImGui.CalcTextSize(label);
        ImGui.GetWindowDrawList().AddText(
            new Vector2(inputMax.X - size.X, inputMin.Y - size.Y - _theme.Scaled(3f)),
            ImGui.GetColorU32(Translation.TranslationColor),
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

    private int TranslationMetricsKey() => HashCode.Combine(
        Translation.Enabled,
        (int)Translation.Display,
        Translation.ShowSourceLanguage);
}
