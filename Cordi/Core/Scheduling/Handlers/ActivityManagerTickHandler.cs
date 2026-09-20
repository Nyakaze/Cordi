using Cordi.Services;
using Cordi.Services.Discord.Presence;
using Dalamud.Plugin.Services;

namespace Cordi.Core.Scheduling.Handlers;

[FrameworkTick]
public class ActivityManagerTickHandler : IFrameworkTickHandler
{
    private readonly DiscordPresenceWatcher _presence;
    private readonly ActivityManager _activity;

    public ActivityManagerTickHandler(DiscordPresenceWatcher presence, ActivityManager activity)
    {
        _presence = presence;
        _activity = activity;
    }

    public void Tick(IFramework framework)
    {
        _presence.Sync();
        _activity.OnFrameworkUpdate(framework);
    }
}
