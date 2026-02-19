using System;
using System.Collections.Generic;
using System.IO;
using Cordi.Core;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;

namespace Cordi.Services.QoLBar;

public class VariableService : IDisposable
{
    private readonly Dictionary<string, string> variables = new();
    private string configPath = string.Empty;
    private readonly ICommandManager commandManager;

    public VariableService(ICommandManager commandManager)
    {
        this.commandManager = commandManager;
        configPath = Path.Combine(CordiPlugin.PluginInterface.ConfigDirectory.FullName, "variables.json");
        Load();

        commandManager.AddHandler("/cordivar", new Dalamud.Game.Command.CommandInfo(OnCommand)
        {
            HelpMessage = "Manage QoLBar variables. Usage: /cordivar set <name> <value> | /cordivar get <name> | /cordivar list"
        });
    }

    private void OnCommand(string command, string args)
    {
        var parts = args.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            PrintHelp();
            return;
        }

        var action = parts[0].ToLowerInvariant();
        if (action == "set" && parts.Length >= 3)
        {
            var name = parts[1];
            var value = parts[2];
            SetVariable(name, value);
            CordiPlugin.ChatGui.Print($"[Cordi] Set variable '{name}' to '{value}'");
        }
        else if (action == "get" && parts.Length >= 2)
        {
            var name = parts[1];
            var val = GetVariable(name);
            CordiPlugin.ChatGui.Print($"[Cordi] Variable '{name}' = '{val}'");
        }
        else if (action == "list")
        {
            CordiPlugin.ChatGui.Print("[Cordi] Variables:");
            foreach (var kvp in variables)
            {
                CordiPlugin.ChatGui.Print($"  {kvp.Key} = {kvp.Value}");
            }
        }
        else
        {
            PrintHelp();
        }
    }

    private void PrintHelp()
    {
        CordiPlugin.ChatGui.Print("[Cordi] Usage:");
        CordiPlugin.ChatGui.Print("/cordivar set <name> <value>");
        CordiPlugin.ChatGui.Print("/cordivar get <name>");
        CordiPlugin.ChatGui.Print("/cordivar list");
    }

    public void SetVariable(string name, string value)
    {
        variables[name] = value;
        Save();
    }

    /// <summary>Sets a variable without saving to disk. Used for high-frequency dynamic updates.</summary>
    public void SetVariableInternal(string name, string value)
    {
        variables[name] = value;
    }

    public string GetVariable(string name)
    {
        return variables.TryGetValue(name, out var val) ? val : string.Empty;
    }

    public IReadOnlyDictionary<string, string> GetAllVariables() => variables;

    private void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(variables, Formatting.Indented);
            File.WriteAllText(configPath, json);
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Failed to save variables.");
        }
    }

    private void Load()
    {
        if (!File.Exists(configPath)) return;
        try
        {
            var json = File.ReadAllText(configPath);
            var loaded = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            if (loaded != null)
            {
                variables.Clear();
                foreach (var kvp in loaded)
                    variables[kvp.Key] = kvp.Value;
            }
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Failed to load variables.");
        }
    }

    public void Dispose()
    {
        commandManager.RemoveHandler("/cordivar");
    }
}
