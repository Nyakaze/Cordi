using System;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;

namespace Cordi.Services.Chatbox;

public sealed class ChatboxSearchSession : IDisposable
{
    private readonly CordiPlugin _plugin;

    private CancellationTokenSource? _cts;
    private int _generation;
    private bool _disposed;

    public ChatboxSearchSession(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public ChatboxSearchQuery Query { get; } = new();

    public ChatboxSearchResults Results { get; private set; } = ChatboxSearchResults.Empty;

    public bool Running { get; private set; }

    public bool HasRun { get; private set; }

    public void Start()
    {
        if (_disposed) return;

        Cancel();

        var generation = Interlocked.Increment(ref _generation);
        var cts = new CancellationTokenSource();
        _cts = cts;
        Running = true;
        HasRun = true;

        var snapshot = Query.Clone();
        var chatbox = _plugin.Chatbox;

        Task.Run(() =>
        {
            ChatboxSearchResults results;

            try
            {
                results = chatbox.ExecuteSearch(snapshot, cts.Token);
            }
            catch (Exception ex)
            {
                _plugin.LogService.Error("Chatbox", "Message search failed", ex);
                results = new ChatboxSearchResults { Error = ex.Message };
            }

            if (Volatile.Read(ref _generation) != generation) return;

            Results = results;
            Running = false;
        }, cts.Token);
    }

    public void Cancel()
    {
        Interlocked.Increment(ref _generation);
        Running = false;

        var cts = _cts;
        _cts = null;

        if (cts == null) return;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        cts.Dispose();
    }

    public void Clear()
    {
        Cancel();
        Results = ChatboxSearchResults.Empty;
        HasRun = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Cancel();
    }
}
