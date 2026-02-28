using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord;
using Dalamud.Plugin.Services;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace Cordi.Services.Features;

public class EmoteDiscordNotifier
{
    private readonly CordiPlugin _plugin;
    private readonly IPluginLog _logger;
    private readonly EmoteBackAction _emoteBackAction;

    private readonly ConcurrentDictionary<ulong, EmoteLogService.DiscordEmoteState> _messageIdCache;
    private readonly ConcurrentDictionary<string, EmoteLogService.DiscordEmoteState> _activeDiscordEmotes;
    private readonly TimeSpan _spamThreshold;

    public EmoteDiscordNotifier(
        CordiPlugin plugin,
        ConcurrentDictionary<ulong, EmoteLogService.DiscordEmoteState> messageIdCache,
        ConcurrentDictionary<string, EmoteLogService.DiscordEmoteState> activeDiscordEmotes,
        TimeSpan spamThreshold,
        EmoteBackAction emoteBackAction)
    {
        _plugin = plugin;
        _logger = Service.Log;
        _messageIdCache = messageIdCache;
        _activeDiscordEmotes = activeDiscordEmotes;
        _spamThreshold = spamThreshold;
        _emoteBackAction = emoteBackAction;
    }

    public async Task ProcessDiscordEmote(string name, string world, ulong gameObjectId, string emoteName, string command, int uiCount)
    {
        if (!_plugin.Config.EmoteLog.DiscordEnabled) return;

        if (string.IsNullOrEmpty(_plugin.Config.EmoteLog.ChannelId)) return;
        if (!ulong.TryParse(_plugin.Config.EmoteLog.ChannelId, out var channelId)) return;
        if (_plugin.Discord?.Client == null) return;

        string key = $"{name}@{world}-{emoteName}";

        bool updateExisting = false;
        EmoteLogService.DiscordEmoteState state = null;

        if (_activeDiscordEmotes.TryGetValue(key, out state))
        {
            if (DateTime.Now - state.LastUpdate < _spamThreshold)
            {
                updateExisting = true;
                state.Count++;
                state.LastUpdate = DateTime.Now;
            }
            else
            {
                _activeDiscordEmotes.TryRemove(key, out _);
                state = null;
            }
        }

        if (state == null)
        {
            state = new EmoteLogService.DiscordEmoteState
            {
                User = name,
                World = world,
                EmoteName = emoteName,
                Command = command,
                Count = 1,
                LastUpdate = DateTime.Now,
                FirstSeen = DateTime.Now,
                EmotedBack = false,
                GameObjectId = gameObjectId
            };
            _activeDiscordEmotes[key] = state;
        }

        try
        {
            var avatarUrl = await _plugin.Lodestone.GetAvatarUrlAsync(name, world);

            string description = $"**{name}@{world}** used **{emoteName}** on you!";
            if (state.Count > 1) description += $" (x{state.Count})";
            if (state.EmotedBack) description += "\n\n✅ *You emoted back!*";

            var embedBuilder = _plugin.EmbedFactory.CreateEmbedBuilder(
                "Emote Detected",
                description,
                DiscordColor.Blurple,
                avatarUrl,
                state.EmotedBack ? "Interaction Complete" : "React with 🔙 to emote back"
            );

            if (updateExisting && state.MessageId != 0)
            {
                await _plugin.Discord.EditWebhookMessage(channelId, state.MessageId, embedBuilder.Build());
            }
            else
            {
                state.MessageId = await _plugin.Discord.SendWebhookMessage(channelId, embedBuilder.Build(), name, world);

                if (state.MessageId != 0)
                {
                    _messageIdCache[state.MessageId] = state;

                    if (_messageIdCache.Count > 100)
                    {
                        var oldest = _messageIdCache.Keys.OrderBy(x => x).Take(10);
                        foreach (var cacheKey in oldest) _messageIdCache.TryRemove(cacheKey, out _);
                    }

                    await _plugin.Discord.AddReaction(channelId, state.MessageId, DiscordEmoji.FromUnicode("🔙"));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to process Discord emote log.");
        }
    }

    public async Task OnDiscordReactionAdded(MessageReactionAddEventArgs e)
    {
        if (e.User.IsBot) return;
        if (e.Emoji.Name != "🔙") return;

        if (!_messageIdCache.TryGetValue(e.Message.Id, out var state))
        {
            state = _activeDiscordEmotes.Values.FirstOrDefault(x => x.MessageId == e.Message.Id);
        }

        if (state == null) return;

        if (state.EmotedBack) return;

        state.EmotedBack = true;

        var cmd = state.Command;
        if (string.IsNullOrEmpty(cmd)) cmd = "/" + state.EmoteName.ToLower().Replace(" ", "");

        await _emoteBackAction.PerformAsync(state.User, state.World, cmd, state.GameObjectId);

        if (ulong.TryParse(_plugin.Config.EmoteLog.ChannelId, out var channelId))
        {
            try
            {
                var avatarUrl = await _plugin.Lodestone.GetAvatarUrlAsync(state.User, state.World);
                string description = $"**{state.User}@{state.World}** used **{state.EmoteName}** on you!";
                if (state.Count > 1) description += $" (x{state.Count})";
                description += "\n\n✅ *You emoted back!*";

                var embedBuilder = _plugin.EmbedFactory.CreateEmbedBuilder(
                    "Emote Detected",
                    description,
                    DiscordColor.Green,
                    avatarUrl,
                    "Interaction Complete"
                );

                await _plugin.Discord.EditWebhookMessage(channelId, state.MessageId, embedBuilder.Build());
                await _plugin.Discord.RemoveReaction(channelId, state.MessageId, DiscordEmoji.FromUnicode("🔙"));
            }
            catch (Exception ex) { _logger.Error(ex, "Failed to update embed after reaction."); }
        }
    }
}
