using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.UI.Themes;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Bindings.ImGui;
using DSharpPlus.Entities;
using DSharpPlus;
using System;

namespace Cordi.UI.Tabs;

public class PartyTab
{
    private readonly CordiPlugin plugin;
    private readonly UiTheme theme;


    public PartyTab(CordiPlugin plugin, UiTheme theme)
    {
        this.plugin = plugin;
        this.theme = theme;
    }

    public void Draw()
    {
        bool enabled = true;

        plugin.ChannelCache.RefreshIfNeeded();

        DrawGeneralCard(ref enabled);

        theme.SpacerY(1f);
        ImGui.Separator();
        theme.SpacerY(1f);

        DrawTriggerCard(ref enabled);

        theme.SpacerY(1f);
        ImGui.Separator();
        theme.SpacerY(1f);

        DrawIntegrationCard(ref enabled);
    }

    private void DrawIntegrationCard(ref bool cardEnabled)
    {
        theme.DrawPluginCardAuto(
            id: "party-integrations",
            title: "Integrations",
            enabled: ref cardEnabled,
            drawContent: (avail) =>
            {
                ImGui.TextColored(UiTheme.ColorDangerText, "(INFORMATIONS MAY NOT BE ACCURATE)");

                bool includeSelf = plugin.Config.Party.IncludeSelf;
                theme.ConfigCheckbox("Include self", ref includeSelf, () =>
                {
                    plugin.Config.Party.IncludeSelf = includeSelf;
                    plugin.Config.Save();
                });
                ImGui.TextDisabled("Send Discord notifications for yourself and fetch your information.");

                theme.SpacerY(0.5f);

                bool showSavageProgress = plugin.Config.Party.ShowSavageProgress;
                theme.ConfigCheckbox("Show savage progress", ref showSavageProgress, () =>
                {
                    plugin.Config.Party.ShowSavageProgress = showSavageProgress;
                    plugin.Config.Save();
                });
                ImGui.TextDisabled("Fetch and display savage raid progress from Tomestone.");

                theme.SpacerY(0.5f);

                bool showGearLevel = plugin.Config.Party.ShowGearLevel;
                theme.ConfigCheckbox("Show gearlevel", ref showGearLevel, () =>
                {
                    plugin.Config.Party.ShowGearLevel = showGearLevel;
                    plugin.Config.Save();
                });
                ImGui.TextDisabled("Shows the character's current gearlevel from Tomestone.");
            }
        );
    }

    private void DrawGeneralCard(ref bool cardEnabled)
    {
        theme.SpacerY(2f);

        theme.DrawPluginCardAuto(
            id: "party-general",
            title: "General",
            enabled: ref cardEnabled,
            drawContent: (avail) =>
            {
                bool partyEnabled = plugin.Config.Party.Enabled;
                theme.ConfigCheckbox("Enable Party Tracker", ref partyEnabled, () =>
                {
                    plugin.Config.Party.Enabled = partyEnabled;
                    plugin.Config.Save();
                });

                bool excludeAlliance = plugin.Config.Party.ExcludeAlliance;
                theme.ConfigCheckbox("Exclude Alliance Parties", ref excludeAlliance, () =>
                {
                    plugin.Config.Party.ExcludeAlliance = excludeAlliance;
                    plugin.Config.Save();
                });
                ImGui.TextDisabled("Prevents tracking alliance parties (up to 24 members).");

                theme.SpacerY(0.5f);

                ImGui.Text("Discord Notification Channel:");

                theme.ChannelPicker(
                    "partyChannel",
                    plugin.Config.Party.DiscordChannelId,
                    plugin.ChannelCache.TextChannels,
                    (newId) =>
                    {
                        plugin.Config.Party.DiscordChannelId = newId;
                        if (!string.IsNullOrEmpty(newId) && !plugin.Config.Party.DiscordEnabled)
                        {
                            plugin.Config.Party.DiscordEnabled = true;
                        }
                        plugin.Config.Save();
                    },
                    defaultLabel: "Select a channel..."
                );
            }
        );
    }

    private void DrawTriggerCard(ref bool cardEnabled)
    {
        theme.DrawPluginCardAuto(
            id: "party-triggers",
            title: "Triggers",
            enabled: ref cardEnabled,
            drawContent: (avail) =>
            {
                bool notifyJoin = plugin.Config.Party.NotifyJoin;
                theme.ConfigCheckbox("Notify on Party Join", ref notifyJoin, () =>
                {
                    plugin.Config.Party.NotifyJoin = notifyJoin;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                bool notifyLeave = plugin.Config.Party.NotifyLeave;
                theme.ConfigCheckbox("Notify on Party Leave", ref notifyLeave, () =>
                {
                    plugin.Config.Party.NotifyLeave = notifyLeave;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                bool notifyFull = plugin.Config.Party.NotifyFull;
                theme.ConfigCheckbox("Notify when Party Fills (8/8)", ref notifyFull, () =>
                {
                    plugin.Config.Party.NotifyFull = notifyFull;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();
            }
        );
    }

}
