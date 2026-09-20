using System;
using System.Threading.Tasks;
using Cordi.Core.Caching;
using Cordi.Domain;
using Cordi.Services.Discord;
using Cordi.Services.Discord.Connection;
using Crovus.Events;
using Crovus.Models;

namespace Cordi.Services.Features;

public class EmoteDiscordNotifier
{
    private static readonly TimeSpan SpamThreshold = TimeSpan.FromSeconds(60);

    private const string LogSource = "EmoteLog";

    private readonly Configuration.Configuration _config;
    private readonly CordiLogService _log;
    private readonly DiscordHandler _discord;
    private readonly DiscordEmbedFactory _embeds;
    private readonly DiscordConnection _connection;
    private readonly LodestoneService _lodestone;
    private readonly EmoteBackAction _emoteBackAction;

    private readonly Cache<ulong, EmoteEvent> _messageIdCache;
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
        _discord = discord;
        _embeds = embeds;
        _connection = connection;
        _lodestone = lodestone;
        _emoteBackAction = emoteBackAction;

        _messageIdCache = new Cache<ulong, EmoteEvent>(
            "emote.messages", caches, maxSize: 100, ttl: TimeSpan.FromHours(2));

        _active = new Cache<string, EmoteEvent>(
            "emote.active", caches, maxSize: 100, ttl: SpamThreshold);
    }

    public Cache<ulong, EmoteEvent> MessageIdCache => _messageIdCache;

    public Cache<string, EmoteEvent> Active => _active;

    public async Task ProcessDiscordEmote(Player player, string emoteName, string command)
    {
        if (!_config.EmoteLog.DiscordEnabled) return;
        if (!ulong.TryParse(_config.EmoteLog.ChannelId, out var channelId)) return;
        if (_connection.Context is null) return;

        var key = $"{player.FullName}-{emoteName}";

        var updateExisting = _active.TryGet(key, out var state);

        if (updateExisting)
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

        try
        {
            var avatarUrl = await _lodestone.GetAvatarUrlAsync(player);
            var lodestoneId = await _lodestone.ResolveLodestoneIdAsync(player);
            var embed = BuildEmoteEmbed(state, lodestoneId, avatarUrl);

            if (updateExisting && state.MessageId != 0)
            {
                await _discord.EditWebhookMessage(channelId, state.MessageId, embed);
                return;
            }

            state.MessageId = await _discord.SendWebhookMessage(channelId, embed, player);

            if (state.MessageId != 0)
            {
                _messageIdCache.Set(state.MessageId, state);
                await _discord.AddReaction(channelId, state.MessageId, "🔙");
            }
        }
        catch (Exception ex)
        {
            _log.Error(LogSource, "Failed to process Discord emote log", ex);
        }
    }

    private DiscordEmbed BuildEmoteEmbed(EmoteEvent state, string? lodestoneId, string avatarUrl)
    {
        var nameLink = !string.IsNullOrEmpty(lodestoneId)
            ? $"[{state.Player.FullName}](https://na.finalfantasyxiv.com/lodestone/character/{lodestoneId}/)"
            : state.Player.FullName;

        var color = state.EmotedBack ? 0x2ECC71 : 0x5865F2;
        var description = $"**{nameLink}** used **{state.EmoteName}** on you!";
        var footerText = state.EmotedBack ? "Interaction Complete" : "React with 🔙 to emote back";

        var embedBuilder = _embeds.CreateEmbedBuilder(
            "Emote Detected",
            description,
            color,
            avatarUrl,
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
        if (e.Emoji.Name != "🔙") return;
        if (!_messageIdCache.TryGet(e.MessageId, out var state)) return;
        if (state.EmotedBack) return;

        state.EmotedBack = true;

        var cmd = state.Command;
        if (string.IsNullOrEmpty(cmd)) cmd = "/" + state.EmoteName.ToLower().Replace(" ", "");

        await _emoteBackAction.PerformAsync(state.User, state.World, cmd, state.GameObjectId);

        if (!ulong.TryParse(_config.EmoteLog.ChannelId, out var channelId)) return;

        try
        {
            var avatarUrl = await _lodestone.GetAvatarUrlAsync(state.Player);
            var lodestoneId = await _lodestone.ResolveLodestoneIdAsync(state.Player);
            var embed = BuildEmoteEmbed(state, lodestoneId, avatarUrl);

            await _discord.EditWebhookMessage(channelId, state.MessageId, embed);
            await _discord.RemoveReaction(channelId, state.MessageId, "🔙");
        }
        catch (Exception ex)
        {
            _log.Error(LogSource, "Failed to update embed after reaction", ex);
        }
    }
}
