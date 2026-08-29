using System.Collections.Generic;
using System.Linq;
using Cordi.Configuration;
using Cordi.Services.Chatbox;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using DiscordChannel = Crovus.Models.DiscordChannel;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab
{
    private static readonly XivChatType[] SelectableChatTypes =
    {
        XivChatType.Say,
        XivChatType.Shout,
        XivChatType.Yell,
        XivChatType.TellIncoming,
        XivChatType.Party,
        XivChatType.CrossParty,
        XivChatType.Alliance,
        XivChatType.FreeCompany,
        XivChatType.NoviceNetwork,
        XivChatType.PvPTeam,
        XivChatType.Ls1,
        XivChatType.Ls2,
        XivChatType.Ls3,
        XivChatType.Ls4,
        XivChatType.Ls5,
        XivChatType.Ls6,
        XivChatType.Ls7,
        XivChatType.Ls8,
        XivChatType.CrossLinkShell1,
        XivChatType.CrossLinkShell2,
        XivChatType.CrossLinkShell3,
        XivChatType.CrossLinkShell4,
        XivChatType.CrossLinkShell5,
        XivChatType.CrossLinkShell6,
        XivChatType.CrossLinkShell7,
        XivChatType.CrossLinkShell8,
        XivChatType.Echo,
        XivChatType.SystemMessage,
    };

    private static bool IsTellType(XivChatType type) =>
        type is XivChatType.TellIncoming or XivChatType.TellOutgoing;

    private string? pendingRemoveId;
    private string? pendingMoveId;
    private int pendingMoveDelta;

    private void DrawChannels()
    {
        var textChannels = plugin.Channels.TextChannels;

        Card("chatbox-channels", "Channels", _ =>
        {
            ImGui.TextDisabled("A channel bundles game chat types and an optional Discord channel.");

            if (theme.PrimaryButton("Add Channel##chatbox"))
            {
                Cfg.Channels.Add(new ChatboxChannelConfig
                {
                    Name = "New Channel",
                    Order = Cfg.Channels.Count,
                });
                Save();
                plugin.Chatbox.RebuildChannels();
            }

            ImGui.SameLine();
            if (theme.Button("Sort by Order##chatbox"))
            {
                Cfg.Channels.Sort((a, b) => a.Order.CompareTo(b.Order));
                Save();
                plugin.Chatbox.RebuildChannels();
            }
        });

        for (var i = 0; i < Cfg.Channels.Count; i++)
            DrawChannelCard(Cfg.Channels[i], i, textChannels);

        ApplyPendingChannelChanges();
    }

    private void ApplyPendingChannelChanges()
    {
        if (pendingRemoveId != null)
        {
            Cfg.Channels.RemoveAll(c => c.Id == pendingRemoveId);
            pendingRemoveId = null;
            Save();
            plugin.Chatbox.RebuildChannels();
        }

        if (pendingMoveId == null) return;

        var index = Cfg.Channels.FindIndex(c => c.Id == pendingMoveId);
        var target = index + pendingMoveDelta;
        pendingMoveId = null;

        if (index < 0 || target < 0 || target >= Cfg.Channels.Count) return;

        (Cfg.Channels[index], Cfg.Channels[target]) = (Cfg.Channels[target], Cfg.Channels[index]);
        for (var i = 0; i < Cfg.Channels.Count; i++)
            Cfg.Channels[i].Order = i;

        Save();
        plugin.Chatbox.RebuildChannels();
    }

    private void DrawChannelCard(ChatboxChannelConfig channel, int index, IReadOnlyList<DiscordChannel>? textChannels)
    {
        var title = string.IsNullOrWhiteSpace(channel.Name) ? $"Channel {index + 1}" : channel.Name;

        Card($"chatbox-channel-{channel.Id}", title, _ =>
        {
            DrawChannelHeader(channel);
            DrawChannelIdentity(channel);
            DrawChannelSources(channel, textChannels);
            DrawChannelSending(channel);
            DrawChannelBehaviour(channel);
        });
    }

    private void DrawChannelHeader(ChatboxChannelConfig channel)
    {
        var enabled = channel.Enabled;
        if (ImGui.Checkbox($"Enabled##chatbox-en-{channel.Id}", ref enabled))
        {
            channel.Enabled = enabled;
            Save();
        }

        ImGui.SameLine();
        var showInNav = channel.ShowInNav;
        if (ImGui.Checkbox($"Show in Navigation##chatbox-nav-{channel.Id}", ref showInNav))
        {
            channel.ShowInNav = showInNav;
            Save();
        }

        ImGui.SameLine();
        if (theme.IconButton($"##chatbox-up-{channel.Id}", FontAwesomeIcon.ArrowUp, "Move up"))
        {
            pendingMoveId = channel.Id;
            pendingMoveDelta = -1;
        }

        ImGui.SameLine();
        if (theme.IconButton($"##chatbox-down-{channel.Id}", FontAwesomeIcon.ArrowDown, "Move down"))
        {
            pendingMoveId = channel.Id;
            pendingMoveDelta = 1;
        }

        ImGui.SameLine();
        if (theme.DangerIconButton($"##chatbox-del-{channel.Id}", FontAwesomeIcon.Trash, "Delete channel"))
            pendingRemoveId = channel.Id;
    }

    private void DrawChannelIdentity(ChatboxChannelConfig channel)
    {
        var name = channel.Name;
        ImGui.SetNextItemWidth(240f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText($"Name##chatbox-name-{channel.Id}", ref name, 64))
        {
            channel.Name = name;
            Save();
        }

        var shortLabel = channel.ShortLabel;
        ImGui.SetNextItemWidth(90f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText($"Rail Label##chatbox-short-{channel.Id}", ref shortLabel, 6))
        {
            channel.ShortLabel = shortLabel;
            Save();
        }

        ImGui.SameLine();
        var color = channel.Color;
        if (ImGui.ColorEdit4($"Color##chatbox-color-{channel.Id}", ref color, ImGuiColorEditFlags.NoInputs))
        {
            channel.Color = color;
            Save();
        }

        var iconUrl = channel.IconUrl;
        ImGui.SetNextItemWidth(320f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputTextWithHint($"Icon URL##chatbox-icon-{channel.Id}", "https://...", ref iconUrl, 260))
        {
            channel.IconUrl = iconUrl;
            Save();
        }
    }

    private void DrawChannelSources(ChatboxChannelConfig channel, IReadOnlyList<DiscordChannel>? textChannels)
    {
        theme.SpacerY(0.4f);

        var preview = channel.GameChatTypes.Count == 0
            ? "None"
            : string.Join(", ", channel.GameChatTypes.Select(ChatboxService.LabelFor).Distinct());

        ImGui.SetNextItemWidth(320f * ImGuiHelpers.GlobalScale);
        using (var combo = ImRaii.Combo($"Game Chats##chatbox-types-{channel.Id}", preview))
        {
            if (combo)
            {
                foreach (var type in SelectableChatTypes)
                {
                    var selected = channel.GameChatTypes.Contains(type);
                    if (!ImGui.Checkbox($"{ChatboxService.LabelFor(type)}##chatbox-{channel.Id}-{type}", ref selected))
                        continue;

                    if (selected) channel.GameChatTypes.Add(type);
                    else channel.GameChatTypes.Remove(type);

                    if (IsTellType(type))
                    {
                        if (selected) channel.GameChatTypes.Add(XivChatType.TellOutgoing);
                        else channel.GameChatTypes.Remove(XivChatType.TellOutgoing);
                    }

                    Save();
                    plugin.Chatbox.RebuildChannels();
                }
            }
        }

        if (textChannels != null)
        {
            theme.ChannelPicker(
                $"chatbox-discord-{channel.Id}",
                channel.DiscordChannelId,
                textChannels,
                id =>
                {
                    channel.DiscordChannelId = id;
                    Save();
                    plugin.Chatbox.RebuildChannels();
                },
                defaultLabel: "No Discord Channel",
                width: 320f * ImGuiHelpers.GlobalScale);
        }
        else
        {
            var discordId = channel.DiscordChannelId;
            ImGui.SetNextItemWidth(320f * ImGuiHelpers.GlobalScale);
            if (ImGui.InputTextWithHint($"Discord Channel ID##chatbox-dc-{channel.Id}", "Channel ID", ref discordId, 32))
            {
                channel.DiscordChannelId = discordId;
                Save();
                plugin.Chatbox.RebuildChannels();
            }
        }
    }

    private void DrawChannelSending(ChatboxChannelConfig channel)
    {
        theme.SpacerY(0.4f);

        var sendToGame = channel.SendToGame;
        if (ImGui.Checkbox($"Send to Game##chatbox-stg-{channel.Id}", ref sendToGame))
        {
            channel.SendToGame = sendToGame;
            Save();
        }

        var sendPreview = channel.SendGameChatType == XivChatType.None
            ? "None (Tell reply)"
            : ChatboxService.LabelFor(channel.SendGameChatType);

        ImGui.SetNextItemWidth(240f * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo($"Send as##chatbox-sendtype-{channel.Id}", sendPreview);
        if (!combo) return;

        if (ImGui.Selectable($"None (Tell reply)##chatbox-sendnone-{channel.Id}"))
        {
            channel.SendGameChatType = XivChatType.None;
            Save();
        }

        foreach (var type in SelectableChatTypes)
        {
            if (IsTellType(type)) continue;
            if (!ImGui.Selectable($"{ChatboxService.LabelFor(type)}##chatbox-send-{channel.Id}-{type}")) continue;

            channel.SendGameChatType = type;
            Save();
        }
    }

    private void DrawChannelBehaviour(ChatboxChannelConfig channel)
    {
        theme.SpacerY(0.4f);

        var mute = channel.MuteNotifications;
        if (ImGui.Checkbox($"Mute Notifications##chatbox-mute-{channel.Id}", ref mute))
        {
            channel.MuteNotifications = mute;
            Save();
        }

        ImGui.SameLine();
        var treatAll = channel.TreatAllAsMention;
        if (ImGui.Checkbox($"Treat every Message as Mention##chatbox-all-{channel.Id}", ref treatAll))
        {
            channel.TreatAllAsMention = treatAll;
            Save();
        }

        var filterAds = channel.FilterAdvertisements;
        if (ImGui.Checkbox($"Hide Advertisements##chatbox-ads-{channel.Id}", ref filterAds))
        {
            channel.FilterAdvertisements = filterAds;
            Save();
        }

        DrawChannelHistory(channel);
    }

    private void DrawChannelHistory(ChatboxChannelConfig channel)
    {
        theme.SpacerY(0.4f);

        var persist = channel.PersistHistory;
        if (ImGui.Checkbox($"Save History##chatbox-persist-{channel.Id}", ref persist))
        {
            channel.PersistHistory = persist;
            Save();
        }

        ImGui.SameLine();
        var stored = plugin.Chatbox.Store.CountFor(channel.Id);
        ImGui.TextDisabled($"{stored} stored");

        ImGui.SameLine();
        if (theme.Button($"Delete History##chatbox-forget-{channel.Id}"))
            plugin.Chatbox.ForgetChannel(channel.Id);

        var limit = channel.MaxMessages;
        ImGui.SetNextItemWidth(220f * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderInt($"Messages kept##chatbox-limit-{channel.Id}", ref limit, 0, 50000))
        {
            channel.MaxMessages = limit;
            Save();
        }

        if (channel.MaxMessages <= 0)
            ImGui.TextDisabled($"0 = use the global default ({plugin.Config.Chatbox.MaxMessagesPerChannel}).");
    }
}
