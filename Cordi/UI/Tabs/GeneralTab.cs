using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using Cordi.Core;
using Cordi.UI.Themes;
using Cordi.Configuration;

namespace Cordi.UI.Tabs;

public class GeneralTab : ConfigTabBase
{
    public override string Label => "General";

    public GeneralTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
    }
    
    protected override IReadOnlyList<(string Label, Action Draw)> GetSubTabs()
    {
        return new (string, Action)[]
        {
            
        };
    }

    public override void Draw()
    {
        theme.SpacerY(2f);

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);

        

        
    }
}
