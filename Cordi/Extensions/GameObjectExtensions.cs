using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;

namespace Cordi.Extensions;

public static class GameObjectExtensions
{
    public static IPlayerCharacter? FindPlayerByName(
        this IObjectTable table, string name, string? world = null)
    {
        foreach (var obj in table)
        {
            if (obj is IPlayerCharacter pc
                && pc.Name.TextValue == name
                && (world == null || pc.HomeWorld.Value.Name.ExtractText() == world))
                return pc;
        }
        return null;
    }
}
