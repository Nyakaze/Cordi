using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

using Cordi.Core;
using Cordi.UI.Themes;
using Cordi.Configuration;

namespace Cordi.UI.Tabs;

public class GeneralTab
{
    private readonly CordiPlugin plugin;
    private readonly UiTheme theme;
    private string botToken = string.Empty;

    public GeneralTab(CordiPlugin plugin, UiTheme theme)
    {
        this.plugin = plugin;
        this.theme = theme;

        this.botToken = plugin.Config.Discord.BotToken ?? string.Empty;
    }

    public void Draw()
    {


        theme.SpacerY(2f);
        bool enabled = true;

        theme.DrawPluginCardAuto(
            id: "dsc-bot-token",
            enabled: ref enabled,
            showCheckbox: false,
            title: "Discord Bot Token",
            drawContent: (avail) =>
            {
                string btnLabel = "Save Token";
                var btnSize = ImGui.CalcTextSize(btnLabel);
                var style = ImGui.GetStyle();
                float btnWidth = btnSize.X + style.FramePadding.X * 2;
                float spacing = style.ItemSpacing.X;


                ImGui.PushItemWidth(avail - btnWidth - spacing);
                ImGui.InputText("##bot-token-input", ref botToken, 256);
                ImGui.PopItemWidth();

                ImGui.SameLine();

                if (ImGui.Button(btnLabel))
                {
                    plugin.Config.Discord.BotToken = botToken;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();
            }
        );


    }
}
