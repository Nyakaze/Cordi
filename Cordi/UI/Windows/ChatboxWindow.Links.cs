using System;
using System.Linq;
using System.Numerics;
using Cordi.Domain;
using Cordi.Services.Chatbox;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Cordi.UI.Windows;

public sealed partial class ChatboxWindow
{
    private const string LinkPopupId = "##cordi-link-popup";

    private ContentSegment? _linkPopupSegment;
    private Player? _linkPopupPlayer;
    private XivChatType _linkPopupChatType;
    private bool _openLinkPopup;

    private bool _itemTooltipOpen;
    private uint _itemTooltipId;
    private uint _itemHoverFrames;
    private uint _itemHoverSeen;

    private void DrawGameLink(ChatboxMessage message, ContentSegment segment)
    {
        var color = segment.Color ?? DefaultLinkColor(segment.LinkKind);
        var clicked = _flow.Pill(
            segment.Text, DimColor(color, 0.28f), color, _theme.Radius(0.35f), out var hovered, true);

        if (!hovered || !ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows)) return;

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        HoverGameLink(segment);

        if (HasLinkPopup(segment.LinkKind) && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            OpenLinkPopup(message, segment);
            return;
        }

        if (clicked) LeftClickGameLink(message, segment);
    }

    private static bool HasLinkPopup(GameLinkKind kind) =>
        kind is not (GameLinkKind.PartyFinder or GameLinkKind.PartyFinderNotification);

    private Vector4 DefaultLinkColor(GameLinkKind kind) => kind switch
    {
        GameLinkKind.Item => new Vector4(0.80f, 0.90f, 1f, 1f),
        GameLinkKind.Status => new Vector4(0.50f, 0.95f, 0.65f, 1f),
        GameLinkKind.Map => new Vector4(1f, 0.85f, 0.40f, 1f),
        GameLinkKind.Quest => new Vector4(1f, 0.78f, 0.45f, 1f),
        GameLinkKind.Player => new Vector4(0.70f, 0.85f, 1f, 1f),
        _ => Lighten(_theme.Accent),
    };

    private void LeftClickGameLink(ChatboxMessage message, ContentSegment segment)
    {
        switch (segment.LinkKind)
        {
            case GameLinkKind.Map when segment.Link is MapLinkPayload map:
                Service.GameGui.OpenMapWithMapLink(map);
                break;

            case GameLinkKind.Quest when segment.Link is QuestPayload quest:
                OpenQuestLog(quest);
                break;

            case GameLinkKind.Plugin when segment.Link is DalamudLinkPayload plugin:
                InvokePluginLink(message, plugin);
                break;

            case GameLinkKind.PartyFinder:
                OpenPartyFinderListing(segment.LinkId);
                break;

            case GameLinkKind.PartyFinderNotification:
                OpenPartyFinder();
                break;

            case GameLinkKind.Achievement:
                OpenAchievement(segment.LinkId);
                break;

            default:
                OpenLinkPopup(message, segment);
                break;
        }
    }

    private void OpenLinkPopup(ChatboxMessage message, ContentSegment segment)
    {
        _linkPopupSegment = segment;
        _linkPopupChatType = message.GameChatType;
        _linkPopupPlayer = segment.Link is PlayerPayload payload ? ResolvePayloadPlayer(message, payload) : null;

        _openLinkPopup = true;
    }

    private static Player ResolvePayloadPlayer(ChatboxMessage message, PlayerPayload payload)
    {
        var sameAuthor = string.Equals(message.AuthorName, payload.PlayerName, StringComparison.Ordinal);

        return Player.FromChatSource(
            payload.PlayerName,
            payload.World.ValueNullable?.Name.ExtractText() ?? string.Empty,
            (ushort)payload.World.RowId,
            sameAuthor ? message.SenderContentId : 0,
            sameAuthor ? message.SenderAccountId : 0);
    }

    private void OpenPlayerPopup(ChatboxMessage message)
    {
        var player = Player.FromChatSource(
            message.AuthorName,
            message.AuthorWorld,
            message.SenderWorldId,
            message.SenderContentId,
            message.SenderAccountId);

        if (player.Name.Length == 0) return;

        _linkPopupSegment = null;
        _linkPopupChatType = message.GameChatType;
        _linkPopupPlayer = player;
        _openLinkPopup = true;
    }

    private void HoverGameLink(ContentSegment segment)
    {
        switch (segment.LinkKind)
        {
            case GameLinkKind.Item when segment.Link is ItemPayload item:
                Service.GameGui.HoveredItem = item.IsHQ
                    ? item.ItemId | 0x1_0000_0000UL
                    : item.ItemId;
                HoverItemTooltip(item);
                break;

            case GameLinkKind.Status when segment.Link is StatusPayload status:
                DrawStatusTooltip(status);
                break;

            case GameLinkKind.Map:
                ImGui.SetTooltip("Left-click to open map\nRight-click for more options");
                break;

            case GameLinkKind.Quest:
                ImGui.SetTooltip("Left-click to open the quest journal\nRight-click for more options");
                break;

            case GameLinkKind.PartyFinder:
            case GameLinkKind.PartyFinderNotification:
                ImGui.SetTooltip("Left-click to open Party Finder");
                break;

            case GameLinkKind.Achievement:
                ImGui.SetTooltip("Left-click to open the achievement\nRight-click for more options");
                break;

            case GameLinkKind.Plugin:
                ImGui.SetTooltip("Left-click to follow this plugin link\nRight-click for more options");
                break;
        }
    }

    private static void DrawStatusTooltip(StatusPayload payload)
    {
        var status = payload.Status.ValueNullable;
        if (status == null) return;

        using (ImRaii.Tooltip())
        {
            var icon = Service.TextureProvider.GetFromGameIcon(new GameIconLookup(status.Value.Icon)).GetWrapOrDefault();
            if (icon != null)
            {
                var size = 28f * ImGuiHelpers.GlobalScale;
                ImGui.Image(icon.Handle, new Vector2(size, size));
                ImGui.SameLine(0, 8f);
            }

            ImGui.BeginGroup();
            ImGui.TextColored(new Vector4(0.5f, 1f, 0.6f, 1f), status.Value.Name.ExtractText());
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Status Effect / Buff");
            ImGui.EndGroup();

            var description = status.Value.Description.ExtractText();
            if (!string.IsNullOrWhiteSpace(description))
            {
                ImGui.Separator();
                ImGui.PushTextWrapPos(320f * ImGuiHelpers.GlobalScale);
                ImGui.TextColored(new Vector4(0.85f, 0.85f, 0.85f, 1f), description);
                ImGui.PopTextWrapPos();
            }
        }
    }

    private void HoverItemTooltip(ItemPayload payload)
    {
        if (_itemTooltipOpen && _itemTooltipId == payload.RawItemId)
        {
            _itemHoverSeen = _itemHoverFrames;
            return;
        }

        _itemTooltipOpen = true;
        _itemTooltipId = payload.RawItemId;
        _itemHoverFrames = 0;
        _itemHoverSeen = 0;

        OpenItemTooltip(payload.RawItemId, IsEventItem(payload));
    }

    private void UpdateItemTooltip()
    {
        if (!_itemTooltipOpen) return;
        if (++_itemHoverFrames - _itemHoverSeen <= 1) return;

        ForceCloseItemTooltip();
    }

    private void ForceCloseItemTooltip()
    {
        if (!_itemTooltipOpen) return;

        CloseItemTooltip();
        _itemTooltipOpen = false;
        _itemTooltipId = 0;
        _itemHoverFrames = 0;
        _itemHoverSeen = 0;
    }

    public override void OnClose() => ForceCloseItemTooltip();

    private static unsafe void OpenItemTooltip(uint itemId, bool eventItem)
    {
        var stage = AtkStage.Instance();
        var agent = AgentItemDetail.Instance();
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("ItemDetail").Address;

        if (stage == null || agent == null || addon == null) return;

        agent->DetailKind = eventItem ? DetailKind.KeyItem : DetailKind.Item;
        agent->TypeOrId = itemId;
        agent->Index = 0;
        agent->Flag1 &= 0xEF;
        agent->ItemId = itemId;
        agent->Flag2 = 1;
        agent->Flag3 = 0;
        agent->AddonId = addon->Id;

        stage->TooltipManager.TooltipType |= 2;
        addon->Show(false, 15);
    }

    private static unsafe void CloseItemTooltip()
    {
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("ItemDetail").Address;
        if (addon != null) addon->Hide(true, false, 0);

        var agent = AgentItemDetail.Instance();
        if (agent == null) return;

        var eventData = stackalloc AtkValue[1];
        var values = stackalloc AtkValue[1];
        values->Type = AtkValueType.Int;
        values->Int = -1;

        agent->ReceiveEvent(eventData, values, 1, 1);
    }

    private void DrawLinkPopup()
    {
        if (_openLinkPopup)
        {
            _openLinkPopup = false;
            ImGui.OpenPopup(LinkPopupId);
        }

        var segment = _linkPopupSegment;
        var player = _linkPopupPlayer;
        if (segment == null && player == null) return;

        using var popup = ImRaii.Popup(LinkPopupId);
        if (!popup) return;

        if (player != null)
        {
            DrawPlayerPopup(player);
            return;
        }

        if (segment!.Link is ItemPayload item) DrawItemPopup(segment, item);
        else DrawGenericLinkPopup(segment);
    }

    private void DrawItemPopup(ContentSegment segment, ItemPayload payload)
    {
        var eventItem = IsEventItem(payload);
        var name = ItemName(segment, payload);

        var icon = eventItem
            ? Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.EventItem>()?.GetRowOrDefault(payload.ItemId)?.Icon ?? 0
            : Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>()?.GetRowOrDefault(payload.ItemId)?.Icon ?? 0;

        PopupHeader(name, segment.Color ?? DefaultLinkColor(GameLinkKind.Item), icon, payload.IsHQ);

        var row = eventItem
            ? null
            : Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>()?.GetRowOrDefault(payload.ItemId);

        if (row.HasValue)
        {
            if (row.Value.EquipSlotCategory.RowId != 0)
            {
                if (ImGui.Selectable("Try On")) TryOnItem(payload.RawItemId);
                if (ImGui.Selectable("Item Comparison")) CompareItem(payload.RawItemId);
            }

            if (row.Value.ItemSearchCategory.ValueNullable?.Category == 3)
                if (ImGui.Selectable("Search Recipes")) SearchRecipesUsingItem(payload.ItemId);

            if (ImGui.Selectable("Search for Item")) SearchForItem(payload.RawItemId);
        }

        if (ImGui.Selectable("Link in Game Chat")) LinkItem(payload.RawItemId);
        if (ImGui.Selectable("Insert in Input")) InsertText(name);
        if (ImGui.Selectable("Copy Item Name")) ImGui.SetClipboardText(name);
    }

    private void DrawGenericLinkPopup(ContentSegment segment)
    {
        PopupHeader(segment.Text, segment.Color ?? DefaultLinkColor(segment.LinkKind), 0, false);

        if (ImGui.Selectable("Insert in Input")) InsertText(segment.Text);
        if (ImGui.Selectable("Copy Text")) ImGui.SetClipboardText(segment.Text);
    }

    private static void PopupHeader(string title, Vector4 color, uint iconId, bool hq)
    {
        if (iconId > 0)
        {
            var icon = Service.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId, hq)).GetWrapOrDefault();
            if (icon != null)
            {
                var size = 24f * ImGuiHelpers.GlobalScale;
                ImGui.Image(icon.Handle, new Vector2(size, size));
                ImGui.SameLine(0, 8f);
            }
        }

        ImGui.TextColored(color, title);
        ImGui.Separator();
    }

    private static bool IsEventItem(ItemPayload payload) => payload.RawItemId >= 2_000_000;

    private static string ItemName(ContentSegment segment, ItemPayload payload)
    {
        var sheetName = IsEventItem(payload)
            ? Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.EventItem>()?.GetRowOrDefault(payload.ItemId)?.Name.ExtractText()
            : Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>()?.GetRowOrDefault(payload.ItemId)?.Name.ExtractText();

        if (!string.IsNullOrEmpty(sheetName)) return sheetName;

        var text = segment.Text;
        if (text.Length >= 2 && text[0] == '[' && text[^1] == ']') text = text[1..^1];
        return text.TrimStart('\ue0bb').TrimEnd('\ue03c', '\ue03d').Trim();
    }

    private static void InvokePluginLink(ChatboxMessage message, DalamudLinkPayload link)
    {
        var source = message.Source;
        if (source == null) return;

        var start = source.Payloads.IndexOf(link);
        if (start < 0) return;

        var end = source.Payloads.IndexOf(RawPayload.LinkTerminator, start);
        if (end < 0) return;

        if (!Service.Chat.RegisteredLinkHandlers.TryGetValue((link.Plugin, link.CommandId), out var handler))
            return;

        var payloads = source.Payloads.Skip(start).Take(end - start + 1).ToList();
        Service.Framework.RunOnTick(() => handler.Invoke(link.CommandId, new SeString(payloads)));
    }

    private static unsafe void OpenQuestLog(QuestPayload payload)
    {
        var quest = payload.Quest.ValueNullable;
        if (quest == null) return;

        AgentQuestJournal.Instance()->OpenForQuest(payload.Quest.RowId & 0xFFFF, 1);
    }

    private static unsafe void OpenPartyFinderListing(uint listingId) =>
        AgentLookingForGroup.Instance()->OpenListing(listingId);

    private static unsafe void OpenPartyFinder()
    {
        var lfg = AgentLookingForGroup.Instance();
        if (lfg == null || lfg->IsAgentActive()) return;

        lfg->Show();
    }

    private static unsafe void OpenAchievement(uint achievementId) =>
        AgentAchievement.Instance()->OpenById(achievementId);

    private static unsafe void TryOnItem(uint itemId) =>
        AgentTryon.TryOn(0xFF, itemId, 0);

    private static unsafe void CompareItem(uint itemId) =>
        AgentItemComp.Instance()->CompareItem(0x4D, itemId, 0, 0);

    private static unsafe void SearchRecipesUsingItem(uint itemId) =>
        AgentRecipeProductList.Instance()->SearchForRecipesUsingItem(itemId);

    private static unsafe void SearchForItem(uint itemId) =>
        ItemFinderModule.Instance()->SearchForItem(itemId);

    private static unsafe void LinkItem(uint itemId) =>
        AgentChatLog.Instance()->LinkItem(itemId);
}
