using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.EventArgs;

namespace Cordi.Services.Discord.Dispatch;

public interface IDiscordMessageHandler
{
    Task HandleAsync(MessageCreateEventArgs e, CancellationToken ct);
}
