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

    public DiscordHandler(CordiPlugin plugin, DiscordWebhookService webhooks)
    {
        _plugin = plugin;
        _webhooks = webhooks;

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

        _ = ProcessDiscordCommand(message.Message.Content, message.Channel.Id);

        var extraChatMapping = _plugin.Config.Chat.ExtraChatMappings.FirstOrDefault(x => x.Value.DiscordChannelId == message.Channel.Id.ToString());
        bool handled = false;

        if (!string.IsNullOrEmpty(extraChatMapping.Key))
        {
            var label = extraChatMapping.Key;
            var connection = extraChatMapping.Value;

            if (connection.ExtraChatNumber > 0)
            {
                string contentToSend = message.Message.Content;

                try
                {
                    string command = $"/ecl{connection.ExtraChatNumber} {contentToSend}";
                    Logger.Info($"[DiscordHandler] Routing to ExtraChat (Key: {label}, Channel: {connection.ExtraChatNumber}) for Msg {message.Message.Id}: {command}");

                    await Service.Framework.RunOnFrameworkThread(() =>
                    {
                        _plugin._chat.SendMessage(command);
                    });

                    handled = true;
                    try { await message.Message.DeleteAsync(); } catch { }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to send ExtraChat command for {label}");
                }
            }
            else
            {
                Logger.Warning($"[DiscordHandler] ExtraChat mapping found for {label} but 'Channel #' is not configured (0). Msg not sent.");
            }
        }

        if (!handled)
        {
            // Standard Mappings
            var mapping = _plugin.Config.Chat.Mappings.FirstOrDefault(m => m.DiscordChannelId == message.Channel.Id.ToString());
            if (mapping != null)
            {
                _ = _plugin._chat.SendAsync(mapping.GameChatType, message.Message.Content);
                Logger.Info($"Forwarding message: {message.Message.Content} to {mapping.GameChatType}");
                handled = true;
            }

            if (!handled)
            {
                // Tell Thread Mappings
                var tellTarget = _plugin.Config.Chat.TellThreadMappings.FirstOrDefault(x => x.Value == message.Channel.Id.ToString()).Key;
                if (!string.IsNullOrEmpty(tellTarget))
                {
                    _ = _plugin._chat.SendTellAsync(tellTarget, message.Message.Content);
                    Logger.Info($"Forwarding Tell reply: {message.Message.Content} to {tellTarget}");
                    handled = true;
                }
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
                if (_plugin.Config.AdvertisementFilter.Enabled && channelFilterEnabled)
                {
                    string senderKey = $"{senderName}@{senderWorld}";

                    // Check Penalty Box
                    if (_penaltyBox.TryGetValue(senderKey, out var releaseTime))
                    {
                        if (DateTime.UtcNow < releaseTime)
                        {
                            Logger.Info($"[AdvertisementFilter] Blocked message from penalized user {senderKey} until {releaseTime}");
                            return;
                        }
                        else
                        {
                            _penaltyBox.TryRemove(senderKey, out _);
                        }
                    }

                    // Buffer logic for split messages
                    var cleanupTime = DateTime.UtcNow.AddSeconds(-5); // 5 second buffer window

                    var userMessages = _messageBuffer.GetOrAdd(senderKey, _ => new List<(string, DateTime, ulong)>());

                    bool isAd = false;
                    string combinedMessage = "";

                    lock (userMessages)
                    {
                        // Clean up old messages
                        userMessages.RemoveAll(x => x.Timestamp < cleanupTime);

                        // Construct combined message to check
                        var recentContent = userMessages.Select(x => x.Content).ToList();
                        recentContent.Add(sanitizedContent);
                        combinedMessage = string.Join(" ", recentContent);
                    }

                    // Check if the individual message OR the combined message is an ad
                    // Create local function to avoid repeating the same 6 config parameters
                    var filterConfig = _plugin.Config.AdvertisementFilter;
                    bool checkAd(string content) => AdvertisementFilter.IsAdvertisement(
                        content,
                        filterConfig.ScoreThreshold,
                        filterConfig.HighScoreRegexPatterns,
                        filterConfig.HighScoreKeywords,
                        filterConfig.MediumScoreRegexPatterns,
                        filterConfig.MediumScoreKeywords,
                        filterConfig.Whitelist);

                    // Check individual first (optimization) - if single message is an ad, skip combined check
                    isAd = checkAd(sanitizedContent);

                    if (!isAd && combinedMessage != sanitizedContent)
                    {
                        // Only check combined if it's different and individual wasn't an ad
                        isAd = checkAd(combinedMessage);
                    }

                    if (isAd)
                    {
                        Logger.Info($"[AdvertisementFilter] Blocked advertisement: {sanitizedContent.Substring(0, Math.Min(100, sanitizedContent.Length))}...");

                        // Add to Penalty Box (10 seconds)
                        _penaltyBox.AddOrUpdate(senderKey, DateTime.UtcNow.AddSeconds(10), (key, oldValue) => DateTime.UtcNow.AddSeconds(10));

                        // Retroactive Deletion: Delete previous messages that were part of this detected ad
                        lock (userMessages)
                        {
                            foreach (var (_, _, msgId) in userMessages)
                            {
                                if (msgId != 0)
                                {
                                    _ = _webhooks.DeleteWebhookMessageAsync(channel, msgId);
                                    Logger.Info($"[AdvertisementFilter] Retroactively deleted message ID: {msgId}");
                                }
                            }
                            userMessages.Clear();
                        }
                        return;
                    }
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
                if (_plugin.Config.AdvertisementFilter.Enabled && channelFilterEnabled)
                {
                    string senderKey = $"{senderName}@{senderWorld}";
                    var userMessages = _messageBuffer.GetOrAdd(senderKey, _ => new List<(string, DateTime, ulong)>());
                    lock (userMessages)
                    {
                        userMessages.Add((sanitizedContent, DateTime.UtcNow, sentMessageId));
                    }
                }

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
        int retryCount = 0;
        const int maxRetries = 3;

        while (true)
        {
            try
            {
                var channel = await _client.GetChannelAsync(channelId);
                var msg = await channel.GetMessageAsync(messageId);
                await msg.CreateReactionAsync(emoji);
                break;
            }
            catch (Exception ex)
            {
                if (retryCount++ >= maxRetries)
                {
                    Logger.Error(ex, "Failed to add reaction after retries.");
                    break;
                }

                // Check if it's a network/socket error worth retrying
                if (ex is System.Net.Http.HttpRequestException ||
                    ex is System.Net.Sockets.SocketException ||
                    ex is System.IO.IOException)
                {
                    Logger.Warning($"AddReaction failed (Retry {retryCount}/{maxRetries}): {ex.Message}");
                    await Task.Delay(1000 * retryCount);
                }
                else
                {
                    Logger.Error(ex, "Failed to add reaction (Non-transient error).");
                    break;
                }
            }
        }
    }

    public async Task RemoveReaction(ulong channelId, ulong messageId, DiscordEmoji emoji)
    {
        if (_client == null) return;
        int retryCount = 0;
        const int maxRetries = 3;

        while (true)
        {
            try
            {
                var channel = await _client.GetChannelAsync(channelId);
                var msg = await channel.GetMessageAsync(messageId);
                await msg.DeleteOwnReactionAsync(emoji);
                break;
            }
            catch (Exception ex)
            {
                if (retryCount++ >= maxRetries)
                {
                    Logger.Error(ex, "Failed to remove reaction after retries.");
                    break;
                }

                if (ex is System.Net.Http.HttpRequestException ||
                    ex is System.Net.Sockets.SocketException ||
                    ex is System.IO.IOException)
                {
                    Logger.Warning($"RemoveReaction failed (Retry {retryCount}/{maxRetries}): {ex.Message}");
                    await Task.Delay(1000 * retryCount);
                }
                else
                {
                    Logger.Error(ex, "Failed to remove reaction (Non-transient error).");
                    break;
                }
            }
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
        if (!_plugin.Config.Discord.AllowDiscordCommands) return;
        if (!content.StartsWith(_plugin.Config.Discord.CommandPrefix)) return;
        Logger.Info($"Processing Discord command: {content}");

        var parts = content.Substring(_plugin.Config.Discord.CommandPrefix.Length).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Logger.Info($"Parts: {string.Join(", ", parts)}");
        if (parts.Length == 0) return;

        var command = parts[0].ToLower();
        string fullName = string.Empty;

        try
        {
            switch (command)
            {
                case "target":
                    if (channelId.ToString() != _plugin.Config.CordiPeep.DiscordChannelId) return;
                    fullName = parts[1] + " " + parts[2];
                    if (parts.Length < 3)
                    {
                        await SendCommandFeedback(channelId, "❌ Usage: `!target PlayerName World`");
                        return;
                    }
                    await HandleTargetCommand(fullName, parts[3], channelId);
                    break;

                case "emote":
                    if (channelId.ToString() != _plugin.Config.EmoteLog.ChannelId) return;
                    fullName = parts[2] + " " + parts[3];
                    if (parts.Length < 4)
                    {
                        await SendCommandFeedback(channelId, "❌ Usage: `!emote emotename PlayerName World`");
                        return;
                    }
                    await HandleEmoteCommand(parts[1], fullName, parts[4], channelId);
                    break;
            }
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Error processing Discord command");
            await SendCommandFeedback(channelId, "❌ An error occurred processing the command.");
        }
    }

    private async Task HandleTargetCommand(string name, string world, ulong channelId)
    {
        // var (name, world) = ParsePlayerSpec(playerSpec);

        if (string.IsNullOrEmpty(name))
        {
            await SendCommandFeedback(channelId, "❌ Invalid player name.");
            return;
        }

        Logger.Info($"Targeting {name} {world}");

        bool success = await _plugin.CordiPeep.TargetPlayer(name, world);

        if (success)
        {
            await SendCommandFeedback(channelId, $"✅ Targeted **{name}**{(string.IsNullOrEmpty(world) ? "" : "@" + world)}!");
        }
        else
        {
            await SendCommandFeedback(channelId, $"❌ Could not find **{name}**{(string.IsNullOrEmpty(world) ? "" : "@" + world)}");
        }
    }

    private async Task HandleEmoteCommand(string emoteName, string name, string world, ulong channelId)
    {
        if (string.IsNullOrEmpty(name))
        {
            await SendCommandFeedback(channelId, "❌ Invalid player name.");
            return;
        }

        Logger.Info($"Emoting {emoteName} at {name} {world}");

        float savedRotation = 0;

        // TargetPlayer is already thread-safe and async
        bool success = await _plugin.CordiPeep.TargetPlayer(name, world);

        if (success)
        {
            await CordiPlugin.Framework.RunOnFrameworkThread(() =>
            {
                var localPlayer = Service.ClientState.LocalPlayer;
                if (localPlayer != null)
                {
                    savedRotation = localPlayer.Rotation;
                }

                var emoteCommand = "/" + emoteName.ToLower();
                _plugin._chat.SendMessage(emoteCommand);

                _ = Task.Delay(5000).ContinueWith(_ =>
                {
                    CordiPlugin.Framework.RunOnFrameworkThread(() =>
                    {
                        var currentLocalPlayer = Service.ClientState.LocalPlayer;
                        if (currentLocalPlayer != null)
                        {
                            var currentTarget = Service.TargetManager.Target;
                            if (currentTarget != null && currentTarget.Name.ToString() == name)
                            {
                                Service.TargetManager.Target = null;
                            }

                            unsafe
                            {
                                var go = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)currentLocalPlayer.Address;
                                go->Rotation = savedRotation;
                            }
                        }
                    });
                });
            });
        }

        await SendCommandFeedback(channelId, $"✅ Emoted **{emoteName}** at **{name}**{(string.IsNullOrEmpty(world) ? "" : "@" + world)}!");
    }

    private (string? name, string? world) ParsePlayerSpec(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return (null, null);

        var atIndex = spec.LastIndexOf('@');
        if (atIndex > 0)
        {
            var name = spec.Substring(0, atIndex).Trim();
            var world = spec.Substring(atIndex + 1).Trim();
            return (name, world);
        }

        return (spec.Trim(), null);
    }

    private async Task SendCommandFeedback(ulong channelId, string message)
    {
        try
        {
            var embed = new DiscordEmbedBuilder()
                .WithDescription(message)
                .WithColor(message.StartsWith("✅") ? DiscordColor.Green : DiscordColor.Red)
                .WithTimestamp(DateTime.Now);

            await _plugin.Discord.SendWebhookMessageRaw(channelId, embed.Build(), "Cordi", null);
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Error sending command feedback");
        }
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

