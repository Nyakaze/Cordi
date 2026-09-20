using System;
using System.Threading.Tasks;
using Cordi.Core.Caching;
using Cordi.Domain;
using Cordi.Services.Discord;
using Cordi.Services.Discord.Connection;
using Cordi.Services.Watchers;
using Crovus.Events;
using Crovus.Models;

namespace Cordi.Services.Features;

public class EmoteDiscordNotifier
{
    private static readonly TimeSpan SpamThreshold = TimeSpan.FromSeconds(60);

    private const string LogSource = "EmoteLog";

    private const string Reaction = "🔙";

    private readonly Configuration.Configuration _config;
    private readonly CordiLogService _log;
    private readonly DiscordEmbedFactory _embeds;
    private readonly DiscordConnection _connection;
    private readonly EmoteBackAction _emoteBackAction;

    private readonly WatcherNotifier<EmoteEvent> _notifier;
    private readonly Cache<string, EmoteEvent> _active;

    public EmoteDiscordNotifier(
        Configuration.Configuration config,
        CordiLogService log,
        ICacheRegistry caches,
        DiscordHandler discord,
        DiscordEmbedFactory embeds,
        DiscordConnection connection,
        LodestoneService lodestone,
        EmoteBackAction emoteBackAction)
    {
        _config = config;
        _log = log;
        _embeds = embeds;
        _connection = connection;
        _emoteBackAction = emoteBackAction;

        _notifier = new WatcherNotifier<EmoteEvent>(
            "emote.messages",
            Reaction,
            () => _config.EmoteLog.ChannelId,
            BuildEmoteEmbed,
            caches,
            discord,
            lodestone);

        _active = new Cache<string, EmoteEvent>(
            "emote.active", caches, maxSize: 100, ttl: SpamThreshold);
    }

    public Cache<ulong, EmoteEvent> MessageIdCache => _notifier.Messages;

    public Cache<string, EmoteEvent> Active => _active;

    public Task ProcessDiscordEmote(Player player, string emoteName, string command)
    {
        if (!_config.EmoteLog.DiscordEnabled) return Task.CompletedTask;
        if (_connection.Context is null) return Task.CompletedTask;

        var key = $"{player.FullName}-{emoteName}";

        if (_active.TryGet(key, out var state))
        {
            state.Count++;
            state.LastUpdate = DateTime.Now;
            state.Command = command;
        }
        else
        {
            state = new EmoteEvent(player, emoteName, command);
        }

        _active.Set(key, state);

        return _notifier.PublishAsync(state);
    }

    private DiscordEmbed BuildEmoteEmbed(EmoteEvent state, string? lodestoneId)
    {
        var nameLink = !string.IsNullOrEmpty(lodestoneId)
            ? $"[{state.Player.FullName}](https://na.finalfantasyxiv.com/lodestone/character/{lodestoneId}/)"
            : state.Player.FullName;

        var color = state.EmotedBack ? 0x2ECC71 : 0x5865F2;
        var description = $"**{nameLink}** used **{state.EmoteName}** on you!";
        var footerText = state.EmotedBack ? "Interaction Complete" : $"React with {Reaction} to emote back";

        var embedBuilder = _embeds.CreateEmbedBuilder(
            "Emote Detected",
            description,
            color,
            state.AvatarUrl,
            footerText
        );

        embedBuilder.AddField("Emote", state.EmoteName, true);

        if (state.Count > 1)
        {
            embedBuilder.AddField("Count", $"{state.Count} times", true);
        }

        if (state.EmotedBack)
        {
            embedBuilder.AddField("Status", "You emoted back!", false);
        }

        return embedBuilder.Build();
    }

    public async Task OnDiscordReactionAdded(ReactionAddedEvent e)
    {
        if (!_notifier.Matches(e)) return;
        if (!_notifier.Messages.TryGet(e.MessageId, out var state)) return;
        if (state.EmotedBack) return;

        state.EmotedBack = true;

        var cmd = state.Command;
        if (string.IsNullOrEmpty(cmd)) cmd = "/" + state.EmoteName.ToLower().Replace(" ", "");

        _log.FireAndForget(
            _emoteBackAction.PerformAsync(state.User, state.World, cmd, state.GameObjectId),
            LogSource,
            $"Failed to emote back at {state.User}@{state.World}");

        await _notifier.EditAsync(state);
        await _notifier.ClearReactionAsync(state);
    }
}
