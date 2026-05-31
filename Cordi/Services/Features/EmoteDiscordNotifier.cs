using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Core.Caching;
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

    private readonly Cache<ulong, EmoteLogService.DiscordEmoteState> _messageIdCache;
    private readonly ConcurrentDictionary<string, EmoteLogService.DiscordEmoteState> _activeDiscordEmotes;
    private readonly TimeSpan _spamThreshold;

    public EmoteDiscordNotifier(
        CordiPlugin plugin,
        Cache<ulong, EmoteLogService.DiscordEmoteState> messageIdCache,
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
            var lodestoneId = await _plugin.Lodestone.ResolveLodestoneIdAsync(name, world);
            var embed = BuildEmoteEmbed(state, lodestoneId, avatarUrl);

            if (updateExisting && state.MessageId != 0)
            {
                await _plugin.Discord.EditWebhookMessage(channelId, state.MessageId, embed);
            }
            else
            {
                state.MessageId = await _plugin.Discord.SendWebhookMessage(channelId, embed, name, world);

                if (state.MessageId != 0)
                {
                    _messageIdCache.Set(state.MessageId, state);
                    await _plugin.Discord.AddReaction(channelId, state.MessageId, DiscordEmoji.FromUnicode("🔙"));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to process Discord emote log.");
        }
    }

    private DiscordEmbed BuildEmoteEmbed(EmoteLogService.DiscordEmoteState state, string? lodestoneId, string avatarUrl)
    {
        var nameLink = !string.IsNullOrEmpty(lodestoneId)
            ? $"[{state.User}@{state.World}](https://na.finalfantasyxiv.com/lodestone/character/{lodestoneId}/)"
            : $"{state.User}@{state.World}";

        var color = state.EmotedBack ? new DiscordColor(0x2ECC71) : new DiscordColor(0x5865F2);
        var title = "Emote Detected";
        var description = $"**{nameLink}** used **{state.EmoteName}** on you!";
        var footerText = state.EmotedBack ? "Interaction Complete" : "React with 🔙 to emote back";

        var embedBuilder = _plugin.EmbedFactory.CreateEmbedBuilder(
            title,
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

    public async Task OnDiscordReactionAdded(MessageReactionAddEventArgs e)
    {
        if (e.User.IsBot) return;
        if (e.Emoji.Name != "🔙") return;

        if (!_messageIdCache.TryGet(e.Message.Id, out var state))
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
                var lodestoneId = await _plugin.Lodestone.ResolveLodestoneIdAsync(state.User, state.World);
                var embed = BuildEmoteEmbed(state, lodestoneId, avatarUrl);

                await _plugin.Discord.EditWebhookMessage(channelId, state.MessageId, embed);
                await _plugin.Discord.RemoveReaction(channelId, state.MessageId, DiscordEmoji.FromUnicode("🔙"));
            }
            catch (Exception ex) { _logger.Error(ex, "Failed to update embed after reaction."); }
        }
    }
}
