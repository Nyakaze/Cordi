using System;
using Cordi.Domain;
using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace Cordi.Services.Features;

public static unsafe class LinkshellNameService
{
    public const int SlotCount = ChatTypes.LinkshellSlotCount;

    public static bool IsJoined(XivChatType type)
    {
        var slot = ChatTypes.LinkshellSlot(type);
        if (slot >= 0)
            return GetLinkshellName(slot) != null;

        slot = ChatTypes.CrossWorldLinkshellSlot(type);
        return slot < 0 || GetCrossWorldLinkshellName(slot) != null;
    }

    public static string? LabelFor(XivChatType type)
    {
        var slot = ChatTypes.LinkshellSlot(type);
        if (slot >= 0)
            return Compose("LS", slot, GetLinkshellName(slot));

        slot = ChatTypes.CrossWorldLinkshellSlot(type);
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
