using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;

namespace Cordi.Services.Discord.Queue;

public class DiscordSendQueue : IDisposable
{
    public const int MaxPendingDefault = 500;

    private static readonly TimeSpan[] BackoffDelays =
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(8),
    };

    private readonly CordiPlugin _plugin;
    private readonly int _maxPending;
    private readonly ConcurrentQueue<DiscordSendOperation> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cts = new();
    private Task? _worker;
    private bool _disposed;

    private long _pending;
    private long _sent;
    private long _failed;
    private long _retried;
    private long _dropped;

    public long Pending => Interlocked.Read(ref _pending);
    public long Sent => Interlocked.Read(ref _sent);
    public long Failed => Interlocked.Read(ref _failed);
    public long Retried => Interlocked.Read(ref _retried);
    public long Dropped => Interlocked.Read(ref _dropped);
    public int Capacity => _maxPending;
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    private CordiLogService Log => _plugin.LogService;
    private const string LogSource = "DiscordQueue";

    public DiscordSendQueue(CordiPlugin plugin, int maxPending = MaxPendingDefault)
    {
        _plugin = plugin;
        _maxPending = maxPending;
    }

    public void Start()
    {
        if (_worker != null) return;
        _worker = Task.Run(() => RunAsync(_cts.Token));
        Log.Info(LogSource, $"Worker started (capacity {_maxPending})");
    }

    public bool Enqueue(DiscordSendOperation op)
    {
        if (_disposed) return false;
        if (op.Action == null) return false;

        if (Interlocked.Read(ref _pending) >= _maxPending)
        {
            Interlocked.Increment(ref _dropped);
            Log.Warning(LogSource, $"Dropped '{op.Description}' — queue full ({_maxPending})");
            return false;
        }

        _queue.Enqueue(op);
        Interlocked.Increment(ref _pending);
        _signal.Release();
        return true;
    }

    public bool Enqueue(string description, string category, Func<Task> action, int maxAttempts = 3)
        => Enqueue(new DiscordSendOperation
        {
            Description = description,
            Category = category,
            Action = action,
            MaxAttempts = maxAttempts,
        });

    public Task RunAsync(string description, string category, Func<Task> action, int maxAttempts = 3)
    {
        var tcs = new TaskCompletionSource();
        var op = new DiscordSendOperation
        {
            Description = description,
            Category = category,
            MaxAttempts = maxAttempts,
            Action = async () =>
            {
                await action().ConfigureAwait(false);
                tcs.TrySetResult();
            },
            OnPermanentFailure = ex => tcs.TrySetException(ex),
        };
        if (!Enqueue(op))
            tcs.TrySetException(new InvalidOperationException("DiscordSendQueue is full"));
        return tcs.Task;
    }

    public Task<T> RunAsync<T>(string description, string category, Func<Task<T>> action, int maxAttempts = 3)
    {
        var tcs = new TaskCompletionSource<T>();
        var op = new DiscordSendOperation
        {
            Description = description,
            Category = category,
            MaxAttempts = maxAttempts,
            Action = async () =>
            {
                var result = await action().ConfigureAwait(false);
                tcs.TrySetResult(result);
            },
            OnPermanentFailure = ex => tcs.TrySetException(ex),
        };
        if (!Enqueue(op))
            tcs.TrySetException(new InvalidOperationException("DiscordSendQueue is full"));
        return tcs.Task;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }

            if (!_queue.TryDequeue(out var op)) continue;
            Interlocked.Decrement(ref _pending);

            await ProcessAsync(op, ct).ConfigureAwait(false);
        }
    }

    private async Task ProcessAsync(DiscordSendOperation op, CancellationToken ct)
    {
        try
        {
            op.Attempts++;
            await op.Action().ConfigureAwait(false);
            Interlocked.Increment(ref _sent);
        }
        catch (Exception ex)
        {
            if (op.Attempts < op.MaxAttempts && !ct.IsCancellationRequested)
            {
                Interlocked.Increment(ref _retried);
                var delay = BackoffDelays[Math.Min(op.Attempts - 1, BackoffDelays.Length - 1)];
                Log.Warning(LogSource, $"'{op.Description}' attempt {op.Attempts}/{op.MaxAttempts} failed: {ex.Message}. Retrying in {delay.TotalSeconds}s");

                try { await Task.Delay(delay, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }

                _queue.Enqueue(op);
                Interlocked.Increment(ref _pending);
                _signal.Release();
            }
            else
            {
                Interlocked.Increment(ref _failed);
                Service.Log.Error(ex, $"[DiscordQueue] '{op.Description}' permanently failed after {op.Attempts} attempt(s)");
                Log.Error(LogSource, $"'{op.Description}' failed permanently after {op.Attempts} attempt(s): {ex.Message}");
                try { op.OnPermanentFailure?.Invoke(ex); } catch { }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _cts.Cancel(); } catch { }
        try { _worker?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _signal.Dispose();
        _cts.Dispose();
    }
}
