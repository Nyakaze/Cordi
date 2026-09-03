using System;
using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace Cordi.Services.Features;

public static unsafe class LinkshellNameService
{
    public const int SlotCount = 8;

    public static int LinkshellSlot(XivChatType type) => type switch
    {
        XivChatType.Ls1 => 0,
        XivChatType.Ls2 => 1,
        XivChatType.Ls3 => 2,
        XivChatType.Ls4 => 3,
        XivChatType.Ls5 => 4,
        XivChatType.Ls6 => 5,
        XivChatType.Ls7 => 6,
        XivChatType.Ls8 => 7,
        _ => -1,
    };

    public static int CrossWorldLinkshellSlot(XivChatType type) => type switch
    {
        XivChatType.CrossLinkShell1 => 0,
        XivChatType.CrossLinkShell2 => 1,
        XivChatType.CrossLinkShell3 => 2,
        XivChatType.CrossLinkShell4 => 3,
        XivChatType.CrossLinkShell5 => 4,
        XivChatType.CrossLinkShell6 => 5,
        XivChatType.CrossLinkShell7 => 6,
        XivChatType.CrossLinkShell8 => 7,
        _ => -1,
    };

    public static bool IsJoined(XivChatType type)
    {
        var slot = LinkshellSlot(type);
        if (slot >= 0)
            return GetLinkshellName(slot) != null;

        slot = CrossWorldLinkshellSlot(type);
        return slot < 0 || GetCrossWorldLinkshellName(slot) != null;
    }

    public static string? LabelFor(XivChatType type)
    {
        var slot = LinkshellSlot(type);
        if (slot >= 0)
            return Compose("LS", slot, GetLinkshellName(slot));

        slot = CrossWorldLinkshellSlot(type);
        return slot >= 0 ? Compose("CWLS", slot, GetCrossWorldLinkshellName(slot)) : null;
    }

    private static string Compose(string prefix, int slot, string? name) =>
        string.IsNullOrWhiteSpace(name) ? $"{prefix} {slot + 1}" : $"{prefix} {slot + 1}: {name}";

    public static string? GetLinkshellName(int slot)
    {
        if (slot < 0 || slot >= SlotCount)
            return null;

        try
        {
            var proxy = InfoProxyLinkshell.Instance();
            if (proxy == null)
                return null;

            var shells = proxy->LinkShells;
            if (slot >= shells.Length)
                return null;

            ulong id = shells[slot].Id;
            if (id == 0)
                return null;

            var name = proxy->GetLinkshellName(id);
            if (!name.HasValue)
                return null;

            var text = name.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string? GetCrossWorldLinkshellName(int slot)
    {
        if (slot < 0 || slot >= SlotCount)
            return null;

        try
        {
            var proxy = InfoProxyCrossWorldLinkshell.Instance();
            if (proxy == null)
                return null;

            var name = proxy->GetCrossworldLinkshellName((uint)slot);
            if (name == null)
                return null;

            var text = name->ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
