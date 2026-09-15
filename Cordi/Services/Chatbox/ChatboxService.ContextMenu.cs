using System;
using Cordi.Configuration;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using Lumina.Excel.Sheets;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
    private const uint ContextMenuPrefixRgb = 0x5534C2;

    private static readonly string[] PlayerContextMenuAddons =
    [
        "PartyMemberList",
        "FriendList",
        "FreeCompany",
        "LinkShell",
        "CrossWorldLinkshell",
        "_PartyList",
        "ChatLog",
        "LookingForGroup",
        "LookingForGroupDetail",
        "BlackList",
        "ContentMemberList",
        "SocialList",
        "ContactList",
        "BeginnerChatList",
    ];

    private static ushort? _contextMenuPrefixColor;

    private Hook<RaptureShellModule.Delegates.SetContextTellTarget>? _tellTargetHook;

    private void InitializeContextMenu()
    {
        Service.ContextMenu.OnMenuOpened += OnContextMenuOpened;
        InitializeTellTargetHook();
    }

    private void DisposeContextMenu()
    {
        Service.ContextMenu.OnMenuOpened -= OnContextMenuOpened;

        _tellTargetHook?.Dispose();
        _tellTargetHook = null;
    }

    private unsafe void InitializeTellTargetHook()
    {
        try
        {
            var address = (nint)RaptureShellModule.MemberFunctionPointers.SetContextTellTarget;
            if (address == nint.Zero) return;

            _tellTargetHook = Service.GameInteropProvider
                .HookFromAddress<RaptureShellModule.Delegates.SetContextTellTarget>(address, OnSetContextTellTarget);

            _tellTargetHook.Enable();
        }
        catch (Exception ex)
        {
            _plugin.LogService.Log(
                "Chatbox",
                CordiLogLevel.Warning,
                "Could not hook the game tell target, Send Tell keeps using the game chat",
                ex);
        }
    }

    private unsafe bool OnSetContextTellTarget(
        RaptureShellModule* module,
        Utf8String* playerName,
        Utf8String* worldName,
        ushort worldId,
        ulong accountId,
        ulong contentId,
        ushort reason,
        bool setChatType)
    {
        try
        {
            if (TryHandleTellTarget(playerName, worldName, worldId)) return true;
        }
        catch (Exception ex)
        {
            _plugin.LogService.Log("Chatbox", CordiLogLevel.Warning, "Send Tell handler failed", ex);
        }

        return _tellTargetHook!.Original(module, playerName, worldName, worldId, accountId, contentId, reason, setChatType);
    }

    private unsafe bool TryHandleTellTarget(Utf8String* playerName, Utf8String* worldName, ushort worldId)
    {
        if (_disposed || !Config.Enabled || playerName == null) return false;

        var window = _plugin.ChatboxWindow;
        if (window == null) return false;

        var name = playerName->ToString().Trim();
        if (name.Length == 0) return false;

        var world = ResolveWorldName(worldId);
        if (world.Length == 0 && worldName != null) world = worldName->ToString().Trim();

        RevealDuringCinematic();

        if (ConversationSettings.TellRouting != ConversationTellRouting.ChannelsOnly
            && CanOpenConversationWith(name, world)
            && OpenConversationFor(name, world) != null)
            return true;

        window.IsOpen = true;
        RequestInputFocus = true;
        PendingInputText = world.Length > 0 ? $"/tell {name}@{world} " : $"/tell {name} ";

        return true;
    }

    private static string ResolveWorldName(ushort worldId) =>
        worldId == 0
            ? string.Empty
            : Service.DataManager.GetExcelSheet<World>()?.GetRowOrDefault(worldId)?.Name.ExtractText() ?? string.Empty;

    private void OnContextMenuOpened(IMenuOpenedArgs args)
    {
        try
        {
            if (!Config.Enabled || !ConversationSettings.Enabled) return;
            if (args.MenuType != ContextMenuType.Default) return;
            if (args.Target is not MenuTargetDefault target) return;
            if (!IsPlayerContextMenu(args, target)) return;

            var name = target.TargetName?.Trim() ?? string.Empty;
            var world = target.TargetHomeWorld.ValueNullable?.Name.ExtractText() ?? string.Empty;

            if (!CanOpenConversationWith(name, world)) return;

            args.AddMenuItem(new MenuItem
            {
                Name = "Message",
                PrefixChar = 'C',
                PrefixColor = ResolveContextMenuPrefixColor(),
                OnClicked = _ => OpenConversationFor(name, world),
            });
        }
        catch (Exception ex)
        {
            _plugin.LogService.Error("Chatbox", "Failed to build the player context menu entry.", ex);
        }
    }

    public bool CanOpenConversationWith(string name, string world) =>
        Config.Enabled
        && ConversationSettings.Enabled
        && name.Length > 0
        && world.Length > 0
        && !string.Equals(name, ChatboxMessage.SystemSender, StringComparison.Ordinal)
        && !IsLocalPlayer(name, world);

    private static bool IsPlayerContextMenu(IMenuOpenedArgs args, MenuTargetDefault target)
    {
        if (target.TargetHomeWorld.ValueNullable is not { IsPublic: true }) return false;

        var addon = args.AddonName;

        if (string.IsNullOrEmpty(addon))
            return target.TargetObject is IPlayerCharacter;

        return Array.IndexOf(PlayerContextMenuAddons, addon) >= 0;
    }

    private static ushort ResolveContextMenuPrefixColor()
    {
        if (_contextMenuPrefixColor.HasValue) return _contextMenuPrefixColor.Value;

        ushort best = 0;
        var bestDistance = long.MaxValue;

        foreach (var row in Service.DataManager.GetExcelSheet<UIColor>())
        {
            if (row.RowId > ushort.MaxValue) continue;

            var distance = ColorDistance(row.Dark >> 8, ContextMenuPrefixRgb);
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = (ushort)row.RowId;
        }

        _contextMenuPrefixColor = best;
        return best;
    }

    private static long ColorDistance(uint left, uint right)
    {
        long dr = (long)((left >> 16) & 0xFF) - ((right >> 16) & 0xFF);
        long dg = (long)((left >> 8) & 0xFF) - ((right >> 8) & 0xFF);
        long db = (long)(left & 0xFF) - (right & 0xFF);

        return dr * dr + dg * dg + db * db;
    }

    private bool IsLocalPlayer(string name, string world)
    {
        var local = _plugin.cachedLocalPlayer;
        if (local == null) return false;

        if (!string.Equals(local.Name.TextValue, name, StringComparison.Ordinal)) return false;

        var localWorld = local.HomeWorld.ValueNullable?.Name.ExtractText() ?? string.Empty;
        return string.Equals(localWorld, world, StringComparison.OrdinalIgnoreCase);
    }
}
