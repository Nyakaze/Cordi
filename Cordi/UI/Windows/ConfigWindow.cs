using System;
using System.Linq;
using System.Numerics;
using Cordi.Packets.Handler.Chat;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
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
    private PartyTab partyTab;
    private RememberMeTab rememberMeTab;
    private CombinedWindowTab combinedWindowTab;
    private SettingsTab settingsTab;


    private int selectedTab = 1;

    public ConfigWindow(CordiPlugin plugin)
        : base("Cordi", ImGuiWindowFlags.None)
    {
        this.plugin = plugin;


        this.generalTab = new GeneralTab(plugin, theme);
        this.chatsTab = new ChatsTab(plugin, theme);
        this.cordiPeepTab = new CordiPeepTab(plugin, theme);
        this.emoteLogTab = new EmoteLogTab(plugin, theme);
        this.discordActivityTab = new DiscordActivityTab(plugin, theme);
#if DEBUG
        this.debugTab = new DebugTab(plugin, theme);
#endif
        this.partyTab = new PartyTab(plugin, theme);
        this.rememberMeTab = new RememberMeTab(plugin, theme);
        this.combinedWindowTab = new CombinedWindowTab(plugin, theme);
        this.settingsTab = new SettingsTab(plugin, theme);

        // Apply saved font settings
        UiTheme.GlobalFontScale = plugin.Config.Font.GlobalScale;
        UiTheme.GlobalFontBold = plugin.Config.Font.Bold;

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
        theme.ApplyFontScale();

        bool botStarted = plugin.Config.Discord.BotStarted;

        using (ImRaii.Disabled(plugin.Discord.IsBusy))
        {
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
        }
        theme.HoverHandIfItem();

        DrawStats();
        theme.SpacerY(1f);
        ImGui.Separator();




        float fontScale = UiTheme.GlobalFontScale;
        using (ImRaii.Child("##sidebar", new Vector2(150 * fontScale, 0), false))
        {
            theme.ApplyFontScale(1.1f);

            using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f))
            using (ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(0.5f, 0.5f)))
            {
                float itemHeight = 40f * fontScale;
                Vector2 buttonSize = new Vector2(-1, itemHeight);

                void DrawSidebarButton(string label, int tabIndex, bool isFirst = false)
                {
                    if (!isFirst)
                        theme.SpacerY();
                    using (ImRaii.PushColor(ImGuiCol.Button, selectedTab == tabIndex ? theme.Accent : new Vector4(0, 0, 0, 0)))
                    using (ImRaii.PushColor(ImGuiCol.ButtonHovered, selectedTab == tabIndex ? theme.Accent : theme.TabHovered))
                    using (ImRaii.PushColor(ImGuiCol.ButtonActive, selectedTab == tabIndex ? theme.Accent : theme.TabActive))
                    {
                        if (ImGui.Button(label, buttonSize)) selectedTab = tabIndex;
                        theme.HoverHandIfItem();
                    }
                    ImGui.Spacing();
                }

                theme.SpacerY(1f);
                // DrawSidebarButton("General", 0);
                DrawSidebarButton("Chats", 1, true);
                DrawSidebarButton("Emote Log", 4);
                DrawSidebarButton("Peepers", 2);
                DrawSidebarButton("Combined", 9);
                DrawSidebarButton("Activity", 5);
                DrawSidebarButton("Party", 6);
                DrawSidebarButton("Remember Me", 7);
                DrawSidebarButton("Settings", 12);

#if DEBUG
                DrawSidebarButton("Debug", 3);
#endif
            }
            theme.ApplyFontScale();
        }
        ImGui.SameLine();

        using (ImRaii.Child("##content", new Vector2(0, 0), false))
        {
            theme.ApplyFontScale();

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

                case 6:
                    partyTab.Draw();
                    break;

                case 7:
                    rememberMeTab.Draw();
                    break;

                case 9:
                    combinedWindowTab.Draw();
                    break;

                case 12:
                    settingsTab.Draw();
                    break;
            }
        }
    }

    private void DrawStats()
    {
        var stats = plugin.Config.Stats;
        bool showMessages = plugin.Config.Chat.Mappings.Count > 0;
        bool showPeeps = plugin.Config.CordiPeep.Enabled;
        bool showEmotes = plugin.Config.EmoteLog.Enabled;

        if (!showMessages && !showPeeps && !showEmotes) return;

        // Calculate total width first
        float totalWidth = 0f;
        float spacing = 15f;
        string msgText = $"Msgs: {stats.TotalMessages}";
        string peepText = $"Peeps: {stats.TotalPeepsTracked}";
        string emoteText = $"Emotes: {stats.TotalEmotesTracked}";

        if (showMessages) totalWidth += ImGui.CalcTextSize(msgText).X;
        if (showPeeps) totalWidth += (totalWidth > 0 ? spacing : 0) + ImGui.CalcTextSize(peepText).X;
        if (showEmotes) totalWidth += (totalWidth > 0 ? spacing : 0) + ImGui.CalcTextSize(emoteText).X;

        // Get the badge rect (last item) for vertical alignment
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();

        // Calculate start X position (Right aligned)
        // GetWindowContentRegionMax is relative to WindowPos
        float rightEdge = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        float startX = rightEdge - totalWidth;

        // Calculate Y position (Vertically centered)
        var centerY = (min.Y + max.Y) * 0.5f;
        var textY = centerY - ImGui.GetTextLineHeight() * 0.5f;

        ImGui.SetCursorScreenPos(new Vector2(startX, textY));

        // Helper to draw a single stat
        void DrawStat(string label, long value, string tooltip)
        {
            ImGui.TextColored(theme.MutedText, $"{label}: {value}");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(tooltip);
            }
        }

        bool first = true;

        if (showMessages)
        {
            DrawStat("Msgs", stats.TotalMessages, "Total messages processed");
            first = false;
        }

        if (showPeeps)
        {
            if (!first) ImGui.SameLine(0, spacing);
            DrawStat("Peeps", stats.TotalPeepsTracked, "Total players tracked");
            first = false;
        }

        if (showEmotes)
        {
            if (!first) ImGui.SameLine(0, spacing);
            DrawStat("Emotes", stats.TotalEmotesTracked, "Total emotes tracked");
        }
    }

    public void Dispose() { }
}
