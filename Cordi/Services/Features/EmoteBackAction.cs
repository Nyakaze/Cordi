using System.Threading.Tasks;
using Cordi.Packets.Handler.Chat;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Cordi.Extensions;

namespace Cordi.Services.Features;

public class EmoteBackAction
{
    private readonly ChatMessenger _chat;

    public EmoteBackAction(ChatMessenger chat)
    {
        _chat = chat;
    }

    public async Task PerformAsync(string targetName, string targetWorld, string command, ulong targetId = 0, bool keepTarget = false, bool keepRotation = false)
    {
        float savedRotation = 0;
        bool targetFound = false;

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
                    VisibilityBridge.UnhidePlayer(pc, allowVoided: true, isEmote: true);
                }
                Service.TargetManager.Target = target;
                _chat.SendMessage(command);
                targetFound = true;
            }
        });

        if (!targetFound) return;

        await Task.Delay(8000);

        await Service.Framework.RunOnFrameworkThread(() =>
        {
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

                ulong finalTargetId = targetId;
                if (finalTargetId == 0)
                {
                    var targetPlayer = Service.ObjectTable.FindPlayerByName(targetName, targetWorld);
                    if (targetPlayer != null)
                    {
                        finalTargetId = targetPlayer.GameObjectId;
                    }
                }

                if (finalTargetId != 0)
                {
                    VisibilityBridge.RestorePlayerHiddenState(finalTargetId);
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
