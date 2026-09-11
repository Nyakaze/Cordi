using System;
using Dalamud.Game.Gui.ContextMenu;
using Lumina.Excel.Sheets;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
    private const uint ContextMenuPrefixRgb = 0x241D34;

    private static ushort? _contextMenuPrefixColor;

    private void InitializeContextMenu() => Service.ContextMenu.OnMenuOpened += OnContextMenuOpened;

    private void DisposeContextMenu() => Service.ContextMenu.OnMenuOpened -= OnContextMenuOpened;

    private void OnContextMenuOpened(IMenuOpenedArgs args)
    {
        try
        {
            if (!Config.Enabled || !ConversationSettings.Enabled || !ConversationSettings.ContextMenuEntry) return;
            if (args.MenuType != ContextMenuType.Default) return;
            if (args.Target is not MenuTargetDefault target) return;

            var name = target.TargetName?.Trim() ?? string.Empty;
            if (name.Length == 0) return;

            var world = target.TargetHomeWorld.ValueNullable?.Name.ExtractText() ?? string.Empty;
            if (world.Length == 0) return;

            if (IsLocalPlayer(name, world)) return;

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
