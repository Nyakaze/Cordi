using Cordi.Configuration;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace Cordi.UI.Windows;

public abstract class ThemedWindow : Window
{
    protected readonly UiTheme _theme = new();

    private ImRaii.ColorDisposable? _opacityScope;
    private ImRaii.StyleDisposable? _borderScope;

    protected ThemedWindow(string name, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
        : base(name, flags)
    {
        BaseFlags = flags;
    }

    protected ImGuiWindowFlags BaseFlags { get; }

    protected abstract IWindowChromeConfig Chrome { get; }

    protected virtual void OnPreDraw()
    {
    }

    public override void PreDraw()
    {
        var chrome = Chrome;

        Flags = BaseFlags;
        if (chrome.LockPosition) Flags |= ImGuiWindowFlags.NoMove;
        if (chrome.LockSize) Flags |= ImGuiWindowFlags.NoResize;
        if (chrome.HideTitleBar) Flags |= ImGuiWindowFlags.NoTitleBar;

        RespectCloseHotkey = !chrome.IgnoreEsc;

        OnPreDraw();
        _theme.PushWindow();

        if (chrome.BackgroundOpacity < 1.0f)
        {
            var background = _theme.WindowBg;
            background.W *= chrome.BackgroundOpacity;
            _opacityScope = ImRaii.PushColor(ImGuiCol.WindowBg, background);
        }

        if (chrome.HideTitleBar)
            _borderScope = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 0f);
    }

    public override void PostDraw()
    {
        _borderScope?.Dispose();
        _borderScope = null;
        _opacityScope?.Dispose();
        _opacityScope = null;
        _theme.PopWindow();
    }
}
