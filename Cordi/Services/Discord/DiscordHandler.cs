using System;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Logging;

using Cordi.Core;
using Cordi.Domain;
using Cordi.Services.Discord.Send;
using Cordi.Services.Discord.Webhooks;
using Crovus.Models;

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
    private readonly DiscordSender _sender;
    private readonly DiscordMessageRouter _messageRouter;
    public DiscordMessageRouter MessageRouter => _messageRouter;
    public DiscordSender Sender => _sender;

    private CordiLogService Log => _plugin.LogService;
    private const string LogSource = "Discord";

    public DiscordHandler(CordiPlugin plugin, DiscordWebhookService webhooks, AdvertisementFilterService adFilter)
    {
        _plugin = plugin;
        _webhooks = webhooks;
        _sender = new DiscordSender(plugin, webhooks, adFilter);
        _messageRouter = new DiscordMessageRouter(plugin);

        _intent = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents | DiscordIntents.Guilds | DiscordIntents.GuildWebhooks | DiscordIntents.GuildMessageReactions | DiscordIntents.GuildMembers;
    }

    public async Task Start()
    {
        if (string.IsNullOrEmpty(_plugin.Config.Discord.BotToken))

        {
            Log.Error(LogSource, "Bot token is empty. Please configure the bot token in the settings.");
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

            _plugin.SlashCommandService.Bind(_client);

            _client.Ready += OnReady;
            await _client.ConnectAsync();
            await _plugin.DiscordConnection.StartAsync();
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

    private async Task OnReady(DiscordClient sender, ReadyEventArgs e)
    {
        Logger.Info("DiscordHandler READY!!");

        if (_plugin.Config.SlashCommands.Enabled)
        {
            try
            {
                _plugin.SlashCommandService.PopulateEmoteCommands();

                await _plugin.SlashCommandService.RegisterCommandsAsync();
            }
            catch (Exception ex)
            {
                Log.Error(LogSource, $"Failed to auto-register slash commands: {ex.Message}");
            }
        }
    }

    public Task SendMessage(ulong? channelId, SeString message, Player sender,
        XivChatType chatType = XivChatType.None, string? correspondentName = null) =>
        _sender.SendMessage(channelId, message, sender, chatType, correspondentName);

    public Task SendMessage(ulong? channelId, string content, Player sender,
        XivChatType chatType = XivChatType.None, string? correspondentName = null, string? avatarUrl = null) =>
        _sender.SendMessage(channelId, content, sender, chatType, correspondentName, avatarUrl);

    public Task<ulong> SendWebhookMessage(ulong channelId, string content, Player sender) =>
        _sender.SendWebhookMessage(channelId, content, sender);

    public Task<ulong> SendWebhookMessage(ulong channelId, DiscordEmbed embed, Player sender) =>
        _sender.SendWebhookMessage(channelId, embed, sender);

    public Task<ulong> SendWebhookMessageRaw(ulong channelId, DiscordEmbed embed, string username, string? avatarUrl) =>
        _sender.SendWebhookMessageRaw(channelId, embed, username, avatarUrl);

    public Task EditWebhookMessage(ulong channelId, ulong messageId, DiscordEmbed embed) =>
        _sender.EditWebhookMessage(channelId, messageId, embed);

    public Task AddReaction(ulong channelId, ulong messageId, string emoji) =>
        _sender.AddReaction(channelId, messageId, emoji);

    public Task RemoveReaction(ulong channelId, ulong messageId, string emoji) =>
        _sender.RemoveReaction(channelId, messageId, emoji);

    public Task<ulong> SendEmbedToChannelAsync(ulong channelId, DiscordEmbed embed) =>
        _sender.SendEmbedToChannelAsync(channelId, embed);

    public Task<bool> EditEmbedInChannelAsync(ulong channelId, ulong messageId, DiscordEmbed embed) =>
        _sender.EditEmbedInChannelAsync(channelId, messageId, embed);

    public Task DeleteChannelMessageAsync(ulong channelId, ulong messageId) =>
        _sender.DeleteChannelMessageAsync(channelId, messageId);

    public async Task Stop()
    {
        IsBusy = true;
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

        _plugin.SlashCommandService.Unbind();

        await _plugin.DiscordConnection.StopAsync();

        await _client.DisconnectAsync();
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
