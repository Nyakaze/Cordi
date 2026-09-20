using System;
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

        var hold = TimeSpan.FromSeconds(Math.Clamp(_config.EmoteLog.EmoteBackHoldSeconds, MinHoldSeconds, MaxHoldSeconds));
        var token = _logout.Token;

        float savedRotation = 0;
        bool targetFound = false;
        ulong unheldId = 0;

        await Service.Framework.RunOnFrameworkThread(() =>
        {
            var localPlayer = Service.ObjectTable.LocalPlayer;
            if (localPlayer == null) return;

            savedRotation = localPlayer.Rotation;

            IGameObject? target = null;
            if (targetId != 0)
            {
                target = Service.ObjectTable.SearchById(targetId);
            }

            target ??= Service.ObjectTable.FindPlayerByName(targetName, targetWorld);

            if (target != null)
            {
                if (target is IPlayerCharacter pc)
                {
                    _visibility.Unhide(pc, hold + TimeSpan.FromSeconds(2));
                    unheldId = pc.GameObjectId;
                }
                Service.TargetManager.Target = target;
                _chat.SendMessage(command);
                targetFound = true;
            }
        });

        if (!targetFound) return;

        try
        {
            await Task.Delay(hold, token);
        }
        catch (OperationCanceledException)
        {
            if (unheldId != 0) _visibility.Release(unheldId);
            return;
        }

        await Service.Framework.RunOnFrameworkThread(() =>
        {
            if (unheldId != 0) _visibility.Release(unheldId);

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
                    go->Rotation = savedRotation;
                }
            }
        });
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
