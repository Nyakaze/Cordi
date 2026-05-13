using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;

namespace Cordi.Services.Discord.Dispatch;

public interface IDiscordPresenceHandler
{
    Task HandleAsync(DiscordClient sender, PresenceUpdateEventArgs e, CancellationToken ct);
}
