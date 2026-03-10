using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

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
        bool enabled = true;

        theme.DrawPluginCardAuto(
            id: "dsc-commands",
            enabled: ref enabled,
            showCheckbox: false,
            title: "Discord Commands",
            drawContent: (avail) =>
            {
                ImGui.TextColored(UiTheme.ColorDangerText, "WARNING: This feature is experimental!");
                bool allowCommands = plugin.Config.Discord.AllowDiscordCommands;
                if (ImGui.Checkbox("Enable Discord Commands", ref allowCommands))
                {
                    plugin.Config.Discord.AllowDiscordCommands = allowCommands;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();
                if (plugin.Config.Discord.AllowDiscordCommands)
                {
                    ImGui.Indent();

                    string prefix = plugin.Config.Discord.CommandPrefix;
                    if (ImGui.InputText("Command Prefix", ref prefix, 10))
                    {
                        plugin.Config.Discord.CommandPrefix = prefix;
                        plugin.Config.Save();
                    }

                    ImGui.TextWrapped("Available commands:");
                    ImGui.BulletText("!target PlayerName World - Target a player");
                    ImGui.BulletText("!emote emotename PlayerName World - Emote at a player");

                    ImGui.Unindent();
                }
            }
        );

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);

        

        
    }
}
