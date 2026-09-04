using System;
using System.Linq;
using Cordi.Domain;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Party;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace Cordi.Services.Chatbox;

public static unsafe class ChatboxPlayerActions
{
    public static bool IsPublicWorld(Player player)
    {
        var worldId = player.ResolveWorldId();
        if (worldId == 0) return false;

        return Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>()
            ?.GetRowOrDefault(worldId)?.IsPublic ?? false;
    }

    public static bool IsInInstance() => Service.Condition[ConditionFlag.BoundByDuty56];

    public static bool IsMentor()
    {
        var state = PlayerState.Instance();
        return state != null && state->IsMentor();
    }

    public static ulong LocalContentId()
    {
        var state = PlayerState.Instance();
        return state == null ? 0 : state->ContentId;
    }

    public static bool IsSelf(Player player) =>
        player.ContentId is { } id && id != 0 && id == LocalContentId();

    public static bool IsPartyLeader()
    {
        var party = Service.PartyList;
        if (party.Length == 0) return true;

        var leader = party[(int)party.PartyLeaderIndex];
        return leader != null && (ulong)leader.ContentId == LocalContentId();
    }

    public static IPartyMember? FindPartyMember(Player player)
    {
        var worldId = player.ResolveWorldId();

        return Service.PartyList.FirstOrDefault(member =>
            string.Equals(member.Name.TextValue, player.Name, StringComparison.Ordinal)
            && (worldId == 0 || member.World.RowId == worldId));
    }

    public static bool CanInviteToParty(Player player) =>
        IsInInstance()
            ? player.ContentId is not (null or 0) || player.IsNearby
            : player.ContentId is not (null or 0) || player.ResolveWorldId() != 0;

    public static bool InviteToParty(Player player)
    {
        var proxy = InfoProxyPartyInvite.Instance();
        if (proxy == null) return false;

        if (IsInInstance())
        {
            if (player.ContentId is { } contentId && contentId != 0
                && proxy->InviteToPartyInInstanceByContentId(contentId))
                return true;

            return player.EntityId is { } entityId && entityId != 0
                   && proxy->InviteToPartyInInstanceByEntityId(entityId);
        }

        return SharesCurrentWorld(player)
            ? InviteByName(proxy, player) || InviteByContentId(proxy, player)
            : InviteByContentId(proxy, player) || InviteByName(proxy, player);
    }

    private static bool InviteByName(InfoProxyPartyInvite* proxy, Player player)
    {
        var worldId = player.ResolveWorldId();
        return worldId != 0 && proxy->InviteToParty(player.ContentId ?? 0, player.Name, worldId);
    }

    private static bool InviteByContentId(InfoProxyPartyInvite* proxy, Player player) =>
        player.ContentId is { } contentId && contentId != 0 && proxy->InviteToPartyContentId(contentId, 0);

    private static bool SharesCurrentWorld(Player player)
    {
        if (player.IsNearby) return true;

        var current = Service.ObjectTable.LocalPlayer?.CurrentWorld.RowId ?? 0;
        return current != 0 && player.ResolveWorldId() == current;
    }

    public static void Promote(Player player, ulong contentId)
    {
        var agent = AgentPartyMember.Instance();
        if (agent == null) return;

        agent->Promote(player.Name, 0, contentId);
    }

    public static void Kick(Player player, ulong contentId)
    {
        var agent = AgentPartyMember.Instance();
        if (agent == null) return;

        agent->Kick(player.Name, 0, contentId);
    }

    public static void InviteToNoviceNetwork(Player player)
    {
        var proxy = InfoProxyNoviceNetwork.Instance();
        if (proxy == null) return;

        proxy->InviteToNoviceNetwork(0, 0, player.ResolveWorldId(), player.Name);
    }

    public static bool CanMute(Player player) => player.AccountId is not (null or 0);

    public static void AddToMuteList(Player player)
    {
        var agent = AgentMutelist.Instance();
        if (agent == null || player.AccountId is not { } accountId) return;

        agent->Add(accountId, player.ContentId ?? 0, player.Name, (short)player.ResolveWorldId());
    }

    public static bool CanOpenAdventurerPlate(Player player) => player.ContentId is not (null or 0);

    public static void OpenAdventurerPlate(Player player)
    {
        var agent = AgentCharaCard.Instance();
        if (agent == null || player.ContentId is not { } contentId || contentId == 0) return;

        agent->OpenCharaCard(contentId);
    }

    public static void Examine(Player player)
    {
        var agent = AgentInspect.Instance();
        if (agent == null || player.EntityId is not { } entityId || entityId == 0) return;

        agent->ExamineCharacter(entityId, false);
    }

    public static void Target(Player player)
    {
        if (player.TryResolveInWorld(out var character) && character != null)
            Service.TargetManager.Target = character;
    }
}
