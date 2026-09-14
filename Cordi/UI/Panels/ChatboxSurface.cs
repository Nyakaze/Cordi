using System;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Services.Chatbox;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Panels;

public sealed partial class ChatboxSurface : IDisposable
{
    private const string CommandGuardPopupId = "##chatbox-command-guard";

    private readonly CordiPlugin _plugin;
    private readonly UiTheme _theme;
    private readonly ChatboxInlineFlow _flow;
    private readonly ChatboxEmojiPicker _picker;
    private readonly ChatboxAutocomplete _autocomplete;

    private readonly ChatboxEmoteFont _emoteFont = new();
    private readonly ImGui.ImGuiInputTextCallbackPtrDelegate _inputCallback;

    private string _input = string.Empty;
    private string? _pendingToken;
    private int _pendingStart;
    private int _pendingLength;
    private bool _inputWasActive;
    private bool _silentFocus;
    private float _measuredInputHeight;
    private bool _clearSelection;
    private Vector2 _inputMin;
    private float _inputWidth;
    private ChatboxReplyRef? _replyTarget;
    private long _highlightSeq;
    private DateTime _highlightUntil = DateTime.MinValue;
    private int _scrollToBottomFrames = ScrollSettleFrames;
    private bool _focusInput;
    private string? _pendingCommandText;
    private ChatboxReplyRef? _pendingCommandReply;
    private string _pendingCommandChannel = string.Empty;
    private string _pendingCommandName = string.Empty;
    private string _pendingCommandTarget = string.Empty;
    private bool _commandGuardOpen;
    private bool _focused;
    private bool _gameWasFocused = true;
    private bool _windowWasFocused;
    private bool _readArmed;
    private bool _readViewed;
    private bool _requestFocus;

    public ChatboxSurface(CordiPlugin plugin, UiTheme theme, string? pinnedChannelId = null)
    {
        _plugin = plugin;
        _theme = theme;
        _flow = new ChatboxInlineFlow(theme);
        _picker = new ChatboxEmojiPicker(plugin, theme);
        _autocomplete = new ChatboxAutocomplete(plugin, theme);
        _inputCallback = InputCallback;

        PinnedChannelId = pinnedChannelId;
    }

    public string? PinnedChannelId { get; }

    public bool Detached => PinnedChannelId != null;

    public bool Focused => _focused;

    private ChatboxConfig Config => _plugin.Config.Chatbox;

    private ChatboxService Chatbox => _plugin.Chatbox;

    private ChatboxChannelState? Target =>
        Detached ? Chatbox.GetChannel(PinnedChannelId!) : Chatbox.ActiveChannel;

    public void Draw()
    {
        _navDirty = true;
        _picker.SetChatboxBounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        _theme.ApplyFontScale();
        UpdateItemTooltip();

        var windowFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);

        UpdateReadArming(windowFocused);

        _focused = windowFocused && Chatbox.GameFocused;

        SyncFocusState();

        AnimatedTextureWrap.SetWindowAnimates(
            Config.AnimateGifs && (!Config.AnimateOnlyWhenFocused || _focused));

        Chatbox.ImageCache.Tick(ImGui.GetIO().DeltaTime, Config.AnimateGifs);

        ConsumeFocusRequest();
        HandleSearchShortcut();

        var inputHeight = MeasureInputHeight();

        if (Detached)
        {
            DrawBody(inputHeight);
            DrawCommandGuard();
            return;
        }

        switch (Config.NavStyle)
        {
            case ChatboxNavStyle.Tabs:
                DrawWithTabs(inputHeight);
                break;

            case ChatboxNavStyle.ServerRail:
            case ChatboxNavStyle.ChannelList:
                DrawSideBySide(inputHeight);
                break;

            default:
                DrawBody(inputHeight);
                break;
        }

        DrawCommandGuard();
    }

    private void UpdateReadArming(bool windowFocused)
    {
        var gameFocused = Chatbox.GameFocused;
        var gameRegained = gameFocused && !_gameWasFocused;
        var windowGained = windowFocused && !_windowWasFocused;

        _gameWasFocused = gameFocused;
        _windowWasFocused = windowFocused;

        if (!windowFocused || gameRegained)
        {
            _readArmed = false;
            return;
        }

        if (!gameFocused) return;

        if (windowGained || ImGui.IsAnyItemActive() || ClickedInside()) _readArmed = true;
    }

    private static bool ClickedInside() =>
        ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows)
        && (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right));

    private void SyncFocusState()
    {
        var channel = Target;

        if (Detached)
        {
            var read = _focused && _readArmed;
            var wasRead = _readViewed;
            _readViewed = read;

            Chatbox.SetChannelViewed(PinnedChannelId!, read);

            if (read && !wasRead && channel != null && channel.UnreadCount > 0)
                ScrollToUnread(channel, Chatbox.BeginViewing(channel));

            if (read && channel != null) Chatbox.MarkChannelRead(channel);

            return;
        }

        if (_focused && !Chatbox.WindowFocused && channel != null && channel.UnreadCount > 0)
            ScrollToUnread(channel, Chatbox.BeginViewingActive());

        Chatbox.WindowFocused = _focused;

        if (_focused) Chatbox.MarkActiveRead();
    }

    private void ConsumeFocusRequest()
    {
        if (Detached)
        {
            if (!_requestFocus) return;

            _requestFocus = false;
            _focusInput = true;
            ImGui.SetWindowFocus();
            return;
        }

        if (!Chatbox.RequestInputFocus) return;

        Chatbox.RequestInputFocus = false;
        _focusInput = true;
        ImGui.SetWindowFocus();
    }

    public void RequestFocus() => _requestFocus = true;

    private void DrawWithTabs(float inputHeight)
    {
        if (Config.TabSide == ChatboxTabSide.Top)
        {
            DrawHorizontalNav();
            DrawBody(inputHeight);
            return;
        }

        var navHeight = MeasureHorizontalNavHeight();

        using (var body = ImRaii.Child("##chatbox-body", new Vector2(0, -navHeight), false))
        {
            if (body) DrawBody(inputHeight);
        }

        DrawHorizontalNav();
    }

    private void DrawSideBySide(float inputHeight)
    {
        var isRail = Config.NavStyle == ChatboxNavStyle.ServerRail;
        var scale = ImGuiHelpers.GlobalScale * UiTheme.GlobalFontScale;
        var total = MathF.Max(ImGui.GetContentRegionAvail().X, 120f);
        var gap = _theme.Gap(0.3f);
        var reserved = SplitterWidth() + gap * 2f;

        var minNav = MinNavWidth(isRail, scale);
        var maxNav = MathF.Max(minNav, total - reserved - 160f * scale);
        var navWidth = Math.Clamp(StoredNavWidth * scale, minNav, maxNav);
        var bodyWidth = MathF.Max(80f, total - navWidth - reserved);

        if (Config.NavSide == ChatboxNavSide.Right)
        {
            DrawBodyColumn(bodyWidth, inputHeight);
            ImGui.SameLine(0, gap);
            DrawNavSplitter(scale, minNav, maxNav, -1f);
            ImGui.SameLine(0, gap);
            DrawNavColumn(navWidth, isRail);
        }
        else
        {
            DrawNavColumn(navWidth, isRail);
            ImGui.SameLine(0, gap);
            DrawNavSplitter(scale, minNav, maxNav, 1f);
            ImGui.SameLine(0, gap);
            DrawBodyColumn(bodyWidth, inputHeight);
        }
    }

    private float StoredNavWidth
    {
        get => Config.NavStyle == ChatboxNavStyle.ServerRail ? Config.RailWidth : Config.NavWidth;
        set
        {
            if (Config.NavStyle == ChatboxNavStyle.ServerRail) Config.RailWidth = value;
            else Config.NavWidth = value;
        }
    }

    private float SplitterWidth() =>
        Config.ResizableNav ? MathF.Max(4f, 5f * ImGuiHelpers.GlobalScale) : 0f;

    private static float MinNavWidth(bool isRail, float scale) =>
        (isRail ? 40f : 90f) * scale;

    private void DrawNavSplitter(float scale, float minNav, float maxNav, float direction)
    {
        if (!Config.ResizableNav) return;

        var width = SplitterWidth();
        var height = MathF.Max(ImGui.GetContentRegionAvail().Y, 1f);
        var origin = ImGui.GetCursorScreenPos();

        ImGui.InvisibleButton("##chatbox-nav-splitter", new Vector2(width, height));

        var hovered = ImGui.IsItemHovered();
        var held = ImGui.IsItemActive();
        if (hovered || held) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);

        if (held)
        {
            var delta = ImGui.GetIO().MouseDelta.X * direction;
            if (MathF.Abs(delta) > 0.01f)
                StoredNavWidth = Math.Clamp(StoredNavWidth * scale + delta, minNav, maxNav) / scale;
        }

        if (ImGui.IsItemDeactivated()) _plugin.Config.Save();

        if (!hovered && !held) return;

        var color = held ? _theme.Accent : _theme.Hover;
        ImGui.GetWindowDrawList().AddRectFilled(
            origin,
            origin + new Vector2(width, height),
            ImGui.GetColorU32(color),
            width * 0.5f);
    }

    private void DrawNavColumn(float navWidth, bool isRail)
    {
        using var child = ImRaii.Child("##chatbox-nav", new Vector2(navWidth, 0), false);
        if (!child) return;

        if (isRail) DrawServerRail();
        else DrawChannelList();
    }

    private void DrawBodyColumn(float bodyWidth, float inputHeight)
    {
        using var child = ImRaii.Child("##chatbox-body", new Vector2(bodyWidth, 0), false);
        if (!child) return;

        DrawBody(inputHeight);
    }

    private void DrawBody(float inputHeight)
    {
        var channel = Target;
        if (channel == null)
        {
            DrawEmptyState();
            return;
        }

        if (_searchOpen)
        {
            DrawSearchBody(inputHeight);
            DrawInputBar(channel);
            return;
        }

        using (var list = ImRaii.Child(WidgetIds(channel.Id).Body, new Vector2(0, -inputHeight), false))
        {
            if (list)
            {
                DrawMessages(channel);
                DrawAutocomplete();
            }
        }

        DrawInputBar(channel);
    }

    private void DrawEmptyState()
    {
        var available = ImGui.GetContentRegionAvail();
        ImGui.Dummy(new Vector2(0, available.Y * 0.35f));

        if (Detached)
        {
            CenteredText("This conversation is not open.", _theme.MutedText);
            CenteredText("Reopen it from Settings → Chatbox → Conversations.", _theme.MutedText);
            return;
        }

        CenteredText("No channels configured.", _theme.MutedText);
        CenteredText("Add one in Settings → Chatbox.", _theme.MutedText);
    }

    private static void CenteredText(string text, Vector4 color)
    {
        var width = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - width) * 0.5f + ImGui.GetCursorPosX());
        ImGui.TextColored(color, text);
    }

    private float MeasureInputHeight()
    {
        if (_measuredInputHeight > 0f) return _measuredInputHeight;

        var height = ImGui.GetFrameHeightWithSpacing();
        if (_replyTarget != null && Config.EnableReplies)
            height += ImGui.GetTextLineHeightWithSpacing() + _theme.Gap(0.5f);

        if (Chatbox.CanSend(Target))
            height += _theme.PickerCaptionHeight();

        return height;
    }

    public void BeginReply(ChatboxMessage message)
    {
        if (!Config.EnableReplies) return;
        _replyTarget = Chatbox.BuildReplyRef(message);
        _focusInput = true;
    }

    public void CancelReply() => _replyTarget = null;

    public void OnClose()
    {
        ForceCloseItemTooltip();

        if (Detached) Chatbox.SetChannelViewed(PinnedChannelId!, false);

        _focused = false;
        _windowWasFocused = false;
        _readArmed = false;
        _readViewed = false;
    }

    public void Dispose()
    {
        UnhookTranslation();
        DisposeSearch();
        _emoteFont.Dispose();
    }
}
