using System;
using Cordi.Extensions;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace Cordi.Domain;

public class Player : IEquatable<Player>
{
    public string Name { get; }
    public string World { get; }
    public string FullName => $"{Name}@{World}";

    public string? LodestoneId { get; set; }
    public string? AvatarUrl { get; set; }
    public ulong? GameObjectId { get; private set; }
    public ulong? ContentId { get; private set; }
    public ulong? AccountId { get; private set; }
    public ushort? WorldId { get; private set; }
    public uint? EntityId { get; private set; }

    public byte? RaceId { get; private set; }
    public byte? TribeId { get; private set; }
    public byte? Gender { get; private set; }
    public string? FreeCompanyTag { get; private set; }

    protected Player(string name, string world, ulong? gameObjectId = null, string? lodestoneId = null)
    {
        Name = name ?? string.Empty;
        World = world ?? string.Empty;
        GameObjectId = gameObjectId;
        LodestoneId = lodestoneId;
    }

    public static Player FromNameWorld(string name, string world, ulong? gameObjectId = null)
        => new(name, world, gameObjectId);

    public static Player FromGameObject(IPlayerCharacter pc)
    {
        var player = new Player(
            pc.Name.TextValue,
            pc.HomeWorld.Value.Name.ExtractText(),
            pc.GameObjectId);

        player.PopulateContentIdAndCustomize(pc);
        player.PopulateCompanyTag(pc);
        player.EntityId = pc.EntityId;
        player.WorldId = (ushort)pc.HomeWorld.RowId;
        return player;
    }

    public static Player FromPartyMember(string name, string world, ulong? contentId)
    {
        var player = new Player(name, world);
        if (contentId.HasValue && contentId.Value != 0) player.ContentId = contentId;
        return player;
    }

    public static Player FromChatSource(
        string name, string world, ushort worldId = 0, ulong contentId = 0, ulong accountId = 0)
    {
        var player = new Player(name.Trim(), world);
        player.ApplyGameIds(contentId, accountId, worldId);
        player.EnrichFromWorld();
        return player;
    }

    public void ApplyGameIds(ulong contentId, ulong accountId, ushort worldId)
    {
        if (contentId != 0) ContentId = contentId;
        if (accountId != 0) AccountId = accountId;
        if (worldId != 0) WorldId = worldId;
    }

    public bool EnrichFromWorld()
    {
        if (!TryResolveInWorld(out var pc) || pc == null) return false;

        PopulateContentIdAndCustomize(pc);
        EntityId = pc.EntityId;
        WorldId ??= (ushort)pc.HomeWorld.RowId;
        return true;
    }

    public ushort ResolveWorldId()
    {
        if (WorldId is { } known && known != 0) return known;
        if (string.IsNullOrEmpty(World)) return 0;

        var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>();
        if (sheet == null) return 0;

        foreach (var row in sheet)
        {
            if (!string.Equals(row.Name.ExtractText(), World, StringComparison.OrdinalIgnoreCase)) continue;

            WorldId = (ushort)row.RowId;
            return WorldId.Value;
        }

        return 0;
    }

    public string ResolveWorldName()
    {
        if (!string.IsNullOrEmpty(World)) return World;
        if (WorldId is not { } id || id == 0) return string.Empty;

        return Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>()
            ?.GetRowOrDefault(id)?.Name.ExtractText() ?? string.Empty;
    }

    public bool IsNearby => EntityId is { } id && id != 0;

    protected unsafe void PopulateContentIdAndCustomize(IPlayerCharacter pc)
    {
        try
        {
            var character = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)pc.Address;
            if (character == null) return;

            var cid = character->ContentId;
            if (cid != 0) ContentId = cid;

            var aid = character->AccountId;
            if (aid != 0) AccountId = aid;

            var customize = character->DrawData.CustomizeData;
            RaceId = customize.Race;
            Gender = customize.Sex;
            TribeId = customize.Tribe;
        }
        catch
        {
        }
    }

    protected void PopulateCompanyTag(IPlayerCharacter pc)
    {
        try
        {
            var tag = pc.CompanyTag.TextValue;
            FreeCompanyTag = string.IsNullOrWhiteSpace(tag) ? null : tag;
        }
        catch
        {
        }
    }

    public bool TryResolveInWorld(out IPlayerCharacter? pc)
    {
        if (GameObjectId.HasValue)
        {
            var obj = Service.ObjectTable.SearchById(GameObjectId.Value);
            if (obj is IPlayerCharacter resolved)
            {
                pc = resolved;
                return true;
            }
        }

        pc = Service.ObjectTable.FindPlayerByName(Name, string.IsNullOrEmpty(World) ? null : World);
        if (pc != null)
        {
            GameObjectId = pc.GameObjectId;
            return true;
        }

        GameObjectId = null;
        return false;
    }

    public bool IsCurrentlyVisible() => TryResolveInWorld(out _);

    public bool Equals(Player? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(World, other.World, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => obj is Player p && Equals(p);

    public override int GetHashCode() =>
        HashCode.Combine(
            Name.ToLowerInvariant(),
            World.ToLowerInvariant());

    public override string ToString() => FullName;
}
