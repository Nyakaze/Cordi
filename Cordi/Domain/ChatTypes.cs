using System;
using System.Linq;
using Dalamud.Game.Text;

namespace Cordi.Domain;

public static class ChatTypes
{
    public const int LinkshellSlotCount = 8;

    public static readonly XivChatType[] Linkshells =
    {
        XivChatType.Ls1, XivChatType.Ls2, XivChatType.Ls3, XivChatType.Ls4,
        XivChatType.Ls5, XivChatType.Ls6, XivChatType.Ls7, XivChatType.Ls8,
    };

    public static readonly XivChatType[] CrossWorldLinkshells =
    {
        XivChatType.CrossLinkShell1, XivChatType.CrossLinkShell2, XivChatType.CrossLinkShell3, XivChatType.CrossLinkShell4,
        XivChatType.CrossLinkShell5, XivChatType.CrossLinkShell6, XivChatType.CrossLinkShell7, XivChatType.CrossLinkShell8,
    };

    public static readonly XivChatType[] GameMasterLinkshells =
    {
        XivChatType.GmLinkshell1, XivChatType.GmLinkshell2, XivChatType.GmLinkshell3, XivChatType.GmLinkshell4,
        XivChatType.GmLinkshell5, XivChatType.GmLinkshell6, XivChatType.GmLinkshell7, XivChatType.GmLinkshell8,
    };

    public static readonly XivChatType[] Tells =
    {
        XivChatType.TellIncoming, XivChatType.TellOutgoing,
    };

    public static readonly XivChatType[] Sendable = new[]
        {
            XivChatType.Say,
            XivChatType.Shout,
            XivChatType.Yell,
            XivChatType.Party,
            XivChatType.Alliance,
            XivChatType.FreeCompany,
        }
        .Concat(Linkshells)
        .Concat(CrossWorldLinkshells)
        .ToArray();

    public static readonly (string Label, XivChatType Key, uint Default, XivChatType[] Applies)[] ColorGroups =
    {
        ("Say", XivChatType.Say, 0xF7F7F7FF, new[] { XivChatType.Say, XivChatType.GmSay }),
        ("Shout", XivChatType.Shout, 0xFFA64DFF, new[] { XivChatType.Shout, XivChatType.GmShout }),
        ("Yell", XivChatType.Yell, 0xFFE659FF, new[] { XivChatType.Yell, XivChatType.GmYell }),
        ("Tells", XivChatType.TellIncoming, 0xFF8CD9FF, new[] { XivChatType.TellIncoming, XivChatType.TellOutgoing, XivChatType.GmTell }),
        ("Party", XivChatType.Party, 0x66CCFFFF, new[] { XivChatType.Party, XivChatType.CrossParty, XivChatType.GmParty }),
        ("Alliance", XivChatType.Alliance, 0xFF8C33FF, new[] { XivChatType.Alliance }),
        ("Free Company", XivChatType.FreeCompany, 0x8CE6F2FF, new[] { XivChatType.FreeCompany, XivChatType.FreeCompanyAnnouncement, XivChatType.FreeCompanyLoginLogout, XivChatType.GmFreeCompany }),
        ("Linkshells", XivChatType.Ls1, 0xD4FF7DFF, Linkshells.Concat(GameMasterLinkshells).ToArray()),
        ("Cross-world Linkshells", XivChatType.CrossLinkShell1, 0xD4FF7DFF, CrossWorldLinkshells),
        ("Novice Network", XivChatType.NoviceNetwork, 0xD4FF7DFF, new[] { XivChatType.NoviceNetwork, XivChatType.NoviceNetworkSystem, XivChatType.GmNoviceNetwork }),
        ("PvP Team", XivChatType.PvPTeam, 0xABDBE5FF, new[] { XivChatType.PvPTeam, XivChatType.PvpTeamAnnouncement, XivChatType.PvpTeamLoginLogout }),
        ("Emotes", XivChatType.CustomEmote, 0xBAFFF0FF, new[] { XivChatType.CustomEmote, XivChatType.StandardEmote }),
        ("NPC Dialogue", XivChatType.NPCDialogue, 0xABD647FF, new[] { XivChatType.NPCDialogue, XivChatType.NPCDialogueAnnouncements }),
        ("System", XivChatType.SystemMessage, 0xCCCCCCFF, new[]
        {
            XivChatType.SystemMessage, XivChatType.SystemError, XivChatType.GatheringSystemMessage, XivChatType.Echo,
            XivChatType.Orchestrion, XivChatType.Alarm, XivChatType.Sign, XivChatType.MessageBook,
            XivChatType.GlamourNotifications, XivChatType.RetainerSale, XivChatType.PeriodicRecruitmentNotification,
        }),
        ("Errors", XivChatType.ErrorMessage, 0xFF4A4AFF, new[] { XivChatType.ErrorMessage }),
        ("Notices", XivChatType.Notice, 0xB38CFFFF, new[] { XivChatType.Notice }),
        ("Urgent", XivChatType.Urgent, 0xFF7F7FFF, new[] { XivChatType.Urgent }),
        ("Debug", XivChatType.Debug, 0xCCCCCCFF, new[] { XivChatType.Debug }),
        ("Damage", XivChatType.Damage, 0xFF7D7DFF, new[] { XivChatType.Damage }),
        ("Misses", XivChatType.Miss, 0xCCCCCCFF, new[] { XivChatType.Miss }),
        ("Healing", XivChatType.Healing, 0xD4FF7DFF, new[] { XivChatType.Healing }),
        ("Actions and Items", XivChatType.Action, 0xFFFFB0FF, new[] { XivChatType.Action, XivChatType.Item, XivChatType.LootNotice }),
        ("Beneficial Effects", XivChatType.GainBuff, 0x94BFFFFF, new[] { XivChatType.GainBuff, XivChatType.LoseBuff }),
        ("Detrimental Effects", XivChatType.GainDebuff, 0xFF8AC4FF, new[] { XivChatType.GainDebuff, XivChatType.LoseDebuff }),
        ("Progress", XivChatType.Progress, 0xFFDE73FF, new[] { XivChatType.Progress }),
        ("Loot Rolls", XivChatType.LootRoll, 0xC7BF9EFF, new[] { XivChatType.LootRoll, XivChatType.RandomNumber }),
        ("Crafting and Gathering", XivChatType.Crafting, 0xDEBFF7FF, new[] { XivChatType.Crafting, XivChatType.Gathering }),
    };

    public static readonly (string Group, bool Simple, XivChatType[] Types)[] SelectableGroups =
    {
        ("Chat", true, new[]
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
        }),
        ("Linkshells", true, Linkshells),
        ("Cross-world Linkshells", true, CrossWorldLinkshells),
        ("Emotes", false, new[]
        {
            XivChatType.CustomEmote,
            XivChatType.StandardEmote,
        }),
        ("Battle", false, new[]
        {
            XivChatType.Damage,
            XivChatType.Miss,
            XivChatType.Action,
            XivChatType.Item,
            XivChatType.Healing,
            XivChatType.GainBuff,
            XivChatType.LoseBuff,
            XivChatType.GainDebuff,
            XivChatType.LoseDebuff,
        }),
        ("Announcements", false, new[]
        {
            XivChatType.FreeCompanyAnnouncement,
            XivChatType.FreeCompanyLoginLogout,
            XivChatType.PvpTeamAnnouncement,
            XivChatType.PvpTeamLoginLogout,
            XivChatType.NoviceNetworkSystem,
            XivChatType.NPCDialogue,
            XivChatType.NPCDialogueAnnouncements,
            XivChatType.PeriodicRecruitmentNotification,
            XivChatType.MessageBook,
        }),
        ("Progress", false, new[]
        {
            XivChatType.LootNotice,
            XivChatType.LootRoll,
            XivChatType.Progress,
            XivChatType.Crafting,
            XivChatType.Gathering,
            XivChatType.GlamourNotifications,
            XivChatType.RetainerSale,
        }),
        ("System", false, new[]
        {
            XivChatType.Echo,
            XivChatType.SystemMessage,
            XivChatType.SystemError,
            XivChatType.GatheringSystemMessage,
            XivChatType.ErrorMessage,
            XivChatType.Notice,
            XivChatType.Urgent,
            XivChatType.Debug,
            XivChatType.Alarm,
            XivChatType.Orchestrion,
            XivChatType.Sign,
            XivChatType.RandomNumber,
        }),
    };

    public static int LinkshellSlot(XivChatType type) => Array.IndexOf(Linkshells, type);

    public static int CrossWorldLinkshellSlot(XivChatType type) => Array.IndexOf(CrossWorldLinkshells, type);

    public static bool IsTell(XivChatType type) => type is XivChatType.TellIncoming or XivChatType.TellOutgoing;

    public static bool IsSendable(XivChatType type) => Array.IndexOf(Sendable, type) >= 0;

    public static bool IsGameMaster(XivChatType type) =>
        Array.IndexOf(GameMasterLinkshells, type) >= 0
        || type is XivChatType.GmTell
            or XivChatType.GmSay
            or XivChatType.GmShout
            or XivChatType.GmYell
            or XivChatType.GmParty
            or XivChatType.GmFreeCompany
            or XivChatType.GmNoviceNetwork;

    public static string SendGroup(XivChatType type)
    {
        if (LinkshellSlot(type) >= 0)
            return "Linkshells";

        return CrossWorldLinkshellSlot(type) >= 0 ? "Cross-world Linkshells" : "Chat";
    }

    public static string? SendCommand(XivChatType type)
    {
        var slot = LinkshellSlot(type);
        if (slot >= 0)
            return $"/ls{slot + 1}";

        slot = CrossWorldLinkshellSlot(type);
        if (slot >= 0)
            return $"/cwl{slot + 1}";

        return type switch
        {
            XivChatType.Say => "/say",
            XivChatType.Shout => "/sh",
            XivChatType.Yell => "/y",
            XivChatType.Party => "/p",
            XivChatType.Alliance => "/a",
            XivChatType.FreeCompany => "/fc",
            XivChatType.TellOutgoing => "/tell",
            _ => null,
        };
    }

    public static string Label(XivChatType type) => type switch
    {
        XivChatType.Say => "Say",
        XivChatType.Shout => "Shout",
        XivChatType.Yell => "Yell",
        XivChatType.Party => "Party",
        XivChatType.CrossParty => "Cross Party",
        XivChatType.Alliance => "Alliance",
        XivChatType.FreeCompany => "Free Company",
        XivChatType.TellIncoming => "Tells",
        XivChatType.TellOutgoing => "Tells",
        XivChatType.NoviceNetwork => "Novice Network",
        XivChatType.PvPTeam => "PvP Team",
        XivChatType.CustomEmote => "Custom Emotes",
        XivChatType.StandardEmote => "Standard Emotes",
        XivChatType.Damage => "Damage Dealt",
        XivChatType.Miss => "Missed Attacks",
        XivChatType.Action => "Actions Used",
        XivChatType.Item => "Items Used",
        XivChatType.Healing => "HP Recovery",
        XivChatType.GainBuff => "Beneficial Effects Granted",
        XivChatType.LoseBuff => "Beneficial Effects Lost",
        XivChatType.GainDebuff => "Detrimental Effects Inflicted",
        XivChatType.LoseDebuff => "Detrimental Effects Lost",
        XivChatType.FreeCompanyAnnouncement => "Free Company Announcements",
        XivChatType.FreeCompanyLoginLogout => "Free Company Login and Logout",
        XivChatType.PvpTeamAnnouncement => "PvP Team Announcements",
        XivChatType.PvpTeamLoginLogout => "PvP Team Login and Logout",
        XivChatType.NoviceNetworkSystem => "Novice Network Notices",
        XivChatType.NPCDialogue => "NPC Dialogue",
        XivChatType.NPCDialogueAnnouncements => "NPC Announcements",
        XivChatType.LootNotice => "Loot Messages",
        XivChatType.LootRoll => "Loot Rolls",
        XivChatType.Progress => "Progression Messages",
        XivChatType.Crafting => "Synthesis Messages",
        XivChatType.Gathering => "Gathering Messages",
        XivChatType.Sign => "Sign Messages",
        XivChatType.RandomNumber => "Random Number Messages",
        XivChatType.Orchestrion => "Orchestrion Track Messages",
        XivChatType.MessageBook => "Message Book Alerts",
        XivChatType.PeriodicRecruitmentNotification => "Recruitment Notices",
        XivChatType.GlamourNotifications => "Glamour Messages",
        XivChatType.RetainerSale => "Retainer Sales",
        XivChatType.Alarm => "Alarm Notifications",
        XivChatType.Echo => "Echo",
        XivChatType.SystemMessage => "System Messages",
        XivChatType.SystemError => "Battle System Messages",
        XivChatType.GatheringSystemMessage => "Gathering System Messages",
        XivChatType.ErrorMessage => "Error Messages",
        XivChatType.Notice => "Notices",
        XivChatType.Urgent => "Urgent Messages",
        XivChatType.Debug => "Debug Messages",
        XivChatType.GmTell => "GM Tells",
        XivChatType.GmSay => "GM Say",
        XivChatType.GmShout => "GM Shout",
        XivChatType.GmYell => "GM Yell",
        XivChatType.GmParty => "GM Party",
        XivChatType.GmFreeCompany => "GM Free Company",
        XivChatType.GmLinkshell1 => "GM Linkshell 1",
        XivChatType.GmLinkshell2 => "GM Linkshell 2",
        XivChatType.GmLinkshell3 => "GM Linkshell 3",
        XivChatType.GmLinkshell4 => "GM Linkshell 4",
        XivChatType.GmLinkshell5 => "GM Linkshell 5",
        XivChatType.GmLinkshell6 => "GM Linkshell 6",
        XivChatType.GmLinkshell7 => "GM Linkshell 7",
        XivChatType.GmLinkshell8 => "GM Linkshell 8",
        XivChatType.GmNoviceNetwork => "GM Novice Network",
        _ => type.ToString(),
    };
}
