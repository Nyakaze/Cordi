using System;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Core.Caching;
using Cordi.Helpers;
using Crovus.Client;
using Crovus.Factory;
using Crovus.Models;

namespace Cordi.Services.Discord.Webhooks;

public sealed class DiscordWebhookService
{
    private const string WebhookName = "Cordi Hook";
    private const string LogSource = "Webhook";

    private readonly CordiLogService Log;
    private readonly Connection.DiscordConnection _connection;
    private readonly Projections.DiscordChannelProjection _channels;
    private readonly Cache<ulong, DiscordWebhook> _webhookCache;

    public DiscordWebhookService(CordiLogService log, ICacheRegistry caches, Connection.DiscordConnection connection, Projections.DiscordChannelProjection channels)
    {
        Log = log;
        _connection = connection;
        _channels = channels;
        _webhookCache = new Cache<ulong, DiscordWebhook>(
            "webhook.byChannel", caches,
            maxSize: 100, ttl: TimeSpan.FromHours(2));
    }

    public async Task<ulong> ExecuteAsync(ulong channelId, Action<WebhookMessageFactory> configure,
        CancellationToken cancellationToken = default) =>
        (await SendAsync(channelId, configure, cancellationToken))?.Id.Value ?? 0UL;

    public async Task<DiscordMessage?> SendAsync(ulong channelId, Action<WebhookMessageFactory> configure,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await RetryHelper.WithRetryAsync(async () =>
            {
                if (await ResolveAsync(channelId, cancellationToken) is not { } target) return null;

                var factory = WebhookMessageFactory.Create();
                configure(factory);

                return await target.Context.Services.Webhooks.SendAsync(
                    target.Webhook, factory.Build(), target.ThreadId, wait: true, cancellationToken);
            });
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Failed to execute webhook in channel {channelId}", ex);
            return null;
        }
    }

    public async Task EditMessageAsync(ulong channelId, ulong messageId, Action<MessageFactory> configure,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await RetryHelper.WithRetryAsync(async () =>
            {
                if (await ResolveAsync(channelId, cancellationToken) is not { } target) return;

                await target.Context.Services.Webhooks.EditMessageAsync(
                    target.Webhook, messageId, configure, target.ThreadId, cancellationToken);
            });
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Failed to edit webhook message {messageId} in channel {channelId}", ex);
        }
    }

    public async Task DeleteMessageAsync(ulong channelId, ulong messageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await RetryHelper.WithRetryAsync(async () =>
            {
                if (await ResolveAsync(channelId, cancellationToken) is not { } target) return;

                await target.Context.Services.Webhooks.DeleteMessageAsync(
                    target.Webhook, messageId, target.ThreadId, cancellationToken);
            });
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Failed to delete webhook message {messageId} in channel {channelId}", ex);
        }
    }

    public void ClearCache() => _webhookCache.Clear();

    private async Task<WebhookTarget?> ResolveAsync(ulong channelId, CancellationToken cancellationToken)
    {
        if (_connection.Context is not { } context)
        {
            Log.Warning(LogSource, $"Cannot resolve webhook for channel {channelId}: not connected");
            return null;
        }

        var channel = _channels.Find(channelId)
                      ?? await context.Services.Channels.GetAsync(channelId, cancellationToken);

        var hostId = channel.IsThread ? channel.ParentId?.Value ?? 0UL : channel.Id.Value;

        if (hostId == 0UL)
        {
            Log.Error(LogSource, $"Could not resolve the parent channel of thread {channelId}");
            return null;
        }

        var threadId = channel.IsThread ? new Snowflake(channelId) : (Snowflake?)null;

        if (_webhookCache.TryGet(hostId, out var cached))
            return new WebhookTarget(context, cached, threadId);

        var webhook = await context.Services.Webhooks.GetOrCreateAsync(
            hostId, WebhookName, cancellationToken: cancellationToken);

        _webhookCache.Set(hostId, webhook);

        return new WebhookTarget(context, webhook, threadId);
    }

    private readonly record struct WebhookTarget(ICrovusContext Context, DiscordWebhook Webhook, Snowflake? ThreadId);
}
