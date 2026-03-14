using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Tabs;

public class TrackerTab : ConfigTabBase
{
    private string newBlacklistName = string.Empty;
    private string newBlacklistWorld = string.Empty;
    
    public override string Label => "Tracker";

    public TrackerTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
        
    }
    
    protected override IReadOnlyList<(string Label, Action Draw)> GetSubTabs()
    {
        bool enabled = true;
        plugin.ChannelCache.RefreshIfNeeded();
        
        var tabs = new List<(string Label, Action Draw)>
        {
            ("Peeper", () => DrawPeeperConfig(ref enabled)),
            ("Emote Log", () => DrawEmoteLogConfig(ref enabled)),
            ("Combined Window", () => DrawCombinedWindowConfig(ref enabled))
        };
        
        return tabs;
    }
    
    private void DrawPeeperConfig(ref bool enabled)
    {
        DrawPeeperGeneralCard(ref enabled);
        
        theme.SpacerY();
        DrawPeeperOverlayCard(ref enabled);
        
        theme.SpacerY();
        DrawPeeperDetectionFilterCard(ref enabled);

        theme.SpacerY();
        DrawPeeperAudioCard(ref enabled);
        
        theme.SpacerY();
        DrawPeeperBlacklistCard(ref enabled);
    }

    private void DrawEmoteLogConfig(ref bool enabled)
    {
        DrawEmoteLogGeneralCard(ref enabled);
        
        theme.SpacerY();
        DrawEmoteLogOverlayCard(ref enabled);
        
        theme.SpacerY();
        DrawEmoteLogFilterAndBehaviorCard(ref enabled);
        
        theme.SpacerY();
        DrawEmoteLogBlacklistCard(ref enabled);
    }

    private void DrawCombinedWindowConfig(ref bool enabled)
    {
        DrawCombinedOverlayCard(ref enabled);
    }
    
    #region Shared Blacklist

    private bool blacklistExpanded = false;
    private Dictionary<string, int> blacklistEditStates = new();
    private Dictionary<string, bool> blacklistAddStates = new();

    private static void CenterCursorForItem(float itemWidth)
    {
        float colWidth = ImGui.GetColumnWidth();
        float offset = (colWidth - itemWidth) * 0.5f;
        if (offset > 0) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
    }

    private void DrawBlacklistCard<T>(
        ref bool enabled,
        string id,
        string title,
        string description,
        string idPrefix,
        List<T> list,
        Func<string, string, T> createEntry,
        Action<T, int>? drawExtraColumns = null,
        Action<T, int>? drawExtraEditColumns = null,
        Action? setupExtraColumns = null) where T : IBlacklistEntry
    {
        int extraColumnCount = setupExtraColumns != null ? 1 : 0;
        var headers = new string[3 + extraColumnCount];
        headers[0] = "Character";
        int col = 1;
        if (extraColumnCount > 0) headers[col++] = "No Sound";
        headers[col++] = "No Discord";
        headers[col] = "Action";

        Action setupCols = () =>
        {
            ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch);
            setupExtraColumns?.Invoke();
            ImGui.TableSetupColumn("No Discord", ImGuiTableColumnFlags.WidthFixed, 70f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, theme.ScaledActionsWidth);
        };

        int? removeIdx = null;
        bool isEditing = blacklistEditStates.TryGetValue(id, out int editIdx);
        bool isAdding = blacklistAddStates.ContainsKey(id);

        Action<T, int> drawRow = (entry, idx) =>
        {
            bool rowEditing = isEditing && editIdx == idx;

            if (rowEditing)
            {
                string editName = entry.Name;
                string editWorld = entry.World;

                float worldWidth = 120f * ImGuiHelpers.GlobalScale;
                float gap = ImGui.GetStyle().ItemSpacing.X;
                float nameWidth = ImGui.GetColumnWidth() - worldWidth - gap;

                ImGui.SetNextItemWidth(nameWidth);
                if (ImGui.InputText($"##editName-{idPrefix}-{idx}", ref editName, 64))
                    entry.Name = editName;
                ImGui.SameLine();
                ImGui.SetNextItemWidth(worldWidth);
                if (ImGui.InputText($"##editWorld-{idPrefix}-{idx}", ref editWorld, 64))
                    entry.World = editWorld;
            }
            else
            {
                ImGui.AlignTextToFramePadding();
                ImGui.Text($"{entry.Name}@{entry.World}");
            }

            if (rowEditing)
                drawExtraEditColumns?.Invoke(entry, idx);
            else
                drawExtraColumns?.Invoke(entry, idx);

            ImGui.TableNextColumn();
            {
                bool noDiscord = entry.DisableDiscord;
                float cbWidth = ImGui.GetFrameHeight();
                CenterCursorForItem(cbWidth);
                theme.ConfigCheckbox($"##{idPrefix}Discord{idx}", ref noDiscord, () =>
                {
                    entry.DisableDiscord = noDiscord;
                    plugin.Config.Save();
                });
            }

            ImGui.TableNextColumn();
            {
                float btnSize = ImGui.GetFrameHeight();
                float totalWidth = btnSize * 2 + ImGui.GetStyle().ItemSpacing.X;

                if (rowEditing)
                {
                    if (theme.SuccessIconButton($"##save-{idPrefix}-{idx}", FontAwesomeIcon.Check, "Save"))
                    {
                        blacklistEditStates.Remove(id);
                        plugin.Config.Save();
                    }
                    ImGui.SameLine();
                    if (theme.SecondaryIconButton($"##cancel-{idPrefix}-{idx}", FontAwesomeIcon.Times, "Cancel"))
                    {
                        blacklistEditStates.Remove(id);
                    }
                }
                else
                {
                    if (theme.SecondaryIconButton($"##edit-{idPrefix}-{idx}", FontAwesomeIcon.Pen, "Edit"))
                    {
                        blacklistEditStates[id] = idx;
                        blacklistAddStates.Remove(id);
                    }
                    ImGui.SameLine();
                    if (theme.DangerIconButton($"##del-{idPrefix}-{idx}", FontAwesomeIcon.Trash, "Remove"))
                    {
                        removeIdx = idx;
                    }
                }
            }
        };

        theme.DrawCollapsableCardWithTable(
            id: id,
            title: title,
            expanded: ref blacklistExpanded,
            collection: list,
            drawRow: drawRow,
            headers: headers,
            setupColumns: setupCols,
            showHeaders: true,
            showCount: true,
            collapsible: false,
            mutedText: description,
            drawFooter: (width) =>
            {
                if (isAdding)
                {
                    float scale = ImGuiHelpers.GlobalScale;
                    float actionsWidth = theme.ScaledActionsWidth;
                    float worldWidth = 120f * scale;
                    float inputsWidth = width - actionsWidth - worldWidth - theme.Gap(2f);

                    ImGui.SetNextItemWidth(inputsWidth);
                    ImGui.InputTextWithHint($"##new{idPrefix}Name", "Character Name...", ref newBlacklistName, 64);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(worldWidth);
                    ImGui.InputTextWithHint($"##new{idPrefix}World", "World...", ref newBlacklistWorld, 64);
                    ImGui.SameLine();
                    if (theme.SuccessIconButton($"##addSave-{idPrefix}", FontAwesomeIcon.Check, "Add"))
                    {
                        if (!string.IsNullOrWhiteSpace(newBlacklistName) && !string.IsNullOrWhiteSpace(newBlacklistWorld))
                        {
                            if (!list.Any(x => x.Name == newBlacklistName && x.World == newBlacklistWorld))
                            {
                                list.Add(createEntry(newBlacklistName, newBlacklistWorld));
                                plugin.Config.Save();
                                newBlacklistName = string.Empty;
                                newBlacklistWorld = string.Empty;
                                blacklistAddStates.Remove(id);
                            }
                        }
                    }
                    ImGui.SameLine();
                    if (theme.SecondaryIconButton($"##addCancel-{idPrefix}", FontAwesomeIcon.Times, "Cancel"))
                    {
                        blacklistAddStates.Remove(id);
                        newBlacklistName = string.Empty;
                        newBlacklistWorld = string.Empty;
                    }
                    theme.SpacerY(1f);
                }

                float btnWidth = width * 0.95f;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (width - btnWidth) * 0.5f);
                if (theme.SecondaryButton("Add New Entry", new Vector2(btnWidth, 0)))
                {
                    blacklistAddStates[id] = true;
                    blacklistEditStates.Remove(id);
                    newBlacklistName = string.Empty;
                    newBlacklistWorld = string.Empty;
                }
            }
        );

        if (removeIdx.HasValue)
        {
            list.RemoveAt(removeIdx.Value);
            plugin.Config.Save();
            if (isEditing && editIdx == removeIdx.Value)
                blacklistEditStates.Remove(id);
        }
    }

    #endregion

    #region Peeper
    private void DrawPeeperGeneralCard(ref bool enabled)
    {
        theme.DrawPluginCardAuto(
            id: "cordi-peep-general",
            title: "General",
            enabled: ref enabled,
            drawContent: (avail) =>
            {
                bool pEnabled = plugin.Config.CordiPeep.Enabled;
                if (theme.ConfigCheckbox("Enable Peeper Detection", ref pEnabled, () =>
                {
                    plugin.Config.CordiPeep.Enabled = pEnabled;
                    plugin.Config.Save();
                })) { }
                theme.HoverHandIfItem();

                bool detectClosed = plugin.Config.CordiPeep.DetectWhenClosed;
                if (theme.ConfigCheckbox("Detect when Window is Closed", ref detectClosed, () =>
                {
                    plugin.Config.CordiPeep.DetectWhenClosed = detectClosed;
                    plugin.Config.Save();
                })) { }
                theme.HoverHandIfItem();

                bool dEnabled = plugin.Config.CordiPeep.DiscordEnabled;
                if (theme.ConfigCheckbox("Enable Discord Notifications", ref dEnabled, () =>
                {
                    plugin.Config.CordiPeep.DiscordEnabled = dEnabled;
                    plugin.Config.Save();
                })) { }
                theme.HoverHandIfItem();

                bool dDisableCombat = plugin.Config.CordiPeep.DisableDiscordInCombat;
                if (theme.ConfigCheckbox("Disable Notifications in Combat##btn-disable-discord-in-combat", ref dDisableCombat, () =>
                {
                    plugin.Config.CordiPeep.DisableDiscordInCombat = dDisableCombat;
                    plugin.Config.Save();
                })) { }
                theme.HoverHandIfItem();

                theme.SpacerY(0.5f);


                theme.ChannelPicker(
                    "peepChannel",
                    plugin.Config.CordiPeep.DiscordChannelId,
                    plugin.ChannelCache.TextChannels,
                    (newId) =>
                    {
                        plugin.Config.CordiPeep.DiscordChannelId = newId;
                        plugin.Config.Save();
                    },
                    defaultLabel: "None"
                );
            }
        );
    }

    private void DrawPeeperOverlayCard(ref bool enabled)
    {
        theme.DrawPluginCardAuto(
            id: "cordi-peep-window",
            title: "In-Game Overlay",
            enabled: ref enabled,
            drawContent: (avail) =>
            {


                float half = avail / 2f - theme.Gap(1f);

                using (var g1 = ImRaii.Group())
                {
                    bool wEnabled = plugin.Config.CordiPeep.WindowEnabled;
                    theme.ConfigCheckbox("Enable Window", ref wEnabled, () =>
                    {
                        plugin.Config.CordiPeep.WindowEnabled = wEnabled;
                        plugin.Config.Save();
                        plugin.UpdateCommandVisibility();
                    });
                    theme.HoverHandIfItem();

                    bool openOnLogin = plugin.Config.CordiPeep.OpenOnLogin;
                    theme.ConfigCheckbox("Open on Login", ref openOnLogin, () =>
                    {
                        plugin.Config.CordiPeep.OpenOnLogin = openOnLogin;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();

                    bool wLocked = plugin.Config.CordiPeep.WindowLocked;
                    theme.ConfigCheckbox("Lock Position", ref wLocked, () =>
                    {
                        plugin.Config.CordiPeep.WindowLocked = wLocked;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();
                }

                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + theme.Gap(2f));

                using (var g2 = ImRaii.Group())
                {
                    bool wNoResize = plugin.Config.CordiPeep.WindowNoResize;
                    theme.ConfigCheckbox("Lock Size", ref wNoResize, () =>
                    {
                        plugin.Config.CordiPeep.WindowNoResize = wNoResize;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();

                    bool ignoreEsc = plugin.Config.CordiPeep.IgnoreEsc;
                    theme.ConfigCheckbox("Ignore ESC", ref ignoreEsc, () =>
                    {
                        plugin.Config.CordiPeep.IgnoreEsc = ignoreEsc;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();

                    bool wFocusHover = plugin.Config.CordiPeep.FocusOnHover;
                    theme.ConfigCheckbox("Focus on hover", ref wFocusHover, () =>
                    {
                        plugin.Config.CordiPeep.FocusOnHover = wFocusHover;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();

                    bool wAltClick = plugin.Config.CordiPeep.AltClickExamine;
                    theme.ConfigCheckbox("Alt-click Examine", ref wAltClick, () =>
                    {
                        plugin.Config.CordiPeep.AltClickExamine = wAltClick;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();

                    bool hideTitleBar = plugin.Config.CordiPeep.HideTitleBar;
                    theme.ConfigCheckbox("Hide Title Bar", ref hideTitleBar, () =>
                    {
                        plugin.Config.CordiPeep.HideTitleBar = hideTitleBar;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();
                }

                theme.SpacerY(0.5f);
                ImGui.Text("Background Opacity:");
                ImGui.SetNextItemWidth(avail);
                float peepOpacity = plugin.Config.CordiPeep.BackgroundOpacity * 100f;
                if (ImGui.SliderFloat("##peepBgOpacity", ref peepOpacity, 0f, 100f, "%.0f%%"))
                {
                    plugin.Config.CordiPeep.BackgroundOpacity = peepOpacity / 100f;
                    plugin.Config.Save();
                }

                bool peepTextShadow = plugin.Config.CordiPeep.TextShadow;
                theme.ConfigCheckbox("Text Shadow", ref peepTextShadow, () =>
                {
                    plugin.Config.CordiPeep.TextShadow = peepTextShadow;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                theme.SpacerY(1f);
                ImGui.Separator();
                theme.SpacerY(0.5f);
                ImGui.TextColored(theme.MutedText, "Overlay Display");
                theme.SpacerY(0.5f);

                using (var g3 = ImRaii.Group())
                {
                    bool showDir = plugin.Config.CordiPeep.ShowDirection;
                    theme.ConfigCheckbox("Show Direction Arrow", ref showDir, () =>
                    {
                        plugin.Config.CordiPeep.ShowDirection = showDir;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();

                    bool showDist = plugin.Config.CordiPeep.ShowDistance;
                    theme.ConfigCheckbox("Show Distance", ref showDist, () =>
                    {
                        plugin.Config.CordiPeep.ShowDistance = showDist;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();
                }

                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + theme.Gap(2f));

                using (var g4 = ImRaii.Group())
                {
                    bool showTarget = plugin.Config.CordiPeep.ShowCurrentTarget;
                    theme.ConfigCheckbox("Show Peeper's Target", ref showTarget, () =>
                    {
                        plugin.Config.CordiPeep.ShowCurrentTarget = showTarget;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();

                    bool showDirHistory = plugin.Config.CordiPeep.ShowDirectionInHistory;
                    theme.ConfigCheckbox("Show Direction in History", ref showDirHistory, () =>
                    {
                        plugin.Config.CordiPeep.ShowDirectionInHistory = showDirHistory;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();

                    bool showDistHistory = plugin.Config.CordiPeep.ShowDistanceInHistory;
                    theme.ConfigCheckbox("Show Distance in History", ref showDistHistory, () =>
                    {
                        plugin.Config.CordiPeep.ShowDistanceInHistory = showDistHistory;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();
                }

                theme.SpacerY(1f);
                ImGui.Separator();
                theme.SpacerY(0.5f);
                ImGui.TextColored(theme.MutedText, "Colors");
                theme.SpacerY(0.5f);

                var highlightColor = plugin.Config.CordiPeep.TargetingHighlightColor;
                if (ImGui.ColorEdit4("Targeting Highlight##peepHighlight", ref highlightColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
                {
                    plugin.Config.CordiPeep.TargetingHighlightColor = highlightColor;
                    plugin.Config.Save();
                }

                theme.SpacerY(1f);
                ImGui.Separator();
                theme.SpacerY(0.5f);
                ImGui.TextColored(theme.MutedText, "Targeting Dot");
                theme.SpacerY(0.5f);

                bool showDot = plugin.Config.CordiPeep.ShowTargetingDot;
                theme.ConfigCheckbox("Show Dot on Targeting Players", ref showDot, () =>
                {
                    plugin.Config.CordiPeep.ShowTargetingDot = showDot;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                var dotColor = plugin.Config.CordiPeep.TargetingDotColor;
                if (ImGui.ColorEdit4("Dot Color##peepDotColor", ref dotColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
                {
                    plugin.Config.CordiPeep.TargetingDotColor = dotColor;
                    plugin.Config.Save();
                }

                ImGui.Text("Dot Size:");
                ImGui.SetNextItemWidth(avail);
                float dotSize = plugin.Config.CordiPeep.TargetingDotSize;
                if (ImGui.SliderFloat("##peepDotSize", ref dotSize, 2f, 20f, "%.1f px"))
                {
                    plugin.Config.CordiPeep.TargetingDotSize = dotSize;
                    plugin.Config.Save();
                }

                ImGui.Text("Dot Y Offset:");
                ImGui.SetNextItemWidth(avail);
                float dotYOffset = plugin.Config.CordiPeep.TargetingDotYOffset;
                if (ImGui.SliderFloat("##peepDotYOffset", ref dotYOffset, -1f, 5f, "%.1f"))
                {
                    plugin.Config.CordiPeep.TargetingDotYOffset = dotYOffset;
                    plugin.Config.Save();
                }

                theme.SpacerY(0.7f);

                if (theme.SecondaryButton("Open Window Now", new Vector2(avail, 28)))
                {
                    if (plugin.CordiPeepWindow != null)
                        plugin.CordiPeepWindow.IsOpen = true;
                }
                theme.HoverHandIfItem();
            }
        );
    }

    private void DrawPeeperDetectionFilterCard(ref bool enabled)
    {
        theme.DrawPluginCardAuto(
            id: "cordi-peep-filters",
            title: "Detection Filters",
            enabled: ref enabled,
            drawContent: (avail) =>
            {

                using (var g5 = ImRaii.Group())
                {
                    bool logParty = plugin.Config.CordiPeep.LogParty;
                    theme.ConfigCheckbox("Log party members", ref logParty, () =>
                    {
                        plugin.Config.CordiPeep.LogParty = logParty;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();

                    bool logAlliance = plugin.Config.CordiPeep.LogAlliance;
                    theme.ConfigCheckbox("Log alliance members", ref logAlliance, () =>
                    {
                        plugin.Config.CordiPeep.LogAlliance = logAlliance;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();
                }

                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + theme.Gap(2f));

                using (var g6 = ImRaii.Group())
                {
                    bool logCombat = plugin.Config.CordiPeep.LogCombat;
                    theme.ConfigCheckbox("Combat targeters only", ref logCombat, () =>
                    {
                        plugin.Config.CordiPeep.LogCombat = logCombat;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();

                    bool wIncludeSelf = plugin.Config.CordiPeep.IncludeSelf;
                    theme.ConfigCheckbox("Include yourself", ref wIncludeSelf, () =>
                    {
                        plugin.Config.CordiPeep.IncludeSelf = wIncludeSelf;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();
                }
            }
        );
    }

    private void DrawPeeperAudioCard(ref bool enabled)
    {
        theme.DrawPluginCardAuto(
            id: "cordi-peep-audio",
            title: "Audio Notifications",
            enabled: ref enabled,
            drawContent: (avail) =>
            {
                bool sEnabled = plugin.Config.CordiPeep.SoundEnabled;
                theme.ConfigCheckbox("Enable Sound Alert", ref sEnabled, () =>
                {
                    plugin.Config.CordiPeep.SoundEnabled = sEnabled;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                bool sDisableCombat = plugin.Config.CordiPeep.DisableSoundInCombat;
                if (theme.ConfigCheckbox("Disable in Combat##btn-disable-sound-in-combat", ref sDisableCombat, () =>
                {
                    plugin.Config.CordiPeep.DisableSoundInCombat = sDisableCombat;
                    plugin.Config.Save();
                })) { }
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

                using (var g7 = ImRaii.Group())
                {
                    float vol = plugin.Config.CordiPeep.SoundVolume * 100f;
                    ImGui.Text("Volume:");
                    ImGui.SetNextItemWidth(colW);
                    if (ImGui.SliderFloat("##vol", ref vol, 0f, 100f, "%.0f%%"))
                    {
                        plugin.Config.CordiPeep.SoundVolume = vol / 100f;
                        plugin.Config.Save();
                    }
                }

                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + theme.Gap(2f));

                using (var g8 = ImRaii.Group())
                {
                    var devices = plugin.CordiPeep.GetOutputDevices().Where(d => d.Guid != Guid.Empty).ToList();
                    string currentDeviceName = "Primary Driver";
                    if (plugin.Config.CordiPeep.SoundDevice != Guid.Empty)
                    {
                        var current = devices.FirstOrDefault(d => d.Guid == plugin.Config.CordiPeep.SoundDevice);
                        if (current != null) currentDeviceName = current.Description;
                    }

                    ImGui.Text("Output Device:");
                    ImGui.SetNextItemWidth(colW);
                    using (var combo = ImRaii.Combo("##outDevice", currentDeviceName))
                    {
                        if (combo)
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
                        }
                    }
                    theme.HoverHandIfItem();
                }
            }
        );
    }

    private void DrawPeeperBlacklistCard(ref bool enabled)
    {
        Action<CordiPeepBlacklistEntry, int> drawSoundColumn = (entry, i) =>
        {
            ImGui.TableNextColumn();
            bool noSound = entry.DisableSound;
            float cbWidth = ImGui.GetFrameHeight();
            CenterCursorForItem(cbWidth);
            theme.ConfigCheckbox($"##peepBlSound{i}", ref noSound, () =>
            {
                entry.DisableSound = noSound;
                plugin.Config.Save();
            });
        };

        DrawBlacklistCard<CordiPeepBlacklistEntry>(
            ref enabled,
            id: "peep-blacklist-card",
            title: "Peeper Blacklist",
            description: "Characters in this list will not trigger Peep alerts based on the options below.",
            idPrefix: "peepBl",
            list: plugin.Config.CordiPeep.Blacklist,
            createEntry: (name, world) => new CordiPeepBlacklistEntry { Name = name, World = world },
            drawExtraColumns: drawSoundColumn,
            drawExtraEditColumns: drawSoundColumn,
            setupExtraColumns: () =>
            {
                ImGui.TableSetupColumn("No Sound", ImGuiTableColumnFlags.WidthFixed, 70f * ImGuiHelpers.GlobalScale);
            }
        );
    }

    #endregion

    #region Emote Log

    private void DrawEmoteLogGeneralCard(ref bool enabled)
    {
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
    }

    private void DrawEmoteLogOverlayCard(ref bool enabled)
    {
        theme.DrawPluginCardAuto(
            id: "emote-log-window",
            title: "In-Game Overlay",
            enabled: ref enabled,
            drawContent: (avail) =>
            {
                float half = avail / 2f - theme.Gap(1f);

                using (var g1 = ImRaii.Group())
                {
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
                }

                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + theme.Gap(2f));

                using (var g2 = ImRaii.Group())
                {
                    var lockSize = plugin.Config.EmoteLog.WindowLockSize;
                    theme.ConfigCheckbox("Lock Size", ref lockSize, () =>
                    {
                        plugin.Config.EmoteLog.WindowLockSize = lockSize;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();

                    var ignoreEsc = plugin.Config.EmoteLog.IgnoreEsc;
                    theme.ConfigCheckbox("Ignore ESC", ref ignoreEsc, () =>
                    {
                        plugin.Config.EmoteLog.IgnoreEsc = ignoreEsc;
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

                    bool hideEmoteTitleBar = plugin.Config.EmoteLog.HideTitleBar;
                    theme.ConfigCheckbox("Hide Title Bar", ref hideEmoteTitleBar, () =>
                    {
                        plugin.Config.EmoteLog.HideTitleBar = hideEmoteTitleBar;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();
                }

                theme.SpacerY(0.5f);
                ImGui.Text("Background Opacity:");
                ImGui.SetNextItemWidth(avail);
                float emoteOpacity = plugin.Config.EmoteLog.BackgroundOpacity * 100f;
                if (ImGui.SliderFloat("##emoteBgOpacity", ref emoteOpacity, 0f, 100f, "%.0f%%"))
                {
                    plugin.Config.EmoteLog.BackgroundOpacity = emoteOpacity / 100f;
                    plugin.Config.Save();
                }

                bool emoteTextShadow = plugin.Config.EmoteLog.TextShadow;
                theme.ConfigCheckbox("Text Shadow##emoteTextShadow", ref emoteTextShadow, () =>
                {
                    plugin.Config.EmoteLog.TextShadow = emoteTextShadow;
                    plugin.Config.Save();
                });
                theme.HoverHandIfItem();

                theme.SpacerY(0.7f);

                if (theme.SecondaryButton("Open Window Now", new Vector2(avail, 28)))
                {
                    if (plugin.EmoteLogWindow != null)
                        plugin.EmoteLogWindow.IsOpen = true;
                }
            }
        );
    }

    private void DrawEmoteLogFilterAndBehaviorCard(ref bool enabled)
    {
        theme.DrawPluginCardAuto(
            id: "emote-log-filters",
            title: "Filters & Behavior",
            enabled: ref enabled,
            drawContent: (avail) =>
            {
                using (var g3 = ImRaii.Group())
                {
                    var includeSelf = plugin.Config.EmoteLog.IncludeSelf;
                    theme.ConfigCheckbox("Include Self", ref includeSelf, () =>
                    {
                        plugin.Config.EmoteLog.IncludeSelf = includeSelf;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();
                }

                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + theme.Gap(2f));

                using (var g4 = ImRaii.Group())
                {
                    var collapse = plugin.Config.EmoteLog.CollapseDuplicates;
                    theme.ConfigCheckbox("Collapse Duplicates", ref collapse, () =>
                    {
                        plugin.Config.EmoteLog.CollapseDuplicates = collapse;
                        plugin.Config.Save();
                    });
                    theme.HoverHandIfItem();
                }
            }
        );
    }

    private void DrawEmoteLogBlacklistCard(ref bool enabled)
    {
        DrawBlacklistCard<EmoteLogBlacklistEntry>(
            ref enabled,
            id: "emote-blacklist-card",
            title: "Emote Log Blacklist",
            description: "Characters in this list will not trigger Emote Discord alerts.",
            idPrefix: "emoteBl",
            list: plugin.Config.EmoteLog.Blacklist,
            createEntry: (name, world) => new EmoteLogBlacklistEntry { Name = name, World = world }
        );
    }

    #endregion
    
    #region Combined Window

    private void DrawCombinedGeneralCard(ref bool enabled)
    {
        theme.DrawPluginCardAuto(
            id: "cordi-combinedwindow-general",
            title: "General",
            enabled: ref enabled,
            drawContent: (avail) =>
            {
                bool cEnabled = plugin.Config.CombinedWindow.Enabled;
                if (theme.ConfigCheckbox("Enable CombinedWindow Detection", ref cEnabled, () =>
                {
                    plugin.Config.CombinedWindow.Enabled = cEnabled;
                    plugin.Config.Save();
                })) { }
            }
        );
    }

    private void DrawCombinedOverlayCard(ref bool enabled)
    {
        theme.DrawPluginCardAuto(
            id: "combined-window",
            enabled: ref enabled,
            showCheckbox: false,
            title: "In-Game Overlay",
            drawContent: (avail) =>
            {
                var swap = plugin.Config.CombinedWindow.SwapPanels;
                if (ImGui.Checkbox("Swap Panels (Peeper links, Emote Log rechts)##comboSwap", ref swap))
                {
                    plugin.Config.CombinedWindow.SwapPanels = swap;
                    plugin.Config.Save();
                }

                var openOnLogin = plugin.Config.CombinedWindow.OpenOnLogin;
                if (ImGui.Checkbox("Open on Login##comboLogin", ref openOnLogin))
                {
                    plugin.Config.CombinedWindow.OpenOnLogin = openOnLogin;
                    plugin.Config.Save();
                }

                var locked = plugin.Config.CombinedWindow.WindowLocked;
                if (ImGui.Checkbox("Lock Position##comboLock", ref locked))
                {
                    plugin.Config.CombinedWindow.WindowLocked = locked;
                    plugin.Config.Save();
                }

                var noResize = plugin.Config.CombinedWindow.WindowNoResize;
                if (ImGui.Checkbox("Lock Size##comboResize", ref noResize))
                {
                    plugin.Config.CombinedWindow.WindowNoResize = noResize;
                    plugin.Config.Save();
                }

                var ignoreEsc = plugin.Config.CombinedWindow.IgnoreEsc;
                if (ImGui.Checkbox("Ignore ESC##comboEsc", ref ignoreEsc))
                {
                    plugin.Config.CombinedWindow.IgnoreEsc = ignoreEsc;
                    plugin.Config.Save();
                }

                var comboHideTitle = plugin.Config.CombinedWindow.HideTitleBar;
                if (ImGui.Checkbox("Hide Title Bar##comboTitle", ref comboHideTitle))
                {
                    plugin.Config.CombinedWindow.HideTitleBar = comboHideTitle;
                    plugin.Config.Save();
                }

                theme.SpacerY(0.5f);
                ImGui.Text("Background Opacity:");
                ImGui.SetNextItemWidth(avail);
                float comboOpacity = plugin.Config.CombinedWindow.BackgroundOpacity * 100f;
                if (ImGui.SliderFloat("##comboBgOpacity", ref comboOpacity, 0f, 100f, "%.0f%%"))
                {
                    plugin.Config.CombinedWindow.BackgroundOpacity = comboOpacity / 100f;
                    plugin.Config.Save();
                }

                var comboTextShadow = plugin.Config.CombinedWindow.TextShadow;
                if (ImGui.Checkbox("Text Shadow##comboShadow", ref comboTextShadow))
                {
                    plugin.Config.CombinedWindow.TextShadow = comboTextShadow;
                    plugin.Config.Save();
                }

                theme.SpacerY(0.7f);
                if (theme.SecondaryButton("Open Window Now", new Vector2(avail, 28)))
                {
                    plugin.CombinedWindow.IsOpen = true;
                }
            });
    }
    
    #endregion
}
