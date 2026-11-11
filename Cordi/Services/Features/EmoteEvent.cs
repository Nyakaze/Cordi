using System;
using Cordi.Domain;
using Cordi.Services.Watchers;

using Cordi.Services;

namespace Cordi.Services.Features;

public sealed class EmoteEvent : IWatcherAlert
{
    public EmoteEvent(Player player, string emoteName, string command, bool isCustom = false)
    {
        Player = player;
        IsCustom = isCustom;
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

    public string AvatarUrl
    {
        get => Player.AvatarUrl ?? string.Empty;
        set => Player.AvatarUrl = value;
    }

    public string EmoteName { get; }

    public string Command { get; set; }

    public bool IsCustom { get; }

    public bool AcceptsReaction => !IsCustom;

    public string ReplyCommand => IsCustom
        ? string.Empty
        : !string.IsNullOrEmpty(Command)
            ? Command
            : "/" + EmoteName.ToLowerInvariant().Replace(" ", "");

    public ulong MessageId { get; set; }

    public int Count = 1;
    public bool EmotedBack;
    public DateTime FirstSeen;
    public DateTime LastUpdate;
}
