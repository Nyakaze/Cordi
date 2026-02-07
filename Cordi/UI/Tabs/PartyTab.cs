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

    private DateTime _lastChannelFetch = DateTime.MinValue;
    private readonly TimeSpan _cacheInterval = TimeSpan.FromSeconds(5);
    private List<DiscordChannel> _cachedTextChannels = new();

    public PartyTab(CordiPlugin plugin, UiTheme theme)
    {
        this.plugin = plugin;
        this.theme = theme;
    }

    public void Draw()
    {
        bool enabled = true;

        if (DateTime.Now - _lastChannelFetch > _cacheInterval)
        {
            RefreshChannelCache();
        }

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
                if (theme.Checkbox("Include self", ref includeSelf))
                {
                    plugin.Config.Party.IncludeSelf = includeSelf;
                    plugin.Config.Save();
                }
                ImGui.TextDisabled("Send Discord notifications for yourself and fetch your information.");

                theme.SpacerY(0.5f);

                bool showSavageProgress = plugin.Config.Party.ShowSavageProgress;
                if (theme.Checkbox("Show savage progress", ref showSavageProgress))
                {
                    plugin.Config.Party.ShowSavageProgress = showSavageProgress;
                    plugin.Config.Save();
                }
                ImGui.TextDisabled("Fetch and display savage raid progress from Tomestone.");

                theme.SpacerY(0.5f);

                bool showGearLevel = plugin.Config.Party.ShowGearLevel;
                if (theme.Checkbox("Show gearlevel", ref showGearLevel))
                {
                    plugin.Config.Party.ShowGearLevel = showGearLevel;
                    plugin.Config.Save();
                }
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
                if (theme.Checkbox("Enable Party Tracker", ref partyEnabled))
                {
                    plugin.Config.Party.Enabled = partyEnabled;
                    plugin.Config.Save();
                }

                bool excludeAlliance = plugin.Config.Party.ExcludeAlliance;
                if (theme.Checkbox("Exclude Alliance Parties", ref excludeAlliance))
                {
                    plugin.Config.Party.ExcludeAlliance = excludeAlliance;
                    plugin.Config.Save();
                }
                ImGui.TextDisabled("Prevents tracking alliance parties (up to 24 members).");

                theme.SpacerY(0.5f);

                ImGui.Text("Discord Notification Channel:");

                var currentChannelId = plugin.Config.Party.DiscordChannelId;
                string preview = "Select a channel...";

                if (!string.IsNullOrEmpty(currentChannelId) && _cachedTextChannels != null)
                {
                    var ch = _cachedTextChannels.FirstOrDefault(c => c.Id.ToString() == currentChannelId);
                    if (ch != null) preview = $"#{ch.Name}";
                    else preview = currentChannelId;
                }

                ImGui.SetNextItemWidth(avail);
                if (ImGui.BeginCombo("##partyChannel", preview))
                {
                    if (ImGui.Selectable("None", string.IsNullOrEmpty(currentChannelId)))
                    {
                        plugin.Config.Party.DiscordChannelId = string.Empty;
                        plugin.Config.Save();
                    }
                    theme.HoverHandIfItem();

                    if (_cachedTextChannels != null)
                    {
                        foreach (var channel in _cachedTextChannels)
                        {
                            bool isSelected = channel.Id.ToString() == currentChannelId;
                            if (ImGui.Selectable($"#{channel.Name}", isSelected))
                            {
                                plugin.Config.Party.DiscordChannelId = channel.Id.ToString();

                                if (!plugin.Config.Party.DiscordEnabled) plugin.Config.Party.DiscordEnabled = true;
                                plugin.Config.Save();
                            }
                            if (isSelected) ImGui.SetItemDefaultFocus();
                            theme.HoverHandIfItem();
                        }
                    }
                    ImGui.EndCombo();
                }
                theme.HoverHandIfItem();
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
                if (ImGui.Checkbox("Notify on Party Join", ref notifyJoin))
                {
                    plugin.Config.Party.NotifyJoin = notifyJoin;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();

                bool notifyLeave = plugin.Config.Party.NotifyLeave;
                if (ImGui.Checkbox("Notify on Party Leave", ref notifyLeave))
                {
                    plugin.Config.Party.NotifyLeave = notifyLeave;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();

                bool notifyFull = plugin.Config.Party.NotifyFull;
                if (ImGui.Checkbox("Notify when Party Fills (8/8)", ref notifyFull))
                {
                    plugin.Config.Party.NotifyFull = notifyFull;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();
            }
        );
    }

    private void RefreshChannelCache()
    {
        _lastChannelFetch = DateTime.Now;
        var allChannels = plugin.Discord.Client?.Guilds.Values
            .SelectMany(g => g.Channels.Values)
            .ToList();

        if (allChannels == null)
        {
            _cachedTextChannels.Clear();
        }
        else
        {
            _cachedTextChannels = allChannels.Where(c => c.Type == ChannelType.Text).ToList();
        }
    }
}
