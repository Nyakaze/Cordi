using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Packets.Handler.Chat;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Cordi.Extensions;

namespace Cordi.Services.Features;

public class EmoteBackAction : IDisposable
{
    private const int MinHoldSeconds = 1;
    private const int MaxHoldSeconds = 60;

    private readonly Configuration.Configuration _config;
    private readonly ChatMessenger _chat;
    private readonly VisibilityService _visibility;

    private readonly object _gate = new();
    private readonly HashSet<ulong> _sessionHeld = new();

    private int _sequence;
    private bool _sessionActive;
    private float _sessionRotation;

    private CancellationTokenSource _logout = new();
    private bool _disposed;

    public EmoteBackAction(Configuration.Configuration config, ChatMessenger chat, VisibilityService visibility)
    {
        _config = config;
        _chat = chat;
        _visibility = visibility;

        Service.ClientState.Logout += OnLogout;
    }

    public async Task PerformAsync(string targetName, string targetWorld, string command, ulong targetId = 0, bool keepTarget = false, bool keepRotation = false)
    {
        if (_disposed) return;
        if (string.IsNullOrWhiteSpace(command) || !command.StartsWith('/')) return;

        var hold = TimeSpan.FromSeconds(Math.Clamp(_config.EmoteLog.EmoteBackHoldSeconds, MinHoldSeconds, MaxHoldSeconds));
        var token = _logout.Token;

        int ticket = 0;

        await Service.Framework.RunOnFrameworkThread(() =>
        {
            var localPlayer = Service.ObjectTable.LocalPlayer;
            if (localPlayer == null) return;

            IGameObject? target = null;
            if (targetId != 0)
            {
                target = Service.ObjectTable.SearchById(targetId);
            }

            target ??= Service.ObjectTable.FindPlayerByName(targetName, targetWorld);
            if (target == null) return;

            lock (_gate)
            {
                if (!_sessionActive)
                {
                    _sessionActive = true;
                    _sessionRotation = localPlayer.Rotation;
                }

                ticket = ++_sequence;
            }

            if (target is IPlayerCharacter pc)
            {
                _visibility.UnhideForEmoteBack(pc, hold + TimeSpan.FromSeconds(2));

                lock (_gate)
                {
                    _sessionHeld.Add(pc.GameObjectId);
                }
            }

            Service.TargetManager.Target = target;
            _chat.SendMessage(command);
        });

        if (ticket == 0) return;

        try
        {
            await Task.Delay(hold, token);
        }
        catch (OperationCanceledException)
        {
            ReleaseSession(ticket);
            return;
        }

        await Service.Framework.RunOnFrameworkThread(() =>
        {
            if (!ReleaseSession(ticket)) return;

            var localPlayer = Service.ObjectTable.LocalPlayer;
            if (localPlayer == null) return;

            if (!keepTarget)
            {
                var currentTarget = Service.TargetManager.Target;
                if (currentTarget != null)
                {
                    if (currentTarget.Name.ToString() == targetName)
                    {
                        Service.TargetManager.Target = null;
                    }
                }
            }

            if (!keepRotation)
            {
                unsafe
                {
                    var go = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)localPlayer.Address;
                    go->Rotation = _sessionRotation;
                }
            }
        });
    }

    private bool ReleaseSession(int ticket)
    {
        ulong[] held;

        lock (_gate)
        {
            if (!_sessionActive || ticket != _sequence) return false;

            held = _sessionHeld.ToArray();
            _sessionHeld.Clear();
            _sessionActive = false;
        }

        foreach (var id in held)
        {
            _visibility.Release(id);
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Service.ClientState.Logout -= OnLogout;

        _logout.Cancel();
        _logout.Dispose();
    }

    private void OnLogout(int type, int code)
    {
        var previous = _logout;
        _logout = new CancellationTokenSource();

        previous.Cancel();
        previous.Dispose();
    }
}
