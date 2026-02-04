using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using NetStone;
using NetStone.Search.Character;
using NetStone.Model.Parseables.Search.Character;

using Cordi.Core;
using Cordi.Configuration;

namespace Cordi.Services.Discord;



public class DiscordHandler : IDisposable
{
    static readonly IPluginLog Logger = Service.Log;
    private readonly CordiPlugin _plugin;

    private DiscordClient _client;
    private DiscordIntents _intent;
    public DiscordClient Client => _client;



    private readonly DiscordWebhookService _webhooks;

    public DiscordHandler(CordiPlugin plugin, DiscordWebhookService webhooks)
    {
        _plugin = plugin;
        _webhooks = webhooks;

        _intent = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents | DiscordIntents.Guilds | DiscordIntents.GuildWebhooks | DiscordIntents.GuildMessageReactions | DiscordIntents.GuildMembers | DiscordIntents.GuildPresences;
    }

    public async Task Start()
    {
        if (string.IsNullOrEmpty(_plugin.Config.Discord.BotToken))

        {

            Logger.Error("Token empty, cannot start bot.");
            _plugin.Config.Discord.BotStarted = false;
            _plugin.Config.Save();
            return;
        }
        try
        {
            if (_plugin.Config.Discord.BotStarted)
            {
                Logger.Info("Bot already started... Trying to stop and restart.");
                await Stop();
            }
            _client = new DiscordClient(new DiscordConfiguration
            {
                Token = this._plugin.Config.Discord.BotToken,
                TokenType = TokenType.Bot,
                Intents = _intent,
                MinimumLogLevel = LogLevel.Debug,
            });
            _client.Ready += OnReady;
            _client.MessageCreated += MessageCreatedHandler;
            _client.MessageReactionAdded += MessageReactionAddedHandler;
            _client.PresenceUpdated += OnPresenceUpdatedHandler;
            await _client.ConnectAsync();
            await Task.Yield();
            Logger.Info($"Discord handler started");
            _plugin.Config.Discord.BotStarted = true;
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to connect to the bot. {e.StackTrace}");
            _plugin.Config.Discord.BotStarted = false;
        }
        _plugin.Config.Save();
    }

    public event Func<MessageReactionAddEventArgs, Task> OnReactionAdded;
    public event Func<DiscordClient, PresenceUpdateEventArgs, Task> OnPresenceUpdated;

    private Task OnPresenceUpdatedHandler(DiscordClient sender, PresenceUpdateEventArgs e)
    {
        return OnPresenceUpdated?.Invoke(sender, e) ?? Task.CompletedTask;
    }

    private Task OnReady(DiscordClient sender, ReadyEventArgs e)
    {

        Logger.Info("DiscordHandler READY!!");
        return Task.CompletedTask;
    }

    private Task MessageReactionAddedHandler(DiscordClient sender, MessageReactionAddEventArgs e)
    {
        Logger.Info($"[DiscordHandler] RAW REACTION: {e.Emoji.Name} by {e.User.Username} on Msg {e.Message.Id}");
        if (e.User.IsBot) return Task.CompletedTask;
        OnReactionAdded?.Invoke(e);
        return Task.CompletedTask;
    }

    async Task MessageCreatedHandler(DiscordClient sender, MessageCreateEventArgs message)
    {
        if (message.Author.IsBot || message.Message.WebhookMessage) return;

        _plugin.Config.Stats.IncrementTotal();


        foreach (var mapping in _plugin.Config.Chat.Mappings)
        {
            if (mapping.DiscordChannelId == message.Channel.Id.ToString())
            {
                _ = _plugin._chat.SendAsync(mapping.GameChatType, message.Message.Content);
                Logger.Info($"Forwarding message: {message.Message.Content} to {mapping.GameChatType}");
            }
        }


        var tellTarget = _plugin.Config.Chat.TellThreadMappings.FirstOrDefault(x => x.Value == message.Channel.Id.ToString()).Key;
        if (!string.IsNullOrEmpty(tellTarget))
        {
            _ = _plugin._chat.SendTellAsync(tellTarget, message.Message.Content);
            Logger.Info($"Forwarding Tell reply: {message.Message.Content} to {tellTarget}");
        }


        bool isMapped = _plugin.Config.Chat.Mappings.Any(m => m.DiscordChannelId == message.Channel.Id.ToString()) ||
                        _plugin.Config.Chat.TellThreadMappings.ContainsValue(message.Channel.Id.ToString());

        if (isMapped)
        {
            try
            {

                await message.Message.DeleteAsync();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to delete original message: {ex.Message}");
            }
        }


        if (message.Message.Content == "STOP BOT") await Stop();

        if (message.Message.Content.StartsWith("Create Channel: "))
            await message.Guild.CreateChannelAsync(message.Message.Content["Create Channel: ".Length..], ChannelType.Text, parent: message.Channel.Parent);

        if (message.Message.Content.StartsWith("Create ForumChannel: "))
        {
            ulong forumChannelID = message.Channel.Parent.Id;
            var forumChannel = (DiscordForumChannel)message.Guild.GetChannel(forumChannelID);
            var forumPostBuilder = new ForumPostBuilder
            {
                Name = message.Message.Content["Create ForumChannel: ".Length..],
                Message = new DiscordMessageBuilder().WithContent(" ")
            };
            await forumChannel.CreateForumPostAsync(forumPostBuilder);
        }
    }

    public async Task SendMessage(DiscordChannel channel, Dalamud.Game.Text.SeStringHandling.SeString message, string senderName, string senderWorld, XivChatType chatType = XivChatType.None, string? correspondentName = null)
    {

        string? avatarUrl = null;
        await SendMessage(channel, message.TextValue, senderName, senderWorld, chatType, correspondentName, avatarUrl);
    }

    public async Task SendMessage(DiscordChannel channel, string content, string senderName, string senderWorld, XivChatType chatType = XivChatType.None, string? correspondentName = null, string? avatarUrl = null)
    {
        if (_client == null) return;

        string targetChannelId = _plugin.Config.Discord.DefaultChannelId;


        if (_plugin.Config.MappingCache.TryGetValue(chatType, out var mappedId))
        {
            targetChannelId = mappedId;
        }

        if (string.IsNullOrEmpty(targetChannelId))
        {

            return;
        }

        if (ulong.TryParse(targetChannelId, out ulong id))
        {
            try
            {
                channel = await _client.GetChannelAsync(id);
                DiscordChannel webhookChannel = channel;

                if (channel.Type == ChannelType.GuildForum && !string.IsNullOrEmpty(correspondentName))
                {
                    DiscordThreadChannel thread = null;

                    if (_plugin.Config.Chat.TellThreadMappings.TryGetValue(correspondentName, out var threadIdStr) && ulong.TryParse(threadIdStr, out var threadId))
                    {
                        try { thread = await _client.GetChannelAsync(threadId) as DiscordThreadChannel; } catch { }
                    }
                    if (thread == null)
                    {
                        var forum = channel as DiscordForumChannel;
                        var post = await forum.CreateForumPostAsync(new ForumPostBuilder
                        {
                            Name = correspondentName,
                            Message = new DiscordMessageBuilder().WithContent($"Started conversation with {correspondentName}")
                        });
                        thread = (DiscordThreadChannel)post.Channel;
                        _plugin.Config.Chat.TellThreadMappings[correspondentName] = thread.Id.ToString();
                        _plugin.Config.Save();
                        _plugin.NotificationManager.Add("New Conversation!", $"Created Channel for: {correspondentName}", CordiNotificationType.Success);


                        try
                        {
                            var fetchedThread = await _client.GetChannelAsync(thread.Id);
                            if (fetchedThread is DiscordThreadChannel ft) thread = ft;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, $"Failed to re-fetch thread {thread.Id}. Using original object.");
                        }
                    }
                    Logger.Info($"[DiscordHandler] Sending to thread: {thread.Name} (ID: {thread.Id}, ParentID: {thread.ParentId})");
                    channel = thread;
                }

                var finalAvatarUrl = avatarUrl ?? await _plugin.Avatar.GetAvatarUrlAsync(senderName, senderWorld);
                var hookMessage = new DiscordWebhookBuilder()
                    .WithContent(content)
                    .WithUsername($"{senderName}@{senderWorld}")
                    .WithAvatarUrl(finalAvatarUrl);

                Logger.Info($"[DiscordHandler] Executing webhook for channel {channel.Id}...");
                await _webhooks.ExecuteWebhookAsync(channel, hookMessage);
                Logger.Info($"{chatType} | Sent via webhook: {content}");

                _plugin.Config.Stats.IncrementTotal();
                if (chatType != XivChatType.None) _plugin.Config.Stats.IncrementChatType(chatType);
                if (!string.IsNullOrEmpty(correspondentName)) _plugin.Config.Stats.IncrementTell(correspondentName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to send discord message");
            }
        }
        else
        {
            Logger.Error($"Invalid Channel ID: {targetChannelId}");
        }
    }

    public void SendMessageToChannel(ulong channelId, string content)
    {
        if (_client == null) return;
        Task.Run(async () =>
        {
            try
            {
                var channel = await _client.GetChannelAsync(channelId);
                await channel.SendMessageAsync(content);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to send message to channel {channelId}");
            }
        });
    }

    public async Task<ulong> SendWebhookMessage(ulong channelId, DiscordEmbed embed, string senderName, string senderWorld)
    {
        if (_client == null) return 0;
        try
        {
            var channel = await _client.GetChannelAsync(channelId);
            var avatarUrl = await _plugin.Avatar.GetAvatarUrlAsync(senderName, senderWorld);
            var username = $"{senderName}@{senderWorld}";

            var builder = new DiscordWebhookBuilder()
                .WithUsername(username)
                .WithAvatarUrl(avatarUrl)
                .AddEmbed(embed);

            return await _webhooks.ExecuteWebhookAsync(channel, builder);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to send webhook embed.");
            return 0;
        }
    }

    public async Task EditWebhookMessage(ulong channelId, ulong messageId, DiscordEmbed embed)
    {
        if (_client == null) return;
        try
        {
            var channel = await _client.GetChannelAsync(channelId);
            await _webhooks.EditWebhookMessageAsync(channel, messageId, new DiscordWebhookBuilder().AddEmbed(embed));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to edit webhook message.");
        }
    }

    public async Task AddReaction(ulong channelId, ulong messageId, DiscordEmoji emoji)
    {
        if (_client == null) return;
        try
        {
            var channel = await _client.GetChannelAsync(channelId);
            var msg = await channel.GetMessageAsync(messageId);
            await msg.CreateReactionAsync(emoji);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to add reaction.");
        }
    }

    public async Task RemoveReaction(ulong channelId, ulong messageId, DiscordEmoji emoji)
    {
        if (_client == null) return;
        try
        {
            var channel = await _client.GetChannelAsync(channelId);
            var msg = await channel.GetMessageAsync(messageId);

            await msg.DeleteOwnReactionAsync(emoji);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to remove reaction.");
        }
    }

    public async Task Stop()
    {
        if (_client == null) return;
        Logger.Info("Disconnecting Discord client...");
        await _client.DisconnectAsync();
        _client.MessageCreated -= MessageCreatedHandler;
        _client.Ready -= OnReady;
        _client.Dispose();
        _client = null;
        _webhooks.ClearCache();
        Logger.Info("Discord client disconnected.");
        _plugin.Config.Discord.BotStarted = false;
        _plugin.Config.Save();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {

            Logger.Verbose("Discord DISPOSE!!");
        }
    }
}

