using System;
using System.Linq;
using System.Numerics;
using Cordi.Packets.Handler.Chat;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using DSharpPlus;
using DSharpPlus.Entities;
using Dalamud.Interface;
using System.Collections.Generic;

using Cordi.Configuration;
using Cordi.Core;

using Cordi.UI.Tabs;
using Cordi.UI.Themes;

namespace Cordi.UI.Windows;

public sealed class ConfigWindow : Window, IDisposable
{

    static readonly IPluginLog Logger = Service.Log;
    private readonly CordiPlugin plugin;
    private readonly UiTheme theme = new UiTheme();


    private GeneralTab generalTab;
    private ChatsTab chatsTab;
    private CordiPeepTab cordiPeepTab;
#if DEBUG
    private DebugTab debugTab;
#endif
    private EmoteLogTab emoteLogTab;
    private DiscordActivityTab discordActivityTab;

    private int selectedTab = 0;

    public ConfigWindow(CordiPlugin plugin)
        : base("Cordi", ImGuiWindowFlags.None)
    {
        this.plugin = plugin;


        this.generalTab = new GeneralTab(plugin, theme);
        this.chatsTab = new ChatsTab(plugin, theme);
        this.cordiPeepTab = new CordiPeepTab(plugin, theme);
        this.emoteLogTab = new EmoteLogTab(plugin, theme);
#if DEBUG
        this.debugTab = new DebugTab(plugin, theme);
#endif
        this.discordActivityTab = new DiscordActivityTab(plugin, theme);

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        RespectCloseHotkey = true;
    }


    public override void PreDraw() => theme.PushWindow();
    public override void PostDraw() => theme.PopWindow();

    public async override void Draw()
    {
        bool botStarted = plugin.Config.Discord.BotStarted;

        var botStatus = theme.BadgeToggle(
            id: "##botStatus",
            ref botStarted,
            label: "Discord Bot",
            height: 25f,
            iconOnRight: false,
            bgOn: UiTheme.ColorSuccess,
            bgOff: UiTheme.ColorDanger
            );
        if (botStatus.StateChanged)
        {
            if (botStarted)
                plugin.Discord.Start();
            else
                plugin.Discord.Stop();
        }

        theme.SpacerY(3.5f);
        ImGui.Separator();




        ImGui.BeginChild("##sidebar", new Vector2(150, 0), false);

        ImGui.SetWindowFontScale(1.1f);


        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 12f);
        ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(0.5f, 0.5f));

        float itemHeight = 40f;
        Vector2 buttonSize = new Vector2(-1, itemHeight);


        void PushBtnColor(bool active)
        {
            if (active)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, theme.Accent);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, theme.Accent);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, theme.Accent);
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0)); // Transparent
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, theme.TabHovered);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, theme.TabActive);
            }
        }


        PushBtnColor(selectedTab == 0);
        if (ImGui.Button("General", buttonSize)) selectedTab = 0;
        theme.HoverHandIfItem();
        ImGui.PopStyleColor(3);
        ImGui.Spacing();


        PushBtnColor(selectedTab == 1);
        if (ImGui.Button("Chats", buttonSize)) selectedTab = 1;
        theme.HoverHandIfItem();
        ImGui.PopStyleColor(3);
        ImGui.Spacing();


        PushBtnColor(selectedTab == 4);
        if (ImGui.Button("Emote Log", buttonSize)) selectedTab = 4;
        theme.HoverHandIfItem();
        ImGui.PopStyleColor(3);
        ImGui.Spacing();


        PushBtnColor(selectedTab == 2);
        if (ImGui.Button("Peepers", buttonSize)) selectedTab = 2;
        theme.HoverHandIfItem();
        ImGui.PopStyleColor(3);
        ImGui.Spacing();


        PushBtnColor(selectedTab == 5);
        if (ImGui.Button("Activity", buttonSize)) selectedTab = 5;
        theme.HoverHandIfItem();
        ImGui.PopStyleColor(3);
        ImGui.Spacing();


#if DEBUG
        PushBtnColor(selectedTab == 3);
        if (ImGui.Button("Debug", buttonSize)) selectedTab = 3;
        theme.HoverHandIfItem();
        ImGui.PopStyleColor(3);
        ImGui.Spacing();
#endif

        ImGui.PopStyleVar(2);
        ImGui.SetWindowFontScale(1.0f);


        ImGui.EndChild();
        ImGui.SameLine();

        ImGui.BeginChild("##content", new Vector2(0, 0), false);

        switch (selectedTab)
        {
            case 0:

                generalTab.Draw();
                break;

            case 1:

                chatsTab.Draw();
                break;

            case 2:

                cordiPeepTab.Draw();
                break;

#if DEBUG
            case 3:

                debugTab.Draw();
                break;
#endif
            case 4:

                emoteLogTab.Draw();
                break;
            case 5:

                discordActivityTab.Draw();
                break;
        }
        ImGui.EndChild();
    }

    public void Dispose() { }
}
