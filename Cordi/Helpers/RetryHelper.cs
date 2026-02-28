using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace Cordi.Helpers;

public static class RetryHelper
{
    public static async Task<T> WithRetryAsync<T>(
        Func<Task<T>> action,
        int maxRetries = 3,
        int initialDelayMs = 1000,
        IPluginLog? log = null)
    {
        int delay = initialDelayMs;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (IsTransient(ex) && i < maxRetries - 1)
            {
                log?.Warning($"Retry {i + 1}/{maxRetries}: {ex.Message}");
                await Task.Delay(delay);
                delay *= 2;
            }
        }
        return default!;
    }

    public static async Task WithRetryAsync(
        Func<Task> action,
        int maxRetries = 3,
        int initialDelayMs = 1000,
        IPluginLog? log = null)
    {
        int delay = initialDelayMs;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex) when (IsTransient(ex) && i < maxRetries - 1)
            {
                log?.Warning($"Retry {i + 1}/{maxRetries}: {ex.Message}");
                await Task.Delay(delay);
                delay *= 2;
            }
        }
    }

    private static bool IsTransient(Exception ex)
    {
        return ex is HttpRequestException
            || ex is SocketException
            || ex is IOException;
    }
}
