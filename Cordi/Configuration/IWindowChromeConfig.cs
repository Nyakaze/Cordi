namespace Cordi.Configuration;

public interface IWindowChromeConfig
{
    bool IgnoreEsc { get; }
    bool HideTitleBar { get; }
    float BackgroundOpacity { get; }
    bool LockPosition { get; }
    bool LockSize { get; }
}
