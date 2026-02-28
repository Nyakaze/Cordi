using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Core;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;

namespace Cordi.Services.Features;

public class NearbyService : IDisposable
{
    private readonly CordiPlugin plugin;

    public class NearbyState
    {
        public ulong GameObjectId;
        public string Name = string.Empty;
        public string World = string.Empty;
        public float Distance;
        public float DirectionAngle;
        public string? CurrentTargetName;
        public ulong CurrentTargetId;
    }

    // Thread-safe dictionary to store nearby entities per polling tick
    public ConcurrentDictionary<ulong, NearbyState> NearbyPlayers { get; } = new();

    private DateTime _lastUpdate = DateTime.MinValue;

    public NearbyService(CordiPlugin plugin)
    {
        this.plugin = plugin;
        Service.Framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if ((DateTime.Now - _lastUpdate).TotalMilliseconds < 250) return;
        _lastUpdate = DateTime.Now;

        // --- PERFORMANCE BARRIER ---
        // Instantly shut down the loop if the feature isn't cleanly enabled or standard GUI window isn't currently open on screen
        if (!plugin.Config.Nearby.Enabled) return;

        if (plugin.NearbyWindow == null || !plugin.NearbyWindow.IsOpen)
        {
            // If the window is explicitly closed, immediately flush the tracked list so UI doesn't visually cache stale ghost data 
            if (NearbyPlayers.Count > 0)
            {
                NearbyPlayers.Clear();
            }
            return;
        }

        var localPlayer = Service.ObjectTable.LocalPlayer;
        if (localPlayer == null)
        {
            NearbyPlayers.Clear();
            return;
        }

        var currentFramePeers = new HashSet<ulong>();

        foreach (var obj in Service.ObjectTable)
        {
            if (obj is not IPlayerCharacter player) continue;

            if (player.GameObjectId == localPlayer.GameObjectId && !plugin.Config.Nearby.IncludeSelf)
            {
                continue;
            }

            var id = player.GameObjectId;
            currentFramePeers.Add(id);

            var state = NearbyPlayers.GetOrAdd(id, _ => new NearbyState
            {
                GameObjectId = id,
                Name = player.Name.TextValue,
                World = player.HomeWorld.Value.Name.ExtractText()
            });

            // Update spatial logic
            state.Distance = Vector3.Distance(localPlayer.Position, player.Position);

            var dx = player.Position.X - localPlayer.Position.X;
            var dz = player.Position.Z - localPlayer.Position.Z;
            var camRot = GetCameraRotation();
            var worldAngle = MathF.Atan2(dx, dz);
            var relative = worldAngle - (camRot + MathF.PI);
            while (relative > MathF.PI) relative -= 2 * MathF.PI;
            while (relative < -MathF.PI) relative += 2 * MathF.PI;
            state.DirectionAngle = -relative;

            // Target mapping logic
            if (player.TargetObjectId != 0)
            {
                state.CurrentTargetId = player.TargetObjectId;
                var pTarget = Service.ObjectTable.SearchById(player.TargetObjectId);
                state.CurrentTargetName = pTarget?.Name.TextValue;
            }
            else
            {
                state.CurrentTargetId = 0;
                state.CurrentTargetName = null;
            }
        }

        // Cleanup stale state references that physically vanished since last native polling cycle
        var staleIds = NearbyPlayers.Keys.Except(currentFramePeers).ToList();
        foreach (var staleId in staleIds)
        {
            NearbyPlayers.TryRemove(staleId, out _);
        }
    }

    private static unsafe float GetCameraRotation()
    {
        var cm = FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance();
        if (cm != null && cm->Camera != null)
            return cm->Camera->DirH;
        return 0f;
    }

    public void Dispose()
    {
        Service.Framework.Update -= OnFrameworkUpdate;
    }
}
