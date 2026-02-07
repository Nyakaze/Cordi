using System;
using System.Net.Http;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Cordi.Services;
using Dalamud.Plugin.Services;
using DSharpPlus;
using DSharpPlus.Entities;
using Cordi.Core;
using Cordi.Configuration;

namespace Cordi.Services.Discord;

public class DiscordWebhookService
{
    private static readonly IPluginLog Logger = Service.Log;
    private readonly ConcurrentDictionary<ulong, DiscordWebhook> _webhookCache = new();
    private readonly CordiPlugin _plugin;

    public DiscordWebhookService(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public async Task<DiscordWebhook> GetWebhookAsync(DiscordChannel channel)
    {
        if (channel.IsThread)
        {
            try
            {

                var parent = channel.Parent;
                if (parent == null)
                {

                    if (channel.Guild != null && channel.ParentId.HasValue)
                    {
                        parent = channel.Guild.GetChannel(channel.ParentId.Value);
                    }
                }

                if (parent == null) throw new Exception("Could not resolve parent channel for thread.");
                channel = parent;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to resolve parent for thread {channel.Id}. Guild is null? {channel.Guild == null}");
                throw;
            }
        }

        if (_webhookCache.TryGetValue(channel.Id, out var cachedWebhook))
        {
            return cachedWebhook;
        }

        try
        {
            var webhooks = await channel.GetWebhooksAsync();
            var webhook = webhooks.FirstOrDefault(w => w.Name == "Cordi Hook");
            if (webhook == null)
            {
                webhook = await channel.CreateWebhookAsync("Cordi Hook");
                Logger.Info($"Created webhook 'Cordi Hook' in channel {channel.Name}");
            }

            _webhookCache.TryAdd(channel.Id, webhook);
            return webhook;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to get or create webhook for channel {channel.Id}");
            throw;
        }
    }

    public async Task<ulong> ExecuteWebhookAsync(DiscordChannel channel, DiscordWebhookBuilder builder)
    {
        const int maxRetries = 3;
        int delay = 1000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var webhook = await GetWebhookAsync(channel);
                if (channel.IsThread)
                {
                    builder.WithThreadId(channel.Id);
                }
                var msg = await webhook.ExecuteAsync(builder);
                return msg.Id;
            }
            catch (HttpRequestException ex)
            {
                Logger.Warning($"Failed to execute webhook (Attempt {i + 1}/{maxRetries}): {ex.Message}");
                if (i == maxRetries - 1)
                {
                    Logger.Error(ex, "Failed to execute webhook.");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to execute webhook.");
                return 0;
            }

            await Task.Delay(delay);
            delay *= 2;
        }
        return 0;
    }

    public async Task EditWebhookMessageAsync(DiscordChannel channel, ulong messageId, DiscordWebhookBuilder builder)
    {
        const int maxRetries = 3;
        int delay = 1000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var webhook = await GetWebhookAsync(channel);
                await webhook.EditMessageAsync(messageId, builder);
                return;
            }
            catch (HttpRequestException ex)
            {
                Logger.Warning($"Failed to edit webhook message (Attempt {i + 1}/{maxRetries}): {ex.Message}");
                if (i == maxRetries - 1)
                {
                    Logger.Error(ex, "Failed to edit webhook message.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to edit webhook message.");
                return;
            }

            await Task.Delay(delay);
            delay *= 2;
        }
    }

    public async Task DeleteWebhookMessageAsync(DiscordChannel channel, ulong messageId)
    {
        const int maxRetries = 3;
        int delay = 1000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var webhook = await GetWebhookAsync(channel);
                await webhook.DeleteMessageAsync(messageId);
                return;
            }
            catch (HttpRequestException ex)
            {
                Logger.Warning($"Failed to delete webhook message (Attempt {i + 1}/{maxRetries}): {ex.Message}");
                if (i == maxRetries - 1)
                {
                    Logger.Error(ex, "Failed to delete webhook message.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to delete webhook message.");
                return;
            }

            await Task.Delay(delay);
            delay *= 2;
        }
    }

    public void ClearCache() => _webhookCache.Clear();
}
