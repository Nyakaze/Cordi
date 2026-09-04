using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
    private static string LinkDisplayText(GameLinkKind kind, Payload? payload, uint id, string captured)
    {
        var text = captured.Replace("\ue0bb", string.Empty).Trim();
        if (text.Length > 0) return text;

        return kind switch
        {
            GameLinkKind.Item => ItemFallbackName(payload as ItemPayload),
            GameLinkKind.Status => Fallback((payload as StatusPayload)?.Status.ValueNullable?.Name.ExtractText(), "Status"),
            GameLinkKind.Map => payload is MapLinkPayload map
                ? $"{map.PlaceName} ({map.XCoord:F1}, {map.YCoord:F1})"
                : "Map Link",
            GameLinkKind.Quest => Fallback((payload as QuestPayload)?.Quest.ValueNullable?.Name.ExtractText(), "Quest"),
            GameLinkKind.Player => Fallback((payload as PlayerPayload)?.PlayerName, "Player"),
            GameLinkKind.Plugin => "Link",
            GameLinkKind.PartyFinder => "Party Finder",
            GameLinkKind.PartyFinderNotification => "Party Finder",
            GameLinkKind.Achievement => Fallback(
                Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Achievement>()?.GetRowOrDefault(id)?.Name.ExtractText(),
                "Achievement"),
            _ => string.Empty,
        };
    }

    private static string Fallback(string? value, string placeholder) =>
        string.IsNullOrEmpty(value) ? placeholder : value;

    private static string ItemFallbackName(ItemPayload? payload)
    {
        if (payload == null) return "Item";

        var raw = payload.RawItemId;
        var name = raw >= 2_000_000
            ? Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.EventItem>()?.GetRowOrDefault(payload.ItemId)?.Name.ExtractText()
            : Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>()?.GetRowOrDefault(payload.ItemId)?.Name.ExtractText();

        if (string.IsNullOrEmpty(name)) return "Item";

        var suffix = payload.IsHQ ? "\ue03c" : raw is >= 500_000 and < 1_000_000 ? "\ue03d" : string.Empty;
        return $"{name}{suffix}";
    }

    private static bool TryReadRawLink(RawPayload payload, out GameLinkKind kind, out uint id)
    {
        kind = GameLinkKind.None;
        id = 0;

        var data = payload.Data;
        if (data.Length < 4 || data[0] != 0x02 || data[1] != 0x27) return false;

        switch (data[3])
        {
            case 0x0A when data.Length > 7:
                kind = GameLinkKind.PartyFinder;
                id = ReadPackedInteger(data, 4);
                return true;

            case 0x06 when data.Length > 5:
                kind = GameLinkKind.Achievement;
                id = ReadPackedInteger(data, 4);
                return true;

            case 0x08 when IsPeriodicRecruitment(data):
                kind = GameLinkKind.PartyFinderNotification;
                return true;

            default:
                return false;
        }
    }

    private static readonly byte[] PeriodicRecruitmentLink =
        [0x02, 0x27, 0x07, 0x08, 0x01, 0x01, 0x01, 0xFF, 0x01, 0x03];

    private static bool IsPeriodicRecruitment(byte[] data) =>
        data.AsSpan().SequenceEqual(PeriodicRecruitmentLink);

    private static uint ReadPackedInteger(byte[] data, int offset)
    {
        if (offset >= data.Length) return 0;

        var first = (uint)data[offset++];
        if (first < 208u) return first - 1u;

        var flags = (first + 1) & 15;
        var bytes = new byte[4];

        for (var index = 3; index >= 0; index--)
        {
            if ((flags & (1u << index)) == 0) continue;
            if (offset >= data.Length) return 0;
            bytes[index] = data[offset++];
        }

        return BitConverter.ToUInt32(bytes, 0);
    }

    private static bool IsLinkTerminator(RawPayload payload)
    {
        var data = payload.Data;
        return data.Length >= 4 && data[0] == 0x02 && data[1] == 0x27 && data[3] == 0xCF;
    }

    private static Vector4? PeekColor(Stack<uint> stack) =>
        stack.Count > 0 ? RgbaToColor(stack.Peek()) : null;

    private static Vector4? RgbaToColor(uint rgba)
    {
        if (rgba == 0) return null;

        var alpha = (byte)(rgba & 0xFF);
        if (alpha == 0) alpha = 0xFF;

        return new Vector4(
            ((rgba & 0xFF000000) >> 24) / 255f,
            ((rgba & 0x00FF0000) >> 16) / 255f,
            ((rgba & 0x0000FF00) >> 8) / 255f,
            alpha / 255f);
    }

    private static void ApplyRawColor(RawPayload payload, Stack<uint> foreground, Stack<uint> glow)
    {
        var data = payload.Data;
        if (data.Length < 4 || data[0] != 0x02) return;

        var target = data[1] switch
        {
            0x13 => foreground,
            0x14 => glow,
            _ => null,
        };

        if (target == null) return;

        var kind = data[3];
        if (kind == 0xEC)
        {
            if (target.Count > 0) target.Pop();
            return;
        }

        if (kind is >= 0xF0 and <= 0xFE)
        {
            var flags = kind + 1;
            var index = 4;
            uint argb;

            if ((flags & 8) != 0)
            {
                if (index >= data.Length) return;
                argb = (uint)data[index++] << 24;
            }
            else
            {
                argb = 0xFF000000u;
            }

            if ((flags & 4) != 0)
            {
                if (index >= data.Length) return;
                argb |= (uint)data[index++] << 16;
            }

            if ((flags & 2) != 0)
            {
                if (index >= data.Length) return;
                argb |= (uint)data[index++] << 8;
            }

            if ((flags & 1) != 0)
            {
                if (index >= data.Length) return;
                argb |= data[index];
            }

            target.Push((argb << 8) | (argb >> 24));
            return;
        }

        target.Push(0);
    }
}
