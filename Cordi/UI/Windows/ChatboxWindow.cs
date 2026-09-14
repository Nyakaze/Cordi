using System;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Services.Chatbox;
using Cordi.UI.Panels;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Cordi.UI.Windows;

public sealed class ChatboxWindow : ThemedWindow, IDisposable
{
    private const string WindowId = "###CordiChatbox";

    private readonly CordiPlugin _plugin;
    private readonly ChatboxSurface _surface;

    private int _titleMentions = -1;
    private bool _titleUnread;
    private bool _titleFlash;
    private bool _titleDot;

    public ChatboxWindow(CordiPlugin plugin) : base(
        "Chatbox" + WindowId, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _plugin = plugin;
        _surface = new ChatboxSurface(plugin, _theme);

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 260),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        Size = new Vector2(720, 460);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    private ChatboxConfig Config => _plugin.Config.Chatbox;
    private ChatboxService Chatbox => _plugin.Chatbox;

    protected override IWindowChromeConfig Chrome => Config;

    public override bool DrawConditions()
    {
        if (!Config.Enabled) return false;
        if (Config.HideWhenNotLoggedIn && !Cordi.Service.ClientState.IsLoggedIn) return false;
        return !Chatbox.CinematicHidesChatbox;
    }

    protected override void OnPreDraw()
    {
        AllowClickthrough = Config.ClickThroughWhenUnfocused;
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        var mentions = Chatbox.TotalMentions;
        var unread = Chatbox.TotalUnread;

        if (mentions == _titleMentions
            && (unread > 0) == _titleUnread
            && Config.FlashTitleOnMention == _titleFlash
            && Config.ShowUnreadDot == _titleDot)
            return;

        _titleMentions = mentions;
        _titleUnread = unread > 0;
        _titleFlash = Config.FlashTitleOnMention;
        _titleDot = Config.ShowUnreadDot;

        var badge = mentions > 0 && Config.FlashTitleOnMention
            ? $"Chatbox ({mentions})"
            : unread > 0 && Config.ShowUnreadDot
                ? "Chatbox •"
                : "Chatbox";

        WindowName = badge + WindowId;
    }

    public override void Draw() => _surface.Draw();

    public override void OnClose() => _surface.OnClose();

    public void InsertText(string text) => _surface.InsertText(text);

    public bool JumpToMessage(string channelId, long seq) => _surface.JumpToMessage(channelId, seq);

    public void Dispose() => _surface.Dispose();
}
