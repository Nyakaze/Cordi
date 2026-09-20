using System;
using Cordi.Domain;
using Dalamud.Game.ClientState.Objects.Types;

namespace Cordi.Services;

public sealed class PeeperState
{
    public PeeperState(Player player)
    {
        Player = player;
        GameObjectId = player.GameObjectId ?? 0;
    }

    public Player Player { get; }

    public readonly ulong GameObjectId;

    public string Name => Player.Name;

    public string World => Player.World;

    public string AvatarUrl
    {
        get => Player.AvatarUrl ?? string.Empty;
        set => Player.AvatarUrl = value;
    }

    public DateTime StartTime;
    public DateTime LastSeen;
    public DateTime? EndTime;
    public ulong DiscordMessageId;
    public bool IsLooking;
    public float Distance;
    public float DirectionAngle;
    public string? CurrentTargetName;
    public ulong CurrentTargetId;
    public bool IsPresent;
    public bool TargetedBack;

    internal long SeenTick;
    internal IGameObject? Resolved;

    public IGameObject? Live => Resolved is { } obj && obj.IsValid() ? obj : null;
}
