using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Cordi.Services.Chatbox;

public sealed class ChatboxFetchQueue : IDisposable
{
    private readonly ConcurrentDictionary<string, byte> _inFlight = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTime> _failures = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate;
    private readonly CancellationTokenSource _cts = new();
    private readonly TimeSpan _failureBackoff;
    private readonly string _label;
    private bool _disposed;

    public ChatboxFetchQueue(int concurrency, TimeSpan failureBackoff, string label)
    {
        _gate = new SemaphoreSlim(concurrency, concurrency);
        _failureBackoff = failureBackoff;
        _label = label;
    }

    public CancellationToken Token => _cts.Token;

    public int Pending => _inFlight.Count;

    public int Failed => _failures.Count;

    public void MarkFailed(string url) => _failures[url] = DateTime.UtcNow;

    public void ClearFailures() => _failures.Clear();

    public void Enqueue(string url, Func<string, Task> resolve)
    {
        if (_disposed) return;

        if (_failures.TryGetValue(url, out var failedAt))
        {
            if (DateTime.UtcNow - failedAt < _failureBackoff) return;
            _failures.TryRemove(url, out _);
        }

        if (!_inFlight.TryAdd(url, 0)) return;

        _ = Task.Run(() => RunAsync(url, resolve), _cts.Token);
    }

    private async Task RunAsync(string url, Func<string, Task> resolve)
    {
        try
        {
            await resolve(url).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _failures[url] = DateTime.UtcNow;
            Service.Log.Debug($"[Chatbox] {_label} failed for {url}: {ex.Message}");
        }
        finally
        {
            _inFlight.TryRemove(url, out _);
        }
    }

    public async Task<bool> EnterGateAsync()
    {
        try
        {
            await _gate.WaitAsync(_cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void ExitGate()
    {
        try { _gate.Release(); }
        catch (ObjectDisposedException) { }
    }

    public void Cancel() => _cts.Cancel();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _cts.Dispose();
        _gate.Dispose();
    }
}
