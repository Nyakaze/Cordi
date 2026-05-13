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
    public DiscordMessageRouter MessageRouter => _messageRouter;

    private CordiLogService Log => _plugin.LogService;
    private const string LogSource = "Discord";

    public DiscordHandler(CordiPlugin plugin, DiscordWebhookService webhooks, AdvertisementFilterService adFilter)
    {
        _plugin = plugin;
        _webhooks = webhooks;
        _adFilter = adFilter;
        _messageRouter = new DiscordMessageRouter(plugin);
        _processedMessages = new Cordi.Core.Caching.Cache<ulong, DateTime>(
            "discord.processedMessages", plugin.CacheRegistry,
            maxSize: 1000, ttl: TimeSpan.FromMinutes(10));

        _intent = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents | DiscordIntents.Guilds | DiscordIntents.GuildWebhooks | DiscordIntents.GuildMessageReactions | DiscordIntents.GuildMembers | DiscordIntents.GuildPresences;
    }

    private readonly Cordi.Core.Caching.Cache<ulong, DateTime> _processedMessages;

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

            // Bind Slash Commands
            _plugin.SlashCommandService.Bind(_client);

            _client.Ready += OnReady;
            _client.MessageCreated += MessageCreatedHandler;
            _client.MessageReactionAdded += MessageReactionAddedHandler;
            _client.PresenceUpdated += OnPresenceUpdatedHandler;
            await _client.ConnectAsync();
            await Task.Yield();
            Logger.Info($"Discord handler started");
            Log.Info(LogSource, "Bot connected successfully");
            _plugin.Config.Discord.BotStarted = true;
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to connect to the bot. {e.StackTrace}");
            Log.Error(LogSource, $"Bot connection failed: {e.Message}");
            _plugin.Config.Discord.BotStarted = false;
        }
        finally
        {
            IsBusy = false;
        }
        _plugin.Config.Save();
    }

    public event Func<MessageReactionAddEventArgs, Task> OnReactionAdded;
    public event Func<MessageCreateEventArgs, Task> OnMessageCreated;
    public event Func<DiscordClient, PresenceUpdateEventArgs, Task> OnPresenceUpdated;

    private Task OnPresenceUpdatedHandler(DiscordClient sender, PresenceUpdateEventArgs e)
    {
        return OnPresenceUpdated?.Invoke(sender, e) ?? Task.CompletedTask;
    }

    private async Task OnReady(DiscordClient sender, ReadyEventArgs e)
    {
        Logger.Info("DiscordHandler READY!!");

        // Auto-register slash commands if enabled
        if (_plugin.Config.SlashCommands.Enabled)
        {
            try
            {
                // Populate emote commands from game data on first run
                _plugin.SlashCommandService.PopulateEmoteCommands();

                await _plugin.SlashCommandService.RegisterCommandsAsync();
            }
            catch (Exception ex)
            {
                Log.Error(LogSource, $"Failed to auto-register slash commands: {ex.Message}");
            }
        }
    }

    private Task MessageReactionAddedHandler(DiscordClient sender, MessageReactionAddEventArgs e)
    {
        Logger.Info($"[DiscordHandler] RAW REACTION: {e.Emoji.Name} by {e.User.Username} on Msg {e.Message.Id}");
        if (e.User.IsBot) return Task.CompletedTask;
        OnReactionAdded?.Invoke(e);
        return Task.CompletedTask;
    }

    private async Task MessageCreatedHandler(DiscordClient sender, MessageCreateEventArgs e)
    {
        if (sender != _client) return;
        if (e.Author.IsBot || e.Message.WebhookMessage
            || (sender.CurrentUser != null && e.Author.Id == sender.CurrentUser.Id)) return;

        if (!_processedMessages.TryAdd(e.Message.Id, DateTime.UtcNow))
        {
            Logger.Debug($"[DiscordHandler {_instanceId}] Ignored duplicate message ID: {e.Message.Id}");
            return;
        }

        Logger.Info($"[DiscordHandler {_instanceId}] Processing message {e.Message.Id} from {e.Author.Username} in {e.Channel.Name} ({e.Channel.Id})");
        Log.Debug(LogSource, $"Received from {e.Author.Username} in #{e.Channel.Name}: {e.Message.Content}");

        _plugin.Config.Stats.IncrementTotal();

        if (OnMessageCreated != null)
            await OnMessageCreated.Invoke(e);
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
                Log.Debug(LogSource, $"Sent [{chatType}] {senderName}@{senderWorld}: {sanitizedContent}");

                // Add to buffer with message ID
                _adFilter.AddMessageToBuffer(senderName, senderWorld, sanitizedContent, sentMessageId, channelFilterEnabled);

                _plugin.Config.Stats.IncrementTotal();
                if (chatType != XivChatType.None) _plugin.Config.Stats.IncrementChatType(chatType);
                if (!string.IsNullOrEmpty(correspondentName)) _plugin.Config.Stats.IncrementTell(correspondentName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to send discord message");
                Log.Error(LogSource, $"Failed to send message: {ex.Message}");
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

    public Task<ulong> SendWebhookMessage(ulong channelId, string content, string senderName, string senderWorld)
    {
        if (_client == null) return Task.FromResult(0UL);
        var sanitizedContent = DiscordTextSanitizer.Sanitize(content);
        if (string.IsNullOrWhiteSpace(sanitizedContent)) return Task.FromResult(0UL);

        return QueuedSendAsync($"webhook send (channel {channelId})", "webhook", async () =>
        {
            var channel = await _client.GetChannelAsync(channelId);

            var avatarUrl = await _plugin.Lodestone.GetAvatarUrlAsync(senderName, senderWorld);
            if (!string.IsNullOrEmpty(avatarUrl) && !Uri.IsWellFormedUriString(avatarUrl, UriKind.Absolute)) avatarUrl = null;

            var username = $"{senderName}@{senderWorld}";
            var builder = new DiscordWebhookBuilder()
                .WithUsername(username)
                .WithContent(sanitizedContent);

            if (!string.IsNullOrEmpty(avatarUrl)) builder.WithAvatarUrl(avatarUrl);

            return await _webhooks.ExecuteWebhookAsync(channel, builder);
        });
    }

    public Task<ulong> SendWebhookMessage(ulong channelId, DiscordEmbed embed, string senderName, string senderWorld)
    {
        if (_client == null) return Task.FromResult(0UL);
        return QueuedSendAsync($"webhook embed (channel {channelId})", "webhook", async () =>
        {
            var channel = await _client.GetChannelAsync(channelId);
            var avatarUrl = await _plugin.Lodestone.GetAvatarUrlAsync(senderName, senderWorld);
            var username = $"{senderName}@{senderWorld}";

            var builder = new DiscordWebhookBuilder()
                .WithUsername(username)
                .WithAvatarUrl(avatarUrl)
                .AddEmbed(embed);

            return await _webhooks.ExecuteWebhookAsync(channel, builder);
        });
    }

    public Task<ulong> SendWebhookMessageRaw(ulong channelId, DiscordEmbed embed, string username, string? avatarUrl)
    {
        if (_client == null) return Task.FromResult(0UL);
        return QueuedSendAsync($"webhook embed raw (channel {channelId})", "webhook", async () =>
        {
            var channel = await _client.GetChannelAsync(channelId);

            var builder = new DiscordWebhookBuilder()
                .WithUsername(username)
                .AddEmbed(embed);

            if (!string.IsNullOrEmpty(avatarUrl))
                builder.WithAvatarUrl(avatarUrl);

            return await _webhooks.ExecuteWebhookAsync(channel, builder);
        });
    }

    public Task EditWebhookMessage(ulong channelId, ulong messageId, DiscordEmbed embed)
    {
        if (_client == null) return Task.CompletedTask;
        return QueuedSendAsync($"webhook edit (channel {channelId} msg {messageId})", "webhook", async () =>
        {
            var channel = await _client.GetChannelAsync(channelId);
            await _webhooks.EditWebhookMessageAsync(channel, messageId, new DiscordWebhookBuilder().AddEmbed(embed));
        });
    }

    public Task AddReaction(ulong channelId, ulong messageId, DiscordEmoji emoji)
    {
        if (_client == null) return Task.CompletedTask;
        return QueuedSendAsync($"add reaction {emoji.Name} on {messageId}", "reaction", async () =>
        {
            var channel = await _client.GetChannelAsync(channelId);
            var msg = await channel.GetMessageAsync(messageId);
            await msg.CreateReactionAsync(emoji);
        });
    }

    public Task RemoveReaction(ulong channelId, ulong messageId, DiscordEmoji emoji)
    {
        if (_client == null) return Task.CompletedTask;
        return QueuedSendAsync($"remove reaction {emoji.Name} on {messageId}", "reaction", async () =>
        {
            var channel = await _client.GetChannelAsync(channelId);
            var msg = await channel.GetMessageAsync(messageId);
            await msg.DeleteOwnReactionAsync(emoji);
        });
    }

    private async Task<ulong> QueuedSendAsync(string description, string category, Func<Task<ulong>> action)
    {
        try
        {
            return await _plugin.DiscordSendQueue.RunAsync(description, category, action);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"[DiscordHandler] {description} ultimately failed");
            return 0;
        }
    }

    private async Task QueuedSendAsync(string description, string category, Func<Task> action)
    {
        try
        {
            await _plugin.DiscordSendQueue.RunAsync(description, category, action);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"[DiscordHandler] {description} ultimately failed");
        }
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
        Log.Info(LogSource, "Bot disconnecting...");
        Logger.Info($"[{_instanceId}] Disconnecting Discord client...");

        _plugin.ChannelCache.Unbind();
        _plugin.SlashCommandService.Unbind();

        await _client.DisconnectAsync();
        _client.MessageCreated -= MessageCreatedHandler;
        _client.Ready -= OnReady;
        _client.Dispose();
        _client = null;
        _webhooks.ClearCache();
        _processedMessages.Clear();
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

