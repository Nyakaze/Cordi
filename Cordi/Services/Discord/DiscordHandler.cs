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
    private readonly Guid _instanceId = Guid.NewGuid();

    private DiscordClient _client;
    private DiscordIntents _intent;
    public DiscordClient Client => _client;

    public bool IsBusy { get; private set; }

    private readonly DiscordWebhookService _webhooks;
    private readonly AdvertisementFilterService _adFilter;
    private readonly DiscordMessageRouter _messageRouter;
    private readonly DiscordCommandHandler _commandHandler;

    public DiscordHandler(CordiPlugin plugin, DiscordWebhookService webhooks, AdvertisementFilterService adFilter)
    {
        _plugin = plugin;
        _webhooks = webhooks;
        _adFilter = adFilter;
        _messageRouter = new DiscordMessageRouter(plugin);
        _commandHandler = new DiscordCommandHandler(plugin);

        _intent = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents | DiscordIntents.Guilds | DiscordIntents.GuildWebhooks | DiscordIntents.GuildMessageReactions | DiscordIntents.GuildMembers | DiscordIntents.GuildPresences;
    }

    private readonly ConcurrentDictionary<string, List<(string Content, DateTime Timestamp, ulong MessageId)>> _messageBuffer = new();
    private readonly ConcurrentDictionary<string, DateTime> _penaltyBox = new();
    private readonly ConcurrentDictionary<ulong, DateTime> _processedMessages = new();

    public async Task Start()
    {
        if (string.IsNullOrEmpty(_plugin.Config.Discord.BotToken))

        {

            Logger.Error("Token empty, cannot start bot.");
            _plugin.Config.Discord.BotStarted = false;
            _plugin.Config.Save();
            return;
        }

        if (IsBusy) return;
        IsBusy = true;

        try
        {
            if (_plugin.Config.Discord.BotStarted)
            {
                Logger.Info("Bot already started... Trying to stop and restart.");
                await StopInternal();
            }
            _client = new DiscordClient(new DiscordConfiguration
            {
                Token = this._plugin.Config.Discord.BotToken,
                TokenType = TokenType.Bot,
                Intents = _intent,
                MinimumLogLevel = LogLevel.Debug,
            });

            // Bind Cache
            _plugin.ChannelCache.Bind(_client);

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
        finally
        {
            IsBusy = false;
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
        if (sender != _client) return;
        if (message.Author.IsBot || message.Message.WebhookMessage || (sender.CurrentUser != null && message.Author.Id == sender.CurrentUser.Id)) return;

        // Deduplication
        if (!_processedMessages.TryAdd(message.Message.Id, DateTime.UtcNow))
        {
            Logger.Debug($"[DiscordHandler {_instanceId}] Ignored duplicate message ID: {message.Message.Id}");
            return;
        }

        Logger.Info($"[DiscordHandler {_instanceId}] Processing message {message.Message.Id} from {message.Author.Username} in {message.Channel.Name} ({message.Channel.Id})");

        // Simple periodic cleanup of deduplication cache
        if (_processedMessages.Count > 1000)
        {
            var old = DateTime.UtcNow.AddMinutes(-10);
            foreach (var key in _processedMessages.Keys.ToList())
            {
                if (_processedMessages.TryGetValue(key, out var ts) && ts < old)
                    _processedMessages.TryRemove(key, out _);
            }
        }

        _plugin.Config.Stats.IncrementTotal();

        _ = _commandHandler.ProcessDiscordCommand(message.Message.Content, message.Channel.Id);

        bool handled = await _messageRouter.RouteExtraChatMessage(message.Message, message.Channel.Id);

        if (!handled)
        {
            handled = await _messageRouter.RouteStandardMessage(message.Message, message.Channel.Id);

            if (!handled)
            {
                handled = await _messageRouter.RouteTellMessage(message.Message, message.Channel.Id);
            }

            if (handled)
            {
                try { await message.Message.DeleteAsync(); } catch { }
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

        if (channel == null)
        {
            string targetChannelId = "";

            if (chatType != XivChatType.Debug)
            {
                targetChannelId = _plugin.Config.Discord.DefaultChannelId;
            }

            if (_plugin.Config.MappingCache.TryGetValue(chatType, out var mappedId))
            {
                targetChannelId = mappedId;
            }

            if (string.IsNullOrEmpty(targetChannelId)) return;

            if (ulong.TryParse(targetChannelId, out ulong id))
            {
                try
                {
                    channel = await _client.GetChannelAsync(id);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to get channel {id}");
                    return;
                }
            }
        }

        if (channel != null)
        {
            try
            {
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

                var finalAvatarUrl = avatarUrl ?? await _plugin.Lodestone.GetAvatarUrlAsync(senderName, senderWorld);

                var sanitizedContent = DiscordTextSanitizer.Sanitize(content);

                // Validation to prevent 400 Bad Request
                if (string.IsNullOrWhiteSpace(sanitizedContent))
                {
                    Logger.Warning($"[DiscordHandler] Sanitized content is empty. Original: '{content}'. Skipping webhook execution.");
                    return;
                }

                if (!string.IsNullOrEmpty(finalAvatarUrl) && !Uri.IsWellFormedUriString(finalAvatarUrl, UriKind.Absolute))
                {
                    Logger.Warning($"[DiscordHandler] Invalid Avatar URL: '{finalAvatarUrl}'. clear url to prevent error.");
                    finalAvatarUrl = null;
                }

                bool channelFilterEnabled = true;
                var mapping = _plugin.Config.Chat.Mappings.FirstOrDefault(m => m.GameChatType == chatType);
                if (mapping != null)
                {
                    channelFilterEnabled = mapping.EnableAdvertisementFilter;
                }

                // Check if message is a club advertisement
                if (_adFilter.IsAdvertisementOrPenalized(senderName, senderWorld, sanitizedContent, channelFilterEnabled, channel))
                {
                    return;
                }

                var hookMessage = new DiscordWebhookBuilder()
                    .WithContent(sanitizedContent)
                    .WithUsername($"{senderName}@{senderWorld}");

                if (!string.IsNullOrEmpty(finalAvatarUrl))
                {
                    hookMessage.WithAvatarUrl(finalAvatarUrl);
                }

                ulong sentMessageId = await _webhooks.ExecuteWebhookAsync(channel, hookMessage);
                Logger.Info($"{chatType} | Sent via webhook: {sanitizedContent} (ID: {sentMessageId})");

                // Add to buffer with message ID
                _adFilter.AddMessageToBuffer(senderName, senderWorld, sanitizedContent, sentMessageId, channelFilterEnabled);

                _plugin.Config.Stats.IncrementTotal();
                if (chatType != XivChatType.None) _plugin.Config.Stats.IncrementChatType(chatType);
                if (!string.IsNullOrEmpty(correspondentName)) _plugin.Config.Stats.IncrementTell(correspondentName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to send discord message");
            }
        }
        if (channel == null)
        {
            // If we reached here, targetChannelId might not even be defined in this scope if the logic above failed
            // But actually, we only need to log if channel is null
            Logger.Error("Invalid Channel (Null) or ID Resolution Failed");
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

    public async Task<ulong> SendWebhookMessage(ulong channelId, string content, string senderName, string senderWorld)
    {
        if (_client == null) return 0;
        try
        {
            var channel = await _client.GetChannelAsync(channelId);

            var sanitizedContent = DiscordTextSanitizer.Sanitize(content);
            if (string.IsNullOrWhiteSpace(sanitizedContent)) return 0;

            var avatarUrl = await _plugin.Lodestone.GetAvatarUrlAsync(senderName, senderWorld);
            if (!string.IsNullOrEmpty(avatarUrl) && !Uri.IsWellFormedUriString(avatarUrl, UriKind.Absolute)) avatarUrl = null;

            var username = $"{senderName}@{senderWorld}";

            var builder = new DiscordWebhookBuilder()
                .WithUsername(username)
                .WithContent(sanitizedContent);

            if (!string.IsNullOrEmpty(avatarUrl)) builder.WithAvatarUrl(avatarUrl);

            return await _webhooks.ExecuteWebhookAsync(channel, builder);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to send webhook message.");
            return 0;
        }
    }

    public async Task<ulong> SendWebhookMessage(ulong channelId, DiscordEmbed embed, string senderName, string senderWorld)
    {
        if (_client == null) return 0;
        try
        {
            var channel = await _client.GetChannelAsync(channelId);
            var avatarUrl = await _plugin.Lodestone.GetAvatarUrlAsync(senderName, senderWorld);
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

    public async Task<ulong> SendWebhookMessageRaw(ulong channelId, DiscordEmbed embed, string username, string? avatarUrl)
    {
        if (_client == null) return 0;
        try
        {
            var channel = await _client.GetChannelAsync(channelId);

            var builder = new DiscordWebhookBuilder()
                .WithUsername(username)
                .AddEmbed(embed);

            if (!string.IsNullOrEmpty(avatarUrl))
                builder.WithAvatarUrl(avatarUrl);

            return await _webhooks.ExecuteWebhookAsync(channel, builder);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to send raw webhook embed.");
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
        await Helpers.RetryHelper.WithRetryAsync(async () =>
        {
            var channel = await _client.GetChannelAsync(channelId);
            var msg = await channel.GetMessageAsync(messageId);
            await msg.CreateReactionAsync(emoji);
        });
    }

    public async Task RemoveReaction(ulong channelId, ulong messageId, DiscordEmoji emoji)
    {
        if (_client == null) return;
        await Helpers.RetryHelper.WithRetryAsync(async () =>
        {
            var channel = await _client.GetChannelAsync(channelId);
            var msg = await channel.GetMessageAsync(messageId);
            await msg.DeleteOwnReactionAsync(emoji);
        });
    }

    public async Task Stop()
    {
        // Don't return if busy, we must stop!
        // But we want to avoid re-entry of Stop itself.
        // We generally shouldn't call Stop concurrently.
        // If we are "Busy" (Starting), we might interrupt it.

        IsBusy = true; // Mark busy to prevent new Starts
        try
        {
            await StopInternal();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StopInternal()
    {
        if (_client == null) return;
        Logger.Info($"[{_instanceId}] Disconnecting Discord client...");

        _plugin.ChannelCache.Unbind();

        await _client.DisconnectAsync();
        _client.MessageCreated -= MessageCreatedHandler;
        _client.Ready -= OnReady;
        _client.Dispose();
        _client = null;
        _webhooks.ClearCache();
        _messageBuffer.Clear();
        _penaltyBox.Clear();
        _processedMessages.Clear();
        Logger.Info("Discord client disconnected.");
        _plugin.Config.Discord.BotStarted = false;
        _plugin.Config.Save();
    }

    public async Task ProcessDiscordCommand(string content, ulong channelId)
    {
        await _commandHandler.ProcessDiscordCommand(content, channelId);
    }

    private async Task HandleTargetCommand(string name, string world, ulong channelId)
    {
        await _commandHandler.ProcessDiscordCommand($"{_plugin.Config.Discord.CommandPrefix}target {name} {world}", channelId);
    }

    private async Task HandleEmoteCommand(string emoteName, string name, string world, ulong channelId)
    {
        await _commandHandler.ProcessDiscordCommand($"{_plugin.Config.Discord.CommandPrefix}emote {emoteName} {name} {world}", channelId);
    }

    private (string? name, string? world) ParsePlayerSpec(string spec)
        => _commandHandler.ParsePlayerSpec(spec);

    private async Task SendCommandFeedback(ulong channelId, string message)
    {
        await _commandHandler.ProcessDiscordCommand($"feedback:{message}", channelId);
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

