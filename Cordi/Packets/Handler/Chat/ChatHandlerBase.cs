using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Services;
using Cordi.Services.Discord;
using Cordi.Core;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace Cordi.Packets.Handler.Chat;

public abstract class ChatHandlerBase : IChatHandler
{
    protected static readonly IPluginLog Logger = Service.Log;
    private static CordiLogService Log => CordiPlugin.Plugin.LogService;

    public abstract XivChatType ChatType { get; }

    public async Task<Task> HandleAsync(ChatMessage msg, DiscordHandler discord, CancellationToken ct = default)
    {
        if (msg.Sender.Payloads.FirstOrDefault(x => x.Type == PayloadType.Player) is not PlayerPayload playerLink)
        {
            if (msg.Sender.TextValue.EndsWith(CordiPlugin.Plugin.cachedLocalPlayer.Name.TextValue))
            {
                Log.Debug("ChatHandler", $"Routing [{ChatType}] from self to Discord");
                await discord.SendMessage(
                    null,
                    msg.Message,
                    CordiPlugin.Plugin.cachedLocalPlayer.Name.TextValue,
                    CordiPlugin.Plugin.cachedLocalPlayer.HomeWorld.Value.Name.ExtractText(),
                    ChatType
                );
                return Task.CompletedTask;
            }

            Log.Warning("ChatHandler", $"No player payload for [{ChatType}] sender: {msg.Sender.TextValue}");
            return Task.CompletedTask;
        }
        else
        {
            Log.Debug("ChatHandler", $"Routing [{ChatType}] from {playerLink.PlayerName} to Discord");
            await discord.SendMessage(
                null,
                msg.Message,
                playerLink.PlayerName,
                playerLink.World.Value.Name.ExtractText(),
                ChatType
            );
        }

        return Task.CompletedTask;
    }
}
