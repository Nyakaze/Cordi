using System;
using Cordi.Domain;

namespace Cordi.Services;

public sealed class EmoteEvent
{
    public EmoteEvent(Player player, string emoteName, string command)
    {
        Player = player;
        GameObjectId = player.GameObjectId ?? 0;
        EmoteName = emoteName;
        Command = command;
        FirstSeen = DateTime.Now;
        LastUpdate = FirstSeen;
    }

    public Player Player { get; }

    public readonly ulong GameObjectId;

    public string User => Player.Name;

    public string World => Player.World;

    public string EmoteName { get; }

    public string Command { get; set; }

    public int Count = 1;
    public ulong MessageId;
    public bool EmotedBack;
    public DateTime FirstSeen;
    public DateTime LastUpdate;
}
