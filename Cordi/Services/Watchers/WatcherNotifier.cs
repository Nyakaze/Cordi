using System;
using System.Threading.Tasks;
using Cordi.Core.Caching;
using Cordi.Domain;
using Cordi.Services.Discord;
using Cordi.Services.Features;
using Crovus.Events;
using Crovus.Models;

namespace Cordi.Services.Watchers;

public interface IWatcherAlert
{
    Player Player { get; }

    ulong MessageId { get; set; }

    string AvatarUrl { get; set; }
}

public sealed class WatcherNotifier<TState> where TState : class, IWatcherAlert
{
    private readonly string _reaction;
    private readonly Func<string> _channelId;
    private readonly Func<TState, string?, DiscordEmbed> _embed;
    private readonly DiscordHandler _discord;
    private readonly LodestoneService _lodestone;
    private readonly Cache<ulong, TState> _messages;

    public WatcherNotifier(
        string cacheName,
        string reaction,
        Func<string> channelId,
        Func<TState, string?, DiscordEmbed> embed,
        ICacheRegistry caches,
        DiscordHandler discord,
        LodestoneService lodestone)
    {
        _reaction = reaction;
        _channelId = channelId;
        _embed = embed;
        _discord = discord;
        _lodestone = lodestone;

        _messages = new Cache<ulong, TState>(cacheName, caches, maxSize: 100, ttl: TimeSpan.FromHours(2));
    }

    public Cache<ulong, TState> Messages => _messages;

    public bool Matches(ReactionAddedEvent e) => e.Emoji.Name == _reaction;

    public async Task PublishAsync(TState state)
    {
        if (!ulong.TryParse(_channelId(), out var channelId)) return;

        state.AvatarUrl = await _lodestone.GetAvatarUrlAsync(state.Player);

        var embed = _embed(state, await _lodestone.ResolveLodestoneIdAsync(state.Player));

        if (state.MessageId != 0)
        {
            _messages.Set(state.MessageId, state);
            await _discord.EditWebhookMessage(channelId, state.MessageId, embed);
            return;
        }

        state.MessageId = await _discord.SendWebhookMessage(channelId, embed, state.Player);

        if (state.MessageId == 0) return;

        _messages.Set(state.MessageId, state);
        await _discord.AddReaction(channelId, state.MessageId, _reaction);
    }

    public async Task EditAsync(TState state)
    {
        if (state.MessageId == 0) return;
        if (!ulong.TryParse(_channelId(), out var channelId)) return;

        _messages.Set(state.MessageId, state);

        var embed = _embed(state, await _lodestone.ResolveLodestoneIdAsync(state.Player));

        await _discord.EditWebhookMessage(channelId, state.MessageId, embed);
    }

    public async Task ClearReactionAsync(TState state)
    {
        if (state.MessageId == 0) return;
        if (!ulong.TryParse(_channelId(), out var channelId)) return;

        await _discord.RemoveReaction(channelId, state.MessageId, _reaction);
    }
}
