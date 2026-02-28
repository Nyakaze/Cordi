using System;
using System.Threading.Tasks;
using Cordi.Core;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Cordi.Extensions;

namespace Cordi.Services.Features;

public class EmoteBackAction
{
    private readonly CordiPlugin _plugin;
    private readonly IPluginLog _log;

    public EmoteBackAction(CordiPlugin plugin)
    {
        _plugin = plugin;
        _log = Service.Log;
    }

    public async Task PerformAsync(string targetName, string targetWorld, string command, ulong targetId = 0, bool keepTarget = false, bool keepRotation = false)
    {
        float savedRotation = 0;
        bool targetFound = false;

        await CordiPlugin.Framework.RunOnFrameworkThread(() =>
        {
            var localPlayer = Service.ClientState.LocalPlayer;
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
                Service.TargetManager.Target = target;
                _plugin._chat.SendMessage(command);
                targetFound = true;
            }
        });

        if (!targetFound) return;

        await Task.Delay(8000);

        await CordiPlugin.Framework.RunOnFrameworkThread(() =>
        {
            var localPlayer = Service.ClientState.LocalPlayer;
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
}
