using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services;
using Crovus.Client;
using Crovus.Events;
using Crovus.Gateway;
using Crovus.Logs;
using Crovus.Models;

namespace Cordi.Services.Discord.Connection;

public sealed class DiscordConnection : IAsyncDisposable
{
    public const GatewayIntents RequiredIntents =
        GatewayIntents.Guilds |
        GatewayIntents.GuildMembers |
        GatewayIntents.GuildPresences |
        GatewayIntents.GuildMessages |
        GatewayIntents.GuildMessageReactions |
        GatewayIntents.GuildWebhooks |
        GatewayIntents.GuildExpressions |
        GatewayIntents.DirectMessages |
        GatewayIntents.DirectMessageReactions |
        GatewayIntents.MessageContent;

    private const string LogSource = "Discord";

    private readonly CordiPlugin _plugin;
    private readonly DiscordSession _session = new();

    private readonly List<Func<CrovusClient, IDisposable>> _registrations = [];
    private readonly List<IDisposable> _subscriptions = [];

    private CrovusClient? _client;
    private CancellationTokenSource? _cts;
    private Task? _runner;
    private IDisposable? _readySubscription;
    private IDisposable? _logSubscription;
    private bool _disposed;

    public DiscordConnection(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public DiscordSession Session => _session;

    public bool IsBusy { get; private set; }

    public bool IsConnected => _client is { IsConnected: true };

    public ICrovusContext? Context => _client;

    public PresenceTracker? Presences => _client?.Presences;

    public event Func<ReadyEvent, Task>? Ready;

    public IDisposable On<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : DiscordEvent
    {
        if (_client is null)
            throw new InvalidOperationException("The Discord connection has not been started.");

        return _client.On(handler);
    }

    public void Register<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : DiscordEvent
    {
        _registrations.Add(client => client.On(handler));

        if (_client is { } client)
            _subscriptions.Add(client.On(handler));
    }

    public async Task StartAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var token = _plugin.Config.Discord.BotToken;

        if (string.IsNullOrWhiteSpace(token))
        {
            Log.Error(LogSource, "Bot token is empty, cannot start");
            _plugin.Config.Discord.BotStarted = false;
            _plugin.Config.Save();
            return;
        }

        if (IsBusy) return;
        IsBusy = true;

        try
        {
            if (_client is not null)
            {
                Log.Info(LogSource, "Already started, restarting");
                await StopAsync();
            }

            var client = new CrovusClient(new CrovusClientOptions
            {
                Token = token,
                Intents = RequiredIntents,
                EnableCache = true,
                ResolveEntities = true,
                SequentialDispatch = true,
                MinimumLogLevel = LogLevel.Information,
            });

            _logSubscription = client.Diagnostics.SubscribeLogs(Forward);
            _readySubscription = client.On<ReadyEvent>(OnReadyAsync);

            foreach (var registration in _registrations)
                _subscriptions.Add(registration(client));

            _client = client;
            _cts = new CancellationTokenSource();
            _runner = Task.Run(() => RunAsync(client, _cts.Token));

            _plugin.Config.Discord.BotStarted = true;
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Bot connection failed: {Describe(ex)}");
            _plugin.Config.Discord.BotStarted = false;
            await TeardownAsync();
        }
        finally
        {
            IsBusy = false;
        }

        _plugin.Config.Save();
    }

    public async Task StopAsync()
    {
        if (_client is null) return;

        try
        {
            _cts?.Cancel();

            if (_runner is { } runner)
                await runner.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            Log.Warning(LogSource, "Gateway loop did not stop within 5s");
        }
        catch (Exception ex)
        {
            Log.Warning(LogSource, $"Stop failed: {Describe(ex)}");
        }

        await TeardownAsync();

        _plugin.Config.Discord.BotStarted = false;
        Log.Info(LogSource, "Bot disconnected");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync();
    }

    private async Task RunAsync(CrovusClient client, CancellationToken ct)
    {
        try
        {
            await client.RunAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Gateway loop faulted: {Describe(ex)}");
        }
    }

    private async Task OnReadyAsync(ReadyEvent e, CancellationToken ct)
    {
        _session.Apply(e);

        Log.Info(LogSource, $"Connected as {e.User.DisplayName}");

        if (Ready is { } handler)
            await handler(e);
    }

    private async Task TeardownAsync()
    {
        _readySubscription?.Dispose();
        _readySubscription = null;

        _logSubscription?.Dispose();
        _logSubscription = null;

        foreach (var subscription in _subscriptions)
            subscription.Dispose();

        _subscriptions.Clear();

        if (_client is { } client)
        {
            try { await client.DisposeAsync(); }
            catch (Exception ex) { Log.Warning(LogSource, $"Dispose failed: {Describe(ex)}"); }
        }

        _client = null;

        _cts?.Dispose();
        _cts = null;
        _runner = null;

        _session.Clear();
    }

    private void Forward(LogEntry entry)
    {
        var level = entry.Level switch
        {
            LogLevel.Trace or LogLevel.Debug => CordiLogLevel.Debug,
            LogLevel.Information => CordiLogLevel.Info,
            LogLevel.Warning => CordiLogLevel.Warning,
            _ => CordiLogLevel.Error,
        };

        Log.Log(LogSource, level, entry.Exception is { } ex
            ? $"{entry.Message} — {Describe(ex)}"
            : entry.Message);
    }

    private static string Describe(Exception ex) => $"{ex.GetType().Name}: {ex.Message}";

    private CordiLogService Log => _plugin.LogService;
}
