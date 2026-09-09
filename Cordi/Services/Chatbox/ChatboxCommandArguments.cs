using System;
using System.Collections.Generic;
using Cordi.Core;

namespace Cordi.Services.Chatbox;

public static class ChatboxCommandArguments
{
    public static bool TakesPlayerTarget(string command) =>
        command is "/tell" or "/t" or "/trade" or "/invite" or "/blist" or "/friendlist";

    public static void SearchPlayers(CordiPlugin plugin, string fragment, List<string> results, int limit)
    {
        results.Clear();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var homeWorld = plugin.LocalPlayer?.Current?.World ?? string.Empty;

        foreach (var member in Service.PartyList)
        {
            var name = member.Name.TextValue;
            if (name.Length == 0) continue;

            var world = member.World.ValueNullable?.Name.ExtractText() ?? string.Empty;
            var target = world.Length > 0 && !string.Equals(world, homeWorld, StringComparison.OrdinalIgnoreCase)
                ? $"{name}@{world}"
                : name;

            if (!Add(target, fragment, seen, results, limit)) return;
        }

        var active = plugin.Chatbox.ActiveChannel;
        if (active == null) return;

        foreach (var author in active.RecentAuthors())
        {
            if (!Add(author, fragment, seen, results, limit)) return;
        }
    }

    private static bool Add(string value, string fragment, HashSet<string> seen, List<string> results, int limit)
    {
        if (value.Length == 0) return true;
        if (fragment.Length > 0 && !value.StartsWith(fragment, StringComparison.OrdinalIgnoreCase)) return true;
        if (!seen.Add(value)) return true;

        results.Add(value);

        return results.Count < limit;
    }
}
