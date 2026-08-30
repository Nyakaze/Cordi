using System;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;

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

    public bool IsBusy => _plugin.DiscordConnection.IsBusy;

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
    }

    public Task Start() => _plugin.DiscordConnection.StartAsync();

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
        Logger.Info($"[{_instanceId}] Disconnecting Discord client...");

        await _plugin.DiscordConnection.StopAsync();

        _webhooks.ClearCache();
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
