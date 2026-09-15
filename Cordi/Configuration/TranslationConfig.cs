using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Text;
using Newtonsoft.Json;

namespace Cordi.Configuration;

public enum TranslationProviderKind
{
    Machine,
    DeepL,
    Llm,
}

public enum TranslationDetectionSource
{
    Local,
    Online,
}

public enum TranslationSourceMode
{
    Foreign,
    Selected,
}

public enum TranslationDisplayMode
{
    BelowMessage,
    ReplaceMessage,
    TooltipOnly,
}

public enum TranslationMode
{
    Automatic,
    Manual,
    Both,
}

[Serializable]
public class TranslationConfig
{
    public bool Enabled { get; set; }
    public TranslationMode Mode { get; set; } = TranslationMode.Automatic;

    [JsonIgnore]
    public bool TranslatesAutomatically => Enabled && Mode != TranslationMode.Manual;

    [JsonIgnore]
    public bool TranslatesOnDemand => Enabled && Mode != TranslationMode.Automatic;

    public TranslationProviderKind Provider { get; set; } = TranslationProviderKind.DeepL;
    public TranslationDetectionSource DetectionSource { get; set; } = TranslationDetectionSource.Local;
    public TranslationSourceMode SourceMode { get; set; } = TranslationSourceMode.Selected;
    public TranslationDisplayMode Display { get; set; } = TranslationDisplayMode.BelowMessage;

    public string TargetLanguage { get; set; } = "en";
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<string> KnownLanguages { get; set; } = new() { "en" };

    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<string> SourceLanguages { get; set; } = new() { "ja" };

    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<XivChatType> ChatTypes { get; set; } = new()
    {
        XivChatType.Say,
        XivChatType.Shout,
        XivChatType.Yell,
        XivChatType.Party,
        XivChatType.CrossParty,
        XivChatType.Alliance,
        XivChatType.TellIncoming,
        XivChatType.FreeCompany,
    };

    public bool TranslateOwnMessages { get; set; }
    public bool TranslateInDuty { get; set; } = true;
    public bool SkipMacroSpam { get; set; } = true;
    public bool SkipFilteredMessages { get; set; } = true;
    public bool SkipChatNoise { get; set; } = true;
    public bool TranslateDiscordMessages { get; set; } = true;
    public int MinimumLength { get; set; } = 2;
    public int DetectionConfidence { get; set; } = 40;

    public bool OutgoingButton { get; set; }
    public string OutgoingLanguage { get; set; } = "en";
    public bool AutoTranslateOutgoing { get; set; }

    public bool ShowSourceLanguage { get; set; } = true;
    public Vector4 TranslationColor { get; set; } = new(0.51f, 0.76f, 0.96f, 1f);

    public bool CacheEnabled { get; set; } = true;
    public int CacheSize { get; set; } = 400;

    public string DeepLApiKey { get; set; } = string.Empty;
    public bool DeepLPro { get; set; }

    public string LlmEndpoint { get; set; } = "https://openrouter.ai/api/v1/chat/completions";
    public string LlmApiKey { get; set; } = string.Empty;
    public string LlmModel { get; set; } = "google/gemini-2.0-flash-001";
    public string LlmPrompt { get; set; } = string.Empty;
    public bool LlmSendContext { get; set; } = true;

    public int MaxParallelRequests { get; set; } = 3;
    public int RequestTimeoutSeconds { get; set; } = 20;

    public void Normalize()
    {
        KnownLanguages = Deduplicate(KnownLanguages);
        SourceLanguages = Deduplicate(SourceLanguages);
        ChatTypes = ChatTypes.Distinct().ToList();
    }

    private static List<string> Deduplicate(List<string>? values) =>
        values == null
            ? new List<string>()
            : values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    public bool IsKnown(string? iso) =>
        iso != null && KnownLanguages.Contains(iso, StringComparer.OrdinalIgnoreCase);

    public bool IsSelectedSource(string? iso) =>
        iso != null && SourceLanguages.Contains(iso, StringComparer.OrdinalIgnoreCase);

    public bool IsTarget(string? iso) =>
        iso != null && string.Equals(iso, TargetLanguage, StringComparison.OrdinalIgnoreCase);
}
