using Cordi.Services;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using System.Numerics;
using System;
using System.Linq;
using System.Collections.Generic;
using DSharpPlus;
using DSharpPlus.Entities;
using ECommons.ImGuiMethods;

using Cordi.Core;
using Cordi.UI.Themes;
using Cordi.Configuration;

namespace Cordi.UI.Tabs;

public class EmoteLogTab
{
    private readonly CordiPlugin plugin;
    private readonly UiTheme theme;



    private string newBlacklistName = string.Empty;
    private string newBlacklistWorld = string.Empty;

    public EmoteLogTab(CordiPlugin plugin, UiTheme theme)
    {
        this.plugin = plugin;
        this.theme = theme;
    }

    public void Draw()
    {
        theme.SpacerY(2f);


        plugin.ChannelCache.RefreshIfNeeded();

        bool enabled = true;


        theme.DrawPluginCardAuto(
            id: "emote-log-general",
            title: "General",
            enabled: ref enabled,
            drawContent: (avail) =>
            {
                var logEnabled = plugin.Config.EmoteLog.Enabled;
                theme.ConfigCheckbox("Enable Emote Detection", ref logEnabled, () =>
                {
                    plugin.Config.EmoteLog.Enabled = logEnabled;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                var detectClosed = plugin.Config.EmoteLog.DetectWhenClosed;
                theme.ConfigCheckbox("Detect when Window is Closed", ref detectClosed, () =>
                {
                    plugin.Config.EmoteLog.DetectWhenClosed = detectClosed;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                var discordEnabled = plugin.Config.EmoteLog.DiscordEnabled;
                theme.ConfigCheckbox("Enable Discord Notifications", ref discordEnabled, () =>
                {
                    plugin.Config.EmoteLog.DiscordEnabled = discordEnabled;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                theme.SpacerY(0.5f);


                ImGui.Text("Discord Notification Channel:");
                string currentId = plugin.Config.EmoteLog.ChannelId;
                string preview = "None";

                if (!string.IsNullOrEmpty(currentId) && plugin.ChannelCache.TextChannels != null)
                {
                    var ch = plugin.ChannelCache.TextChannels.FirstOrDefault(c => c.Id.ToString() == currentId);
                    if (ch != null) preview = $"#{ch.Name}";
                    else preview = currentId;
                }

                theme.ChannelPicker(
                    "emoteLogChannel",
                    plugin.Config.EmoteLog.ChannelId,
                    plugin.ChannelCache.TextChannels,
                    (newId) =>
                    {
                        plugin.Config.EmoteLog.ChannelId = newId;
                        plugin.Config.Save();
                    },
                    defaultLabel: "None"
                );
                theme.HoverHandIfItem();
            }
        );

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);


        theme.DrawPluginCardAuto(
            id: "emote-log-window",
            title: "In-Game Overlay",
            enabled: ref enabled,
            drawContent: (avail) =>
            {
                float half = avail / 2f - theme.Gap(1f);

                ImGui.BeginGroup();
                bool windowOpen = plugin.EmoteLogWindow.IsOpen;
                theme.ConfigCheckbox("Enable Window", ref windowOpen, () =>
                {
                    plugin.EmoteLogWindow.IsOpen = windowOpen;
                    plugin.Config.EmoteLog.WindowEnabled = windowOpen;
                    plugin.Config.Save();
                    plugin.UpdateCommandVisibility();
                });
                theme.HoverHandIfItem();

                var openOnLogin = plugin.Config.EmoteLog.WindowOpenOnLogin;
                theme.ConfigCheckbox("Open on Login", ref openOnLogin, () =>
                {
                    plugin.Config.EmoteLog.WindowOpenOnLogin = openOnLogin;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                var lockPos = plugin.Config.EmoteLog.WindowLockPosition;
                theme.ConfigCheckbox("Lock Position", ref lockPos, () =>
                {
                    plugin.Config.EmoteLog.WindowLockPosition = lockPos;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();
                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + theme.Gap(2f));

                ImGui.BeginGroup();
                var lockSize = plugin.Config.EmoteLog.WindowLockSize;
                theme.ConfigCheckbox("Lock Size", ref lockSize, () =>
                {
                    plugin.Config.EmoteLog.WindowLockSize = lockSize;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                var showReply = plugin.Config.EmoteLog.ShowReplyButton;
                theme.ConfigCheckbox("Show Reply Button", ref showReply, () =>
                {
                    plugin.Config.EmoteLog.ShowReplyButton = showReply;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();
                ImGui.EndGroup();

                theme.SpacerY(0.7f);

                if (theme.SecondaryButton("Open Window Now", new Vector2(avail, 28)))
                {
                    if (plugin.EmoteLogWindow != null)
                        plugin.EmoteLogWindow.IsOpen = true;
                }
            }
        );

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);


        theme.DrawPluginCardAuto(
            id: "emote-log-filters",
            title: "Filters & Behavior",
            enabled: ref enabled,
            drawContent: (avail) =>
            {
                ImGui.BeginGroup();
                var includeSelf = plugin.Config.EmoteLog.IncludeSelf;
                theme.ConfigCheckbox("Include Self", ref includeSelf, () =>
                {
                    plugin.Config.EmoteLog.IncludeSelf = includeSelf;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();
                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + theme.Gap(2f));

                ImGui.BeginGroup();
                var collapse = plugin.Config.EmoteLog.CollapseDuplicates;
                theme.ConfigCheckbox("Collapse Duplicates", ref collapse, () =>
                {
                    plugin.Config.EmoteLog.CollapseDuplicates = collapse;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();
                ImGui.EndGroup();
            }
        );

        theme.SpacerY(2f);
        ImGui.Separator();
        theme.SpacerY(2f);
        DrawBlacklistCard();
    }

    private void DrawBlacklistCard()
    {
        bool unused = true;
        theme.DrawPluginCardAuto(
            id: "emote-blacklist-card",
            enabled: ref unused,
            showCheckbox: false,
            title: "Emote Log Blacklist",
            drawContent: (avail) =>
            {
                ImGui.TextColored(theme.MutedText, "Characters in this list will not trigger Emote Discord alerts.");
                theme.SpacerY(0.5f);


                float worldWidth = 120f * ImGuiHelpers.GlobalScale;
                float nameWidth = avail - worldWidth - 100f - theme.Gap(2f);

                ImGui.SetNextItemWidth(nameWidth);
                ImGui.InputTextWithHint("##newEmoteBlName", "Character Name...", ref newBlacklistName, 64);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(worldWidth);
                ImGui.InputTextWithHint("##newEmoteBlWorld", "World...", ref newBlacklistWorld, 64);
                ImGui.SameLine();
                if (ImGui.Button("Add##addEmoteBl", new Vector2(100f, 0)))
                {
                    if (!string.IsNullOrWhiteSpace(newBlacklistName) && !string.IsNullOrWhiteSpace(newBlacklistWorld))
                    {
                        if (!plugin.Config.EmoteLog.Blacklist.Any(x => x.Name == newBlacklistName && x.World == newBlacklistWorld))
                        {
                            plugin.Config.EmoteLog.Blacklist.Add(new EmoteLogBlacklistEntry { Name = newBlacklistName, World = newBlacklistWorld });
                            plugin.Config.Save();
                            newBlacklistName = string.Empty;
                            newBlacklistWorld = string.Empty;
                        }
                    }
                }
                theme.HoverHandIfItem();

                theme.SpacerY(1f);

                if (plugin.Config.EmoteLog.Blacklist.Count == 0)
                {
                    ImGui.TextColored(theme.MutedText, "Blacklist is empty.");
                }
                else
                {
                    if (ImGui.BeginTable("##emoteBlacklistTable", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV))
                    {
                        ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("No Discord", ImGuiTableColumnFlags.WidthFixed, 80f);
                        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 60f);
                        ImGui.TableHeadersRow();

                        for (int i = 0; i < plugin.Config.EmoteLog.Blacklist.Count; i++)
                        {
                            var entry = plugin.Config.EmoteLog.Blacklist[i];
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.AlignTextToFramePadding();
                            ImGui.Text($"{entry.Name}@{entry.World}");

                            ImGui.TableNextColumn();
                            bool noDiscord = entry.DisableDiscord;
                            theme.ConfigCheckbox($"##emoteBlDiscord{i}", ref noDiscord, () =>
                            {
                                entry.DisableDiscord = noDiscord;
                                plugin.Config.Save();
                            });

                            ImGui.TableNextColumn();
                            if (ImGui.Button($"Remove##remEmoteBl{i}", new Vector2(-1, 0)))
                            {
                                plugin.Config.EmoteLog.Blacklist.RemoveAt(i);
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
