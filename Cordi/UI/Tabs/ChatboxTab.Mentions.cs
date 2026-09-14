using System;
using System.Collections.Generic;
using Cordi.UI.Components;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab
{
    private sealed class KeywordEntry
    {
        public string Value { get; set; } = string.Empty;
    }

    private readonly List<KeywordEntry> keywordEntries = new();
    private readonly List<string> keywordSnapshot = new();

    public void DrawMentions()
    {
        ConsumeScroll();

        Layout.Draw("Mentions", "What counts as a mention and how you are told about it.");

        Card.Draw("chatbox-mentions", innerWidth =>
        {
            DrawToggleRow(
                "chatbox-enable-mentions", FontAwesomeIcon.At,
                "Enable Mentions", "Detects and highlights mentions in incoming messages.",
                innerWidth, () => Cfg.EnableMentions, v => Cfg.EnableMentions = v);

            DrawToggleRow(
                "chatbox-mention-everyone", FontAwesomeIcon.Users,
                "Highlight @everyone", "Treats @everyone as a mention of you.",
                innerWidth, () => Cfg.MentionEveryone, v => Cfg.MentionEveryone = v);

            DrawToggleRow(
                "chatbox-mention-here", FontAwesomeIcon.MapMarkerAlt,
                "Highlight @here", "Treats @here as a mention of you.",
                innerWidth, () => Cfg.MentionHere, v => Cfg.MentionHere = v);

            DrawToggleRow(
                "chatbox-mention-name", FontAwesomeIcon.User,
                "Highlight my Character Name", "Matches your full character name.",
                innerWidth, () => Cfg.MentionOwnName, v => Cfg.MentionOwnName = v);

            DrawToggleRow(
                "chatbox-mention-parts", FontAwesomeIcon.UserTag,
                "Highlight single Name Parts", "Also matches just your first or last name.",
                innerWidth, () => Cfg.MentionOwnNameParts, v => Cfg.MentionOwnNameParts = v);

            DrawToggleRow(
                "chatbox-mention-tell", FontAwesomeIcon.Envelope,
                "Treat Tells as Mention", "Every incoming tell counts as a mention.",
                innerWidth, () => Cfg.MentionOnTell, v => Cfg.MentionOnTell = v);
        }, "Detection");

        DrawKeywordList();

        Card.Draw("chatbox-notifications", innerWidth =>
        {
            DrawToggleRow(
                "chatbox-unread-dot", FontAwesomeIcon.Circle,
                "Show Unread Dot", "Marks channels that received something new.",
                innerWidth, () => Cfg.ShowUnreadDot, v => Cfg.ShowUnreadDot = v);

            DrawToggleRow(
                "chatbox-mention-badge", FontAwesomeIcon.Bell,
                "Show Mention Count Badge", "Counts unread mentions per channel.",
                innerWidth, () => Cfg.ShowMentionBadge, v => Cfg.ShowMentionBadge = v);

            DrawToggleRow(
                "chatbox-title-count", FontAwesomeIcon.WindowMaximize,
                "Show Count in Window Title", "Puts the mention count into the window title.",
                innerWidth, () => Cfg.FlashTitleOnMention, v => Cfg.FlashTitleOnMention = v);

            DrawToggleRow(
                "chatbox-notify-mention", FontAwesomeIcon.CommentDots,
                "Dalamud Notification on Mention", "Pops a Dalamud toast whenever you are mentioned.",
                innerWidth, () => Cfg.NotifyOnMention, v => Cfg.NotifyOnMention = v);

            DrawToggleRow(
                "chatbox-notify-unread", FontAwesomeIcon.CommentAlt,
                "Dalamud Notification on any Message", "Pops a Dalamud toast for every message.",
                innerWidth, () => Cfg.NotifyOnUnread, v => Cfg.NotifyOnUnread = v);

            DrawToggleRow(
                "chatbox-sound-mention", FontAwesomeIcon.VolumeUp,
                "Play Sound on Mention", "Plays a sound file when you are mentioned.",
                innerWidth, () => Cfg.PlaySoundOnMention, v => Cfg.PlaySoundOnMention = v);

            DrawTextRow(
                "chatbox-sound-path", FontAwesomeIcon.FileAudio,
                "Mention Sound", "Path to a .wav file.",
                innerWidth, () => Cfg.MentionSoundPath, v => Cfg.MentionSoundPath = v, 260, "C:\\sounds\\ping.wav", 320f);

            DrawSliderRow(
                "chatbox-sound-volume", FontAwesomeIcon.VolumeDown,
                "Mention Sound Volume", "How loud the mention sound is played.",
                innerWidth, 0f, 100f, 1f,
                () => Cfg.MentionSoundVolume * 100f,
                v => Cfg.MentionSoundVolume = v / 100f,
                suffix: "%");
        }, "Notifications");

        Card.Draw("chatbox-replies", innerWidth =>
        {
            DrawToggleRow(
                "chatbox-enable-replies", FontAwesomeIcon.Reply,
                "Enable Replies", "Lets you reply to a specific message.",
                innerWidth, () => Cfg.EnableReplies, v => Cfg.EnableReplies = v);

            DrawToggleRow(
                "chatbox-reply-preview", FontAwesomeIcon.QuoteLeft,
                "Show Reply Preview above Message", "Shows the quoted message above the reply.",
                innerWidth, () => Cfg.ShowReplyPreview, v => Cfg.ShowReplyPreview = v);

            DrawIntSliderRow(
                "chatbox-reply-excerpt", FontAwesomeIcon.TextWidth,
                "Reply Excerpt Length", "Characters kept from the quoted message.",
                innerWidth, 16, 200,
                () => Cfg.ReplyExcerptLength, v => Cfg.ReplyExcerptLength = v);

            DrawTextRow(
                "chatbox-reply-format", FontAwesomeIcon.Code,
                "Game Reply Format", "Tokens: {name}, {excerpt}, {message}",
                innerWidth, () => Cfg.GameReplyFormat, v => Cfg.GameReplyFormat = v, 128, "@{name} {message}", 320f);
        }, "Replies");
    }

    private void SyncKeywordEntries()
    {
        if (keywordSnapshot.Count == Cfg.MentionKeywords.Count)
        {
            var same = true;

            for (int index = 0; index < keywordSnapshot.Count; index++)
            {
                if (string.Equals(keywordSnapshot[index], Cfg.MentionKeywords[index], StringComparison.Ordinal))
                    continue;

                same = false;
                break;
            }

            if (same) return;
        }

        keywordEntries.Clear();

        foreach (string keyword in Cfg.MentionKeywords)
            keywordEntries.Add(new KeywordEntry { Value = keyword });

        CaptureKeywordSnapshot();
    }

    private void CaptureKeywordSnapshot()
    {
        keywordSnapshot.Clear();
        keywordSnapshot.AddRange(Cfg.MentionKeywords);
    }

    private void DrawKeywordList()
    {
        SyncKeywordEntries();

        var columns = new[]
        {
            new ListColumn<KeywordEntry>
            {
                Id = "keyword",
                Header = "Keyword",
                Hint = "Word that mentions you",
                MaxLength = 64,
                GetText = entry => entry.Value,
                SetText = (entry, value) => entry.Value = value,
            },
        };

        List.Draw(
            "chatbox-keywords",
            "Mention Keywords",
            "Any message containing one of these words counts as a mention.",
            keywordEntries,
            columns,
            () => new KeywordEntry(),
            () =>
            {
                Cfg.MentionKeywords.Clear();

                foreach (var entry in keywordEntries)
                {
                    if (!string.IsNullOrWhiteSpace(entry.Value))
                        Cfg.MentionKeywords.Add(entry.Value.Trim());
                }

                CaptureKeywordSnapshot();
                Save();
            },
            addLabel: "Add Keyword",
            emptyText: "No keywords configured yet.",
            removeTooltip: "Remove keyword");
    }
}
