using System;
using System.Collections.Concurrent;
using System.Threading;
using Cordi.Model;
using Dalamud.Plugin.Services;
using Cordi.Core;
using Cordi.Configuration;

namespace Cordi.Services.Discord;

public class DiscordMessageQueue
{
    static readonly IPluginLog Logger = Service.Log;
    private volatile bool runQueue = true;

    private readonly CordiPlugin _plugin;
    private readonly Thread _runnerThread;

    private readonly ConcurrentQueue<QueuedXivEvent> _eventQueue = new();

    public DiscordMessageQueue(CordiPlugin plugin)
    {
        _plugin = plugin;
        _runnerThread = new Thread(RunMessageQueue);
    }

    public void Start()
    {
        runQueue = true;
        _runnerThread.Start();
    }

    public void Stop()
    {
        runQueue = false;
        if (_runnerThread.IsAlive)
            _runnerThread.Join();
    }

    public void Enqueue(QueuedXivEvent @event) => this._eventQueue.Enqueue(@event);

    public async void RunMessageQueue()
    {
        while (runQueue)
        {
            if (_eventQueue.TryDequeue(out var evt))
            {
                try
                {

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }
    }
}
