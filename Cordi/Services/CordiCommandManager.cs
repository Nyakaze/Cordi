using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cordi.Attributes;
using Dalamud.Game.Command;
using Cordi.Core;
using Cordi.Configuration;
using Dalamud.Plugin.Services;


namespace Cordi;

public class CordiCommandManager<THost> : IDisposable
{
    private readonly ICommandManager command;
    private readonly (string, CommandInfo)[] pluginCommands;
    private readonly THost host;

    public CordiCommandManager(THost host, ICommandManager command)
    {
        this.command = command;
        this.host = host;

        this.pluginCommands = host
                              .GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static |
                                                    BindingFlags.Instance)
                              .Where(x => x.GetCustomAttribute<CommandAttribute>() != null)
                              .SelectMany(GetCommandInfoTuple)
                              .ToArray();

        AddCommandHandlers();
    }

    private void AddCommandHandlers()
    {
        for (var i = 0; i < this.pluginCommands.Length; i++)
        {
            var (command, commandInfo) = this.pluginCommands[i];
            this.command.AddHandler(command, commandInfo);
        }
    }

    private void RemoveCommandHandlers()
    {
        for (var i = 0; i < this.pluginCommands.Length; i++)
        {
            var (command, _) = this.pluginCommands[i];
            this.command.RemoveHandler(command);
        }
    }

    private IEnumerable<(string, CommandInfo)> GetCommandInfoTuple(MethodInfo method)
    {
        var handlerDelegate = (IReadOnlyCommandInfo.HandlerDelegate)Delegate.CreateDelegate(typeof(IReadOnlyCommandInfo.HandlerDelegate), this.host, method);

        var command = handlerDelegate.Method.GetCustomAttribute<CommandAttribute>();
        var aliases = handlerDelegate.Method.GetCustomAttribute<AliasesAttribute>();
        var helpMessage = handlerDelegate.Method.GetCustomAttribute<HelpMessageAttribute>();
        var doNotShowInHelp = handlerDelegate.Method.GetCustomAttribute<DoNotShowInHelpAttribute>();

        var commandInfo = new CommandInfo(handlerDelegate)
        {
            HelpMessage = helpMessage?.HelpMessage ?? string.Empty,
            ShowInHelp = doNotShowInHelp == null,
        };


        var commandInfoTuples = new List<(string, CommandInfo)> { (command.Command, commandInfo) };
        if (aliases != null)
        {

            for (var i = 0; i < aliases.Aliases.Length; i++)
            {
                commandInfoTuples.Add((aliases.Aliases[i], commandInfo));
            }
        }

        return commandInfoTuples;
    }

    public void Dispose()
    {
        RemoveCommandHandlers();
    }
}
