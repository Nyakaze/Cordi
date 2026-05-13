using System;
using Cordi.Extensions;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace Cordi.Domain;

public sealed class Player : IEquatable<Player>
{
    public string Name { get; }
    public string World { get; }
    public string FullName => $"{Name}@{World}";

    public string? LodestoneId { get; set; }
    public ulong? GameObjectId { get; private set; }
    public ulong? ContentId { get; private set; }

    public byte? RaceId { get; private set; }
    public byte? TribeId { get; private set; }
    public byte? Gender { get; private set; }
    public string? FreeCompanyTag { get; private set; }

    private Player(string name, string world, ulong? gameObjectId = null, string? lodestoneId = null)
    {
        Name = name ?? string.Empty;
        World = world ?? string.Empty;
        GameObjectId = gameObjectId;
        LodestoneId = lodestoneId;
    }

    public static Player FromNameWorld(string name, string world)
        => new(name, world);

    public static Player FromGameObject(IPlayerCharacter pc)
    {
        var player = new Player(
            pc.Name.TextValue,
            pc.HomeWorld.Value.Name.ExtractText(),
            pc.GameObjectId);

        player.PopulateContentIdAndCustomize(pc);
        player.PopulateCompanyTag(pc);
        return player;
    }

    public static Player FromPartyMember(string name, string world, ulong? contentId)
    {
        var player = new Player(name, world);
        if (contentId.HasValue && contentId.Value != 0) player.ContentId = contentId;
        return player;
    }

    private unsafe void PopulateContentIdAndCustomize(IPlayerCharacter pc)
    {
        try
        {
            var character = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)pc.Address;
            if (character == null) return;

            var cid = character->ContentId;
            if (cid != 0) ContentId = cid;

            var customize = character->DrawData.CustomizeData;
            RaceId = customize.Race;
            Gender = customize.Sex;
            TribeId = customize.Tribe;
        }
        catch
        {
        }
    }

    private void PopulateCompanyTag(IPlayerCharacter pc)
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

        pc = Service.ObjectTable.FindPlayerByName(Name, World);
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
