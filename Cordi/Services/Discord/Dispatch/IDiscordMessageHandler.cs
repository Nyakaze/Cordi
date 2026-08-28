using System.Threading;
using System.Threading.Tasks;
using Crovus.Events;

namespace Cordi.Services.Discord.Dispatch;

public interface IDiscordMessageHandler
{
    Task HandleAsync(MessageCreatedEvent e, CancellationToken ct);
}
