using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.EventArgs;

namespace Cordi.Services.Discord.Dispatch;

public interface IDiscordReactionHandler
{
    Task HandleAsync(MessageReactionAddEventArgs e, CancellationToken ct);
}
