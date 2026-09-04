using Cordi.Configuration;
using Cordi.UI.Themes;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab
{
    public void DrawAppearance()
    {
        ConsumeScroll();

        Layout.Draw("Appearance", "How the chatbox is laid out and how messages are drawn.");

        Card.Draw("chatbox-layout", innerWidth =>
        {
            DrawOptionRow(
                "chatbox-message-design", FontAwesomeIcon.AlignLeft,
                "Message Design", "Cozy is the Discord default, Compact fits one line.",
                innerWidth, () => Cfg.Layout, v => Cfg.Layout = v,
                Options(
                    (ChatboxLayout.Cozy, "Cozy"),
                    (ChatboxLayout.Compact, "Compact")));

            DrawOptionRow(
                "chatbox-nav-style", FontAwesomeIcon.Bars,
                "Navigation Style", "Server rail, channel list, tab bar or nothing at all.",
                innerWidth, () => Cfg.NavStyle, v => Cfg.NavStyle = v,
                Options(
                    (ChatboxNavStyle.ServerRail, "Server Rail"),
                    (ChatboxNavStyle.ChannelList, "Channel List"),
                    (ChatboxNavStyle.Tabs, "Tabs"),
                    (ChatboxNavStyle.Hidden, "Hidden")));

            if (Cfg.NavStyle == ChatboxNavStyle.Hidden)
                return;

            DrawOptionRow(
                "chatbox-nav-side", FontAwesomeIcon.ArrowsAltH,
                "Navigation Side", "Which edge the navigation sits on.",
                innerWidth, () => Cfg.NavSide, v => Cfg.NavSide = v,
                Options(
                    (ChatboxNavSide.Left, "Left"),
                    (ChatboxNavSide.Right, "Right")));

            switch (Cfg.NavStyle)
            {
                case ChatboxNavStyle.Tabs:
                    DrawOptionRow(
                        "chatbox-tab-side", FontAwesomeIcon.ArrowsAltV,
                        "Tab Position", "Where the tab bar is anchored.",
                        innerWidth, () => Cfg.TabSide, v => Cfg.TabSide = v,
                        Options(
                            (ChatboxTabSide.Top, "Top"),
                            (ChatboxTabSide.Bottom, "Bottom")));

                    DrawSliderRow(
                        "chatbox-tab-width", FontAwesomeIcon.Ruler,
                        "Tab Width", "0 sizes each tab to its label.",
                        innerWidth, 0f, 260f, 1f,
                        () => Cfg.TabWidth, v => Cfg.TabWidth = v);
                    break;

                case ChatboxNavStyle.ServerRail:
                    DrawSliderRow(
                        "chatbox-rail-width", FontAwesomeIcon.Ruler,
                        "Rail Width", "Width of the icon rail.",
                        innerWidth, 40f, 200f, 1f,
                        () => Cfg.RailWidth, v => Cfg.RailWidth = v);

                    DrawSliderRow(
                        "chatbox-rail-icon", FontAwesomeIcon.ExpandArrowsAlt,
                        "Rail Icon Size", "Size of each channel icon.",
                        innerWidth, 20f, 96f, 1f,
                        () => Cfg.RailIconSize, v => Cfg.RailIconSize = v);

                    DrawToggleRow(
                        "chatbox-resizable-rail", FontAwesomeIcon.ArrowsAltH,
                        "Drag to resize Navigation", "Adds a drag handle next to the navigation.",
                        innerWidth, () => Cfg.ResizableNav, v => Cfg.ResizableNav = v);
                    break;

                default:
                    DrawSliderRow(
                        "chatbox-nav-width", FontAwesomeIcon.Ruler,
                        "Navigation Width", "Width of the channel list.",
                        innerWidth, 90f, 360f, 1f,
                        () => Cfg.NavWidth, v => Cfg.NavWidth = v);

                    DrawToggleRow(
                        "chatbox-resizable-nav", FontAwesomeIcon.ArrowsAltH,
                        "Drag to resize Navigation", "Adds a drag handle next to the navigation.",
                        innerWidth, () => Cfg.ResizableNav, v => Cfg.ResizableNav = v);
                    break;
            }
        }, "Layout");

        Card.Draw("chatbox-avatars", innerWidth =>
        {
            DrawToggleRow(
                "chatbox-show-avatars", FontAwesomeIcon.UserCircle,
                "Show Avatars", "Draws a portrait next to each message group.",
                innerWidth, () => Cfg.ShowAvatars, v => Cfg.ShowAvatars = v);

            DrawToggleRow(
                "chatbox-round-avatars", FontAwesomeIcon.Circle,
                "Round Avatars", "Clips portraits into a circle.",
                innerWidth, () => Cfg.RoundAvatars, v => Cfg.RoundAvatars = v);

            DrawToggleRow(
                "chatbox-lodestone-avatars", FontAwesomeIcon.IdBadge,
                "Lodestone Portraits for Game Chat", "Looks up character portraits from the Lodestone.",
                innerWidth, () => Cfg.ShowLodestoneAvatars, v => Cfg.ShowLodestoneAvatars = v);

            DrawSliderRow(
                "chatbox-avatar-size", FontAwesomeIcon.ExpandArrowsAlt,
                "Avatar Size", "Edge length of each portrait.",
                innerWidth, 16f, 72f, 1f,
                () => Cfg.AvatarSize, v => Cfg.AvatarSize = v);
        }, "Avatars");

        Card.Draw("chatbox-messages", innerWidth =>
        {
            DrawToggleRow(
                "chatbox-group", FontAwesomeIcon.LayerGroup,
                "Group consecutive Messages", "Merges messages from the same author into one block.",
                innerWidth, () => Cfg.GroupConsecutive, v => Cfg.GroupConsecutive = v);

            DrawIntSliderRow(
                "chatbox-group-window", FontAwesomeIcon.Clock,
                "Grouping Window", "Seconds between messages that still count as one group.",
                innerWidth, 30, 1800,
                () => Cfg.GroupWindowSeconds, v => Cfg.GroupWindowSeconds = v, " s");

            DrawSliderRow(
                "chatbox-message-spacing", FontAwesomeIcon.ArrowsAltV,
                "Message Spacing", "Vertical gap between message blocks.",
                innerWidth, 0f, 24f, 1f,
                () => Cfg.MessageSpacing, v => Cfg.MessageSpacing = v);

            DrawSliderRow(
                "chatbox-line-spacing", FontAwesomeIcon.Bars,
                "Line Spacing", "Vertical gap between wrapped lines.",
                innerWidth, 0f, 12f, 1f,
                () => Cfg.LineSpacing, v => Cfg.LineSpacing = v);

            DrawOptionRow(
                "chatbox-timestamps", FontAwesomeIcon.Clock,
                "Timestamps", "How the time in front of a message is written.",
                innerWidth, () => Cfg.Timestamps, v => Cfg.Timestamps = v,
                Options(
                    (ChatboxTimestampStyle.None, "None"),
                    (ChatboxTimestampStyle.Time, "Time"),
                    (ChatboxTimestampStyle.TimeWithSeconds, "Time with Seconds"),
                    (ChatboxTimestampStyle.DateAndTime, "Date and Time"),
                    (ChatboxTimestampStyle.Relative, "Relative")));

            DrawOptionRow(
                "chatbox-name-style", FontAwesomeIcon.User,
                "Name Style", "Whether the home world is appended to a name.",
                innerWidth, () => Cfg.NameStyle, v => Cfg.NameStyle = v,
                Options(
                    (ChatboxNameStyle.NameOnly, "Name only"),
                    (ChatboxNameStyle.NameAndWorld, "Name and World")));

            DrawToggleRow(
                "chatbox-color-names", FontAwesomeIcon.Highlighter,
                "Colour Names by Channel", "Each author takes the colour of its channel.",
                innerWidth, () => Cfg.ColorNamesByChannel, v => Cfg.ColorNamesByChannel = v);

            DrawToggleRow(
                "chatbox-compact-system", FontAwesomeIcon.Compress,
                "Compact System Messages", "System lines drop the portrait and the name and stay on one row.",
                innerWidth, () => Cfg.CompactSystemMessages, v => Cfg.CompactSystemMessages = v);

            DrawToggleRow(
                "chatbox-divider", FontAwesomeIcon.GripLines,
                "Show \"New Messages\" Divider", "Marks where you stopped reading.",
                innerWidth, () => Cfg.ShowNewMessageDivider, v => Cfg.ShowNewMessageDivider = v);

            DrawToggleRow(
                "chatbox-hover-toolbar", FontAwesomeIcon.Tools,
                "Show Hover Toolbar", "Reply and copy actions appear when hovering a message.",
                innerWidth, () => Cfg.ShowHoverToolbar, v => Cfg.ShowHoverToolbar = v);

            DrawToggleRow(
                "chatbox-autoscroll", FontAwesomeIcon.AngleDoubleDown,
                "Auto Scroll", "Follows new messages while you are at the bottom.",
                innerWidth, () => Cfg.AutoScroll, v => Cfg.AutoScroll = v);
        }, "Messages");

        Card.Draw("chatbox-colours", innerWidth =>
        {
            DrawColorRow(
                "chatbox-mention-color",
                "Mention Colour", "Used for the @name itself.",
                innerWidth, () => Cfg.MentionColor, v => Cfg.MentionColor = v);

            DrawColorRow(
                "chatbox-mention-highlight",
                "Mention Highlight", "Background tint of a message that mentions you.",
                innerWidth, () => Cfg.MentionHighlightColor, v => Cfg.MentionHighlightColor = v);

            DrawColorRow(
                "chatbox-badge-color",
                "Badge Colour", "Unread and mention counters.",
                innerWidth, () => Cfg.UnreadBadgeColor, v => Cfg.UnreadBadgeColor = v);

            DrawColorRow(
                "chatbox-link-color",
                "Link Colour", "Colour of clickable links.",
                innerWidth, () => Cfg.LinkColor, v => Cfg.LinkColor = v);

            DrawToggleRow(
                "chatbox-outline-links", FontAwesomeIcon.Link,
                "Outline Links", "The outline keeps links readable when the window is transparent.",
                innerWidth, () => Cfg.OutlineLinks, v => Cfg.OutlineLinks = v);
        }, "Colours");

        Card.Draw("chatbox-chat-colours", innerWidth =>
        {
            theme.WrappedText(
                "Every channel uses these colours unless it overrides them with its own colour.",
                innerWidth,
                theme.MutedText);
            theme.SpacerY(0.6f);

            foreach (var group in ChatboxConfig.ChatColorGroups)
            {
                var applies = group.Applies;
                var fallback = ChatboxConfig.FromRgba(group.Default);

                DrawColorRow(
                    $"chatbox-chat-colour-{group.Key}",
                    group.Label, $"Colour of {group.Label} messages.",
                    innerWidth,
                    () => Cfg.ChatTypeColor(group.Key) ?? fallback,
                    v => Cfg.SetChatTypeColor(applies, v));
            }

            DrawActionRow(
                "chatbox-chat-colour-reset", FontAwesomeIcon.Undo, UiTheme.TileAmber,
                "Reset Chat Colours", "Restores the built-in colour for every chat type.",
                "Reset", innerWidth,
                () =>
                {
                    Cfg.ChatTypeColors = ChatboxConfig.DefaultChatTypeColors();
                    Save();
                });
        }, "Chat Colours");
    }
}
