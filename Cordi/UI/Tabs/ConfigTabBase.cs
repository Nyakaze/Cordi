using Cordi.Core;
using Cordi.UI.Themes;

namespace Cordi.UI.Tabs;

public abstract class ConfigTabBase
{
    protected readonly CordiPlugin plugin;
    protected readonly UiTheme theme;

    protected ConfigTabBase(CordiPlugin plugin, UiTheme theme)
    {
        this.plugin = plugin;
        this.theme = theme;
    }

    public abstract string Label { get; }

    public abstract void Draw();
}
