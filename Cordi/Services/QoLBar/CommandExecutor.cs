using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Plugin.Services;

namespace Cordi.Services.QoLBar;

public class CommandExecutor : IDisposable
{
    private readonly ICommandManager commandManager;
    private readonly IChatGui chatGui;
    private readonly IFramework framework;
    private readonly Queue<string> commandQueue = new();
    private DateTime lastCommandTime = DateTime.MinValue;
    private static readonly TimeSpan CommandThrottle = TimeSpan.Zero; // Execute one per frame

    public CommandExecutor(ICommandManager commandManager, IChatGui chatGui, IFramework framework)
    {
        this.commandManager = commandManager;
        this.chatGui = chatGui;
        this.framework = framework;
    }

    public void QueueCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        var lines = command.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                commandQueue.Enqueue(trimmed);
        }
    }

    public void ReadyCommand()
    {
        if (commandQueue.Count == 0) return;

        var now = DateTime.UtcNow;
        if (now - lastCommandTime < CommandThrottle) return;

        var cmd = commandQueue.Dequeue();
        lastCommandTime = now;
        ExecuteCommand(cmd);
    }

    private void ExecuteCommand(string command)
    {
        try
        {
            if (command.StartsWith("/"))
            {
                ECommons.Automation.Chat.SendMessage(command);
            }
            else
            {
                chatGui.Print(command);
            }
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, $"Failed to execute command: {command}");
        }
    }

    public void Dispose()
    {
        commandQueue.Clear();
    }
}
