using System;
using Cordi.Extensions;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace Cordi.Domain;

public sealed class LocalPlayer : Player
{
    private LocalPlayer(string name, string world, ulong gameObjectId)
        : base(name, world, gameObjectId)
    {
    }

    public string CurrentWorld { get; private set; } = string.Empty;
    public uint ClassJobId { get; private set; }
    public string ClassJobAbbreviation { get; private set; } = string.Empty;
    public byte Level { get; private set; }
    public uint TerritoryId { get; private set; }

    public bool IsTravelling =>
        !string.IsNullOrEmpty(CurrentWorld) &&
        !string.Equals(CurrentWorld, World, StringComparison.OrdinalIgnoreCase);

    public static LocalPlayer From(IPlayerCharacter pc)
    {
        var player = new LocalPlayer(
            pc.Name.TextValue,
            pc.HomeWorld.Value.Name.ExtractText(),
            pc.GameObjectId);

        player.PopulateContentIdAndCustomize(pc);
        player.PopulateCompanyTag(pc);
        player.Refresh(pc);

        return player;
    }

    public void Refresh(IPlayerCharacter pc)
    {
        try
        {
            CurrentWorld = pc.CurrentWorld.Value.Name.ExtractText();
            ClassJobId = pc.ClassJob.RowId;
            ClassJobAbbreviation = pc.ClassJob.Value.Abbreviation.ToString();
            Level = pc.Level;
            TerritoryId = Service.ClientState.TerritoryType;
        }
        catch
        {
        }
    }
}
