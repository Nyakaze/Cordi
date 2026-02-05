using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Services;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using DSharpPlus;
using DSharpPlus.Entities;
using Dalamud.Bindings.ImGui;

using Cordi.Core;
using Cordi.UI.Themes;
using Cordi.Configuration;

namespace Cordi.UI.Tabs;

public class CordiPeepTab
{
    private readonly CordiPlugin plugin;
    private readonly UiTheme theme;


    private DateTime _lastChannelFetch = DateTime.MinValue;
    private readonly TimeSpan _cacheInterval = TimeSpan.FromSeconds(5);
    private List<DiscordChannel> _cachedTextChannels = new();

    private string newBlacklistName = string.Empty;
    private string newBlacklistWorld = string.Empty;

    public CordiPeepTab(CordiPlugin plugin, UiTheme theme)
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


        DrawConfigCard(ref enabled);

        theme.SpacerY(1f);
        ImGui.Separator();
        theme.SpacerY(2f);
        DrawBlacklistCard();
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

    private void DrawConfigCard(ref bool enabled)
    {
        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);


        theme.DrawPluginCardAuto(
            id: "cordi-peep-general",
            title: "General",
            mutedText: "(Monitor player targeting)",
            enabled: ref enabled,
            drawContent: (avail) =>
            {
                bool pEnabled = plugin.Config.CordiPeep.Enabled;
                if (ImGui.Checkbox("Enable Peeper Detection", ref pEnabled))
                {
                    plugin.Config.CordiPeep.Enabled = pEnabled;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();

                bool detectClosed = plugin.Config.CordiPeep.DetectWhenClosed;
                if (ImGui.Checkbox("Detect when Window is Closed", ref detectClosed))
                {
                    plugin.Config.CordiPeep.DetectWhenClosed = detectClosed;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();

                bool dEnabled = plugin.Config.CordiPeep.DiscordEnabled;
                if (ImGui.Checkbox("Enable Discord Notifications", ref dEnabled))
                {
                    plugin.Config.CordiPeep.DiscordEnabled = dEnabled;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();

                theme.SpacerY(0.5f);


                ImGui.Text("Discord Notification Channel:");
                string currentId = plugin.Config.CordiPeep.DiscordChannelId;
                string preview = "None";

                if (!string.IsNullOrEmpty(currentId) && _cachedTextChannels != null)
                {
                    var ch = _cachedTextChannels.FirstOrDefault(c => c.Id.ToString() == currentId);
                    if (ch != null) preview = $"#{ch.Name}";
                    else preview = currentId;
                }


                if (ImGui.BeginCombo("##peepChannel", preview))
                {
                    if (ImGui.Selectable("None", string.IsNullOrEmpty(currentId)))
                    {
                        plugin.Config.CordiPeep.DiscordChannelId = string.Empty;
                        plugin.Config.Save();
                    }

                    if (_cachedTextChannels != null)
                    {
                        foreach (var channel in _cachedTextChannels)
                        {
                            bool isSelected = channel.Id.ToString() == currentId;
                            if (ImGui.Selectable($"#{channel.Name}", isSelected))
                            {
                                plugin.Config.CordiPeep.DiscordChannelId = channel.Id.ToString();
                                plugin.Config.Save();
                            }
                            if (isSelected) ImGui.SetItemDefaultFocus();
                        }
                    }
                    ImGui.EndCombo();
                }
                theme.HoverHandIfItem();
            }
        );

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);


        theme.DrawPluginCardAuto(
            id: "cordi-peep-window",
            title: "In-Game Overlay",
            enabled: ref enabled,
            drawContent: (avail) =>
            {


                float half = avail / 2f - theme.Gap(1f);

                ImGui.BeginGroup();
                bool wEnabled = plugin.Config.CordiPeep.WindowEnabled;
                if (ImGui.Checkbox("Enable Window", ref wEnabled))
                {
                    plugin.Config.CordiPeep.WindowEnabled = wEnabled;
                    plugin.Config.Save();
                    plugin.UpdateCommandVisibility();
                }
                theme.HoverHandIfItem();

                bool openOnLogin = plugin.Config.CordiPeep.OpenOnLogin;
                if (ImGui.Checkbox("Open on Login", ref openOnLogin))
                {
                    plugin.Config.CordiPeep.OpenOnLogin = openOnLogin;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();

                bool wLocked = plugin.Config.CordiPeep.WindowLocked;
                if (ImGui.Checkbox("Lock Position", ref wLocked))
                {
                    plugin.Config.CordiPeep.WindowLocked = wLocked;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();
                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + theme.Gap(2f));

                ImGui.BeginGroup();
                bool wNoResize = plugin.Config.CordiPeep.WindowNoResize;
                if (ImGui.Checkbox("Lock Size", ref wNoResize))
                {
                    plugin.Config.CordiPeep.WindowNoResize = wNoResize;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();

                bool wFocusHover = plugin.Config.CordiPeep.FocusOnHover;
                if (ImGui.Checkbox("Focus on hover", ref wFocusHover))
                {
                    plugin.Config.CordiPeep.FocusOnHover = wFocusHover;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();

                bool wAltClick = plugin.Config.CordiPeep.AltClickExamine;
                if (ImGui.Checkbox("Alt-click Examine", ref wAltClick))
                {
                    plugin.Config.CordiPeep.AltClickExamine = wAltClick;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();
                ImGui.EndGroup();

                theme.SpacerY(0.7f);

                if (theme.SecondaryButton("Open Window Now", new Vector2(avail, 28)))
                {
                    if (plugin.CordiPeepWindow != null)
                        plugin.CordiPeepWindow.IsOpen = true;
                }
                theme.HoverHandIfItem();
            }
        );

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);


        theme.DrawPluginCardAuto(
            id: "cordi-peep-filters",
            title: "Detection Filters",
            enabled: ref enabled,
            drawContent: (avail) =>
            {

                ImGui.BeginGroup();
                bool logParty = plugin.Config.CordiPeep.LogParty;
                if (ImGui.Checkbox("Log party members", ref logParty))
                {
                    plugin.Config.CordiPeep.LogParty = logParty;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();

                bool logAlliance = plugin.Config.CordiPeep.LogAlliance;
                if (ImGui.Checkbox("Log alliance members", ref logAlliance))
                {
                    plugin.Config.CordiPeep.LogAlliance = logAlliance;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();
                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + theme.Gap(2f));

                ImGui.BeginGroup();
                bool logCombat = plugin.Config.CordiPeep.LogCombat;
                if (ImGui.Checkbox("Combat targeters only", ref logCombat))
                {
                    plugin.Config.CordiPeep.LogCombat = logCombat;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();

                bool wIncludeSelf = plugin.Config.CordiPeep.IncludeSelf;
                if (ImGui.Checkbox("Include yourself", ref wIncludeSelf))
                {
                    plugin.Config.CordiPeep.IncludeSelf = wIncludeSelf;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();
                ImGui.EndGroup();
            }
        );

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);


        theme.DrawPluginCardAuto(
            id: "cordi-peep-audio",
            title: "Audio Notifications",
            enabled: ref enabled,
            drawContent: (avail) =>
            {
                bool sEnabled = plugin.Config.CordiPeep.SoundEnabled;
                if (ImGui.Checkbox("Enable Sound Alert", ref sEnabled))
                {
                    plugin.Config.CordiPeep.SoundEnabled = sEnabled;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();

                theme.SpacerY(0.5f);
                ImGui.Text("Custom Audio File:");

                string soundPath = plugin.Config.CordiPeep.SoundPath;
                float btnW = 30f;
                float gap = ImGui.GetStyle().ItemSpacing.X;

                ImGui.SetNextItemWidth(avail - btnW - gap);
                if (ImGui.InputText("##soundPath", ref soundPath, 512))
                {
                    plugin.Config.CordiPeep.SoundPath = soundPath;
                    plugin.Config.Save();
                }
                ImGui.SameLine();
                if (ImGui.Button("...##browseSound", new Vector2(btnW, 0)))
                {

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        var t2 = new System.Threading.Thread(() =>
                        {
                            var ofd = new System.Windows.Forms.OpenFileDialog
                            {
                                Filter = "Audio Files|*.wav;*.mp3;*.wma|All files|*.*",
                                CheckFileExists = true
                            };
                            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                            {
                                plugin.Config.CordiPeep.SoundPath = ofd.FileName;
                                plugin.Config.Save();
                            }
                        });
                        t2.SetApartmentState(System.Threading.ApartmentState.STA);
                        t2.Start();
                    });
                }
                theme.HoverHandIfItem();
                if (string.IsNullOrEmpty(soundPath))
                {
                    ImGui.TextColored(theme.MutedText, "Built-in alert active.");
                }

                theme.SpacerY(1f);


                float colW = (avail - theme.Gap(2f)) / 2f;

                ImGui.BeginGroup();
                float vol = plugin.Config.CordiPeep.SoundVolume * 100f;
                ImGui.Text("Volume:");
                ImGui.SetNextItemWidth(colW);
                if (ImGui.SliderFloat("##vol", ref vol, 0f, 100f, "%.0f%%"))
                {
                    plugin.Config.CordiPeep.SoundVolume = vol / 100f;
                    plugin.Config.Save();
                }
                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + theme.Gap(2f));

                ImGui.BeginGroup();

                var devices = plugin.CordiPeep.GetOutputDevices().Where(d => d.Guid != Guid.Empty).ToList();
                string currentDeviceName = "Primary Driver";
                if (plugin.Config.CordiPeep.SoundDevice != Guid.Empty)
                {
                    var current = devices.FirstOrDefault(d => d.Guid == plugin.Config.CordiPeep.SoundDevice);
                    if (current != null) currentDeviceName = current.Description;
                }

                ImGui.Text("Output Device:");
                ImGui.SetNextItemWidth(colW);
                if (ImGui.BeginCombo("##outDevice", currentDeviceName))
                {
                    if (ImGui.Selectable("Primary Sound Driver", plugin.Config.CordiPeep.SoundDevice == Guid.Empty))
                    {
                        plugin.Config.CordiPeep.SoundDevice = Guid.Empty;
                        plugin.Config.Save();
                    }

                    foreach (var dev in devices)
                    {
                        bool isSelected = dev.Guid == plugin.Config.CordiPeep.SoundDevice;
                        if (ImGui.Selectable(dev.Description, isSelected))
                        {
                            plugin.Config.CordiPeep.SoundDevice = dev.Guid;
                            plugin.Config.Save();
                        }
                    }
                    ImGui.EndCombo();
                }
                theme.HoverHandIfItem();
                ImGui.EndGroup();
            }
        );

        theme.SpacerY(1f);
    }
    private void DrawBlacklistCard()
    {
        bool unused = true;
        theme.DrawPluginCardAuto(
            id: "peep-blacklist-card",
            enabled: ref unused,
            showCheckbox: false,
            title: "Peeper Blacklist",
            drawContent: (avail) =>
            {
                ImGui.TextColored(theme.MutedText, "Characters in this list will not trigger Peep alerts based on the options below.");
                theme.SpacerY(0.5f);


                float worldWidth = 120f * ImGuiHelpers.GlobalScale;
                float nameWidth = avail - worldWidth - 100f - theme.Gap(2f);

                ImGui.SetNextItemWidth(nameWidth);
                ImGui.InputTextWithHint("##newPeepBlName", "Character Name...", ref newBlacklistName, 64);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(worldWidth);
                ImGui.InputTextWithHint("##newPeepBlWorld", "World...", ref newBlacklistWorld, 64);
                ImGui.SameLine();
                if (ImGui.Button("Add##addPeepBl", new Vector2(100f, 0)))
                {
                    if (!string.IsNullOrWhiteSpace(newBlacklistName) && !string.IsNullOrWhiteSpace(newBlacklistWorld))
                    {
                        if (!plugin.Config.CordiPeep.Blacklist.Any(x => x.Name == newBlacklistName && x.World == newBlacklistWorld))
                        {
                            plugin.Config.CordiPeep.Blacklist.Add(new CordiPeepBlacklistEntry { Name = newBlacklistName, World = newBlacklistWorld });
                            plugin.Config.Save();
                            newBlacklistName = string.Empty;
                            newBlacklistWorld = string.Empty;
                        }
                    }
                }
                theme.HoverHandIfItem();

                theme.SpacerY(1f);

                if (plugin.Config.CordiPeep.Blacklist.Count == 0)
                {
                    ImGui.TextColored(theme.MutedText, "Blacklist is empty.");
                }
                else
                {
                    if (ImGui.BeginTable("##peepBlacklistTable", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV))
                    {
                        ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("No Sound", ImGuiTableColumnFlags.WidthFixed, 70f);
                        ImGui.TableSetupColumn("No Discord", ImGuiTableColumnFlags.WidthFixed, 70f);
                        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 60f);
                        ImGui.TableHeadersRow();

                        for (int i = 0; i < plugin.Config.CordiPeep.Blacklist.Count; i++)
                        {
                            var entry = plugin.Config.CordiPeep.Blacklist[i];
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.AlignTextToFramePadding();
                            ImGui.Text($"{entry.Name}@{entry.World}");

                            ImGui.TableNextColumn();
                            bool noSound = entry.DisableSound;
                            if (ImGui.Checkbox($"##peepBlSound{i}", ref noSound))
                            {
                                entry.DisableSound = noSound;
                                plugin.Config.Save();
                            }

                            ImGui.TableNextColumn();
                            bool noDiscord = entry.DisableDiscord;
                            if (ImGui.Checkbox($"##peepBlDiscord{i}", ref noDiscord))
                            {
                                entry.DisableDiscord = noDiscord;
                                plugin.Config.Save();
                            }

                            ImGui.TableNextColumn();
                            if (ImGui.Button($"Remove##remPeepBl{i}", new Vector2(-1, 0)))
                            {
                                plugin.Config.CordiPeep.Blacklist.RemoveAt(i);
                                plugin.Config.Save();
                                i--;
                            }
                            theme.HoverHandIfItem();
                        }
                        ImGui.EndTable();
                    }
                }
            }
        );
    }
}

