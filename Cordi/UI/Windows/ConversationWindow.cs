using System;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Services.Chatbox;
using Cordi.UI.Panels;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Cordi.UI.Windows;

public sealed class ConversationWindow : ThemedWindow, IDisposable
{
    private readonly CordiPlugin _plugin;
    private readonly ChatboxSurface _surface;
    private readonly string _windowId;
    private readonly ConversationConfig _entry;
    private bool _bodyDrawn;
    private bool _titleUnread;
    private string _titleLabel = string.Empty;

    public ConversationWindow(CordiPlugin plugin, ConversationConfig entry) : base(
        entry.Label + "###CordiConversation-" + entry.Id,
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _plugin = plugin;
        _entry = entry;
        _windowId = "###CordiConversation-" + entry.Id;
        ChannelId = entry.Id;
        _surface = new ChatboxSurface(plugin, _theme, entry.Id);

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(280, 180),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        Size = entry.HasWindowGeometry
            ? new Vector2(entry.WindowWidth, entry.WindowHeight)
            : new Vector2(460, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
        AllowClickthrough = false;

        if (!entry.HasWindowGeometry) return;

        Position = new Vector2(entry.WindowX, entry.WindowY);
        PositionCondition = ImGuiCond.FirstUseEver;
    }

    public string ChannelId { get; }

    private ChatboxConfig Config => _plugin.Config.Chatbox;

    private ChatboxService Chatbox => _plugin.Chatbox;

    protected override IWindowChromeConfig Chrome => Config.Conversations.Window;

    public override bool DrawConditions()
    {
        if (!Config.Enabled || !Config.Conversations.Enabled) return false;
        if (!Cordi.Service.ClientState.IsLoggedIn && Config.HideWhenNotLoggedIn) return false;
        return base.DrawConditions();
    }

    protected override void OnPreDraw()
    {
        _bodyDrawn = false;
        UpdateTitle();
    }

    public override void PostDraw()
    {
        if (!_bodyDrawn) Chatbox.SetChannelViewed(ChannelId, false);

        base.PostDraw();
    }

    protected override Vector4? TitleFlash
    {
        get
        {
            var settings = Config.Conversations;
            if (!settings.Flash) return null;

            var channel = Chatbox.GetChannel(ChannelId);
            if (channel == null || channel.UnreadCount == 0) return null;

            return _theme.TitleFlash(_theme.Accent, settings.FlashPeriodMs, !settings.NoFlashing);
        }
    }

    private void UpdateTitle()
    {
        var entry = Chatbox.FindConversation(ChannelId);
        var label = entry?.Label ?? ChannelId;
        var channel = Chatbox.GetChannel(ChannelId);
        var unread = (channel?.UnreadCount ?? 0) > 0;

        if (unread == _titleUnread && string.Equals(label, _titleLabel, StringComparison.Ordinal)) return;

        _titleUnread = unread;
        _titleLabel = label;

        WindowName = (unread ? label + " •" : label) + _windowId;
    }

    public void Activate()
    {
        IsOpen = true;
        RequestFocus = true;
        _surface.RequestFocus();
    }

    public override void Draw()
    {
        _bodyDrawn = true;
        CaptureGeometry();
        _surface.Draw();
    }

    private void CaptureGeometry()
    {
        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();

        if (size.X <= 0f || size.Y <= 0f) return;

        _entry.WindowX = pos.X;
        _entry.WindowY = pos.Y;
        _entry.WindowWidth = size.X;
        _entry.WindowHeight = size.Y;
    }

    public override void OnClose()
    {
        _surface.OnClose();
        _plugin.Config.Save();
    }

    public void Dispose() => _surface.Dispose();
}
