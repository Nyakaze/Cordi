using System.Threading;
using System.Threading.Tasks;
using Cordi.Packets.Handler.Chat;
using Cordi.Services.Discord;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

namespace Cordi.Packets.Handler.Chat;

public interface IChatHandler
{
    XivChatType ChatType { get; }
    Task<Task> HandleAsync(ChatMessage msg, DiscordHandler discord, CancellationToken ct = default);
}
