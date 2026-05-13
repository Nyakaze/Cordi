using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Dispatch;
using DSharpPlus;
using DSharpPlus.EventArgs;

namespace Cordi.Services.Discord.Handlers.Presence;

[DiscordHandler]
public class ActivityPresenceHandler : IDiscordPresenceHandler
{
    private readonly CordiPlugin _plugin;

    public ActivityPresenceHandler(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public Task HandleAsync(DiscordClient sender, PresenceUpdateEventArgs e, CancellationToken ct)
        => _plugin.ActivityManager.OnPresenceUpdated(sender, e);
}
