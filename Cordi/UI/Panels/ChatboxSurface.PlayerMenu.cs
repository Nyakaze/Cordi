using Cordi.Domain;
using Cordi.Services.Chatbox;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;

namespace Cordi.UI.Panels;

public sealed partial class ChatboxSurface
{
    private void DrawPlayerPopup(Player player)
    {
        var world = player.ResolveWorldName();
        var full = world.Length > 0 ? $"{player.Name}@{world}" : player.Name;

        PopupHeader(full, DefaultLinkColor(GameLinkKind.Player), 0, false);

        DrawConversationEntry(player, world);

        if (ImGui.Selectable("Send Tell")) InsertText($"/tell {full} ");

        DrawReplyEntry();

        if (!ChatboxPlayerActions.IsSelf(player))
        {
            DrawPartyEntries(player);

            if (ChatboxPlayerActions.IsMentor() && ImGui.Selectable("Invite to Novice Network"))
                ChatboxPlayerActions.InviteToNoviceNetwork(player);

            if (ChatboxPlayerActions.CanMute(player) && ImGui.Selectable("Add to Mute List"))
                ChatboxPlayerActions.AddToMuteList(player);
        }

        if (player.IsNearby)
        {
            if (ImGui.Selectable("Target")) ChatboxPlayerActions.Target(player);
            if (ImGui.Selectable("Examine")) ChatboxPlayerActions.Examine(player);
        }

        if (ChatboxPlayerActions.CanOpenAdventurerPlate(player) && ImGui.Selectable("Adventurer Plate"))
            ChatboxPlayerActions.OpenAdventurerPlate(player);

        ImGui.Separator();

        if (ImGui.Selectable("Insert in Input")) InsertText(player.Name);
        if (ImGui.Selectable("Copy Name")) ImGui.SetClipboardText(full);
    }

    private void DrawConversationEntry(Player player, string world)
    {
        if (!Chatbox.CanOpenConversationWith(player.Name, world)) return;

        var label = Chatbox.ConversationsInOwnWindow ? "Open DM in a Window" : "Open DM in the Chatbox";
        if (!ImGui.Selectable(label)) return;

        Chatbox.OpenConversationFor(player.Name, world);
    }

    private void DrawReplyEntry()
    {
        var type = _linkPopupChatType;
        if (type == XivChatType.None || !ChatboxService.IsSendTargetAvailable(type)) return;

        if (!ImGui.Selectable($"Reply in {ChatboxService.LabelFor(type)}")) return;

        Config.LastSendChatType = type;
        _plugin.Config.Save();
        _focusInput = true;
    }

    private static void DrawPartyEntries(Player player)
    {
        if (!ChatboxPlayerActions.IsPartyLeader()) return;

        var member = ChatboxPlayerActions.FindPartyMember(player);
        if (member != null)
        {
            if (ImGui.Selectable("Promote")) ChatboxPlayerActions.Promote(player, (ulong)member.ContentId);
            if (ImGui.Selectable("Kick from Party")) ChatboxPlayerActions.Kick(player, (ulong)member.ContentId);
            return;
        }

        if (!ChatboxPlayerActions.CanInviteToParty(player)) return;
        if (ImGui.Selectable("Invite to Party")) ChatboxPlayerActions.InviteToParty(player);
    }
}
