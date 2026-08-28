using Crovus.Events;
using Crovus.Models;

namespace Cordi.Services.Discord.Connection;

public sealed class DiscordSession
{
    private DiscordUser? _currentUser;
    private Snowflake? _applicationId;
    private Snowflake? _guildId;
    private string? _sessionId;

    public DiscordUser? CurrentUser => _currentUser;

    public Snowflake? ApplicationId => _applicationId;

    public Snowflake? GuildId => _guildId;

    public string? SessionId => _sessionId;

    public bool IsReady => _currentUser is not null;

    public bool IsSelf(DiscordUser? user) =>
        user is not null && _currentUser is { } self && user.Id == self.Id;

    public bool IsSelf(Snowflake userId) => _currentUser is { } self && userId == self.Id;

    internal void Apply(ReadyEvent ready)
    {
        _currentUser = ready.User;
        _applicationId = ready.ApplicationId;
        _sessionId = ready.SessionId;

        if (ready.Guilds.Count > 0)
            _guildId = ready.Guilds[0].Id;
    }

    internal void Adopt(Snowflake guildId)
    {
        _guildId ??= guildId;
    }

    internal void Clear()
    {
        _currentUser = null;
        _applicationId = null;
        _guildId = null;
        _sessionId = null;
    }
}
