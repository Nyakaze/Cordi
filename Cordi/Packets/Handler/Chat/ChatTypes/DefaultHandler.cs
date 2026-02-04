using System;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Services.Discord;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

namespace Cordi.Packets.Handler.Chat.ChatTypes;

public class DefaultHandler : IChatHandler
{
    static readonly IPluginLog Logger = Service.Log;

    public XivChatType ChatType => default;
    public async Task<Task> HandleAsync(ChatMessage msg, DiscordHandler discord, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }


}
