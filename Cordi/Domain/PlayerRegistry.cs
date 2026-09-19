using Cordi.Core.Caching;

namespace Cordi.Domain;

public sealed class PlayerRegistry
{
    private readonly Cache<string, Player> _players;

    public PlayerRegistry(ICacheRegistry registry)
    {
        _players = new Cache<string, Player>("players", registry, maxSize: 2000);
    }

    public int Count => _players.Count;

    public Player Intern(Player candidate)
    {
        var key = KeyFor(candidate.Name, candidate.World);
        if (key.Length <= 1) return candidate;

        if (_players.TryGet(key, out var known))
        {
            known.MergeFrom(candidate);
            return known;
        }

        _players.Set(key, candidate);
        return candidate;
    }

    public bool TryGet(string name, string world, out Player player) =>
        _players.TryGet(KeyFor(name, world), out player);

    public void Clear() => _players.Clear();

    private static string KeyFor(string name, string world) =>
        $"{name}@{world}".ToLowerInvariant();
}
