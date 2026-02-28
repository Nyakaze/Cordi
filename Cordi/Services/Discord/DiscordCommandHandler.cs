using System;
using System.Threading.Tasks;
using Cordi.Core;
using Dalamud.Plugin.Services;
using DSharpPlus.Entities;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Cordi.Services.Discord;

public class DiscordCommandHandler
{
    private static readonly IPluginLog Logger = Service.Log;
    private readonly CordiPlugin _plugin;

    public DiscordCommandHandler(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public async Task ProcessDiscordCommand(string content, ulong channelId)
    {
        if (!_plugin.Config.Discord.AllowDiscordCommands) return;
        if (!content.StartsWith(_plugin.Config.Discord.CommandPrefix)) return;
        Logger.Info($"Processing Discord command: {content}");

        var parts = content.Substring(_plugin.Config.Discord.CommandPrefix.Length).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Logger.Info($"Parts: {string.Join(", ", parts)}");
        if (parts.Length == 0) return;

        var command = parts[0].ToLower();
        string fullName = string.Empty;

        try
        {
            switch (command)
            {
                case "target":
                    if (channelId.ToString() != _plugin.Config.CordiPeep.DiscordChannelId) return;
                    fullName = parts[1] + " " + parts[2];
                    if (parts.Length < 3)
                    {
                        await SendCommandFeedback(channelId, "❌ Usage: `!target PlayerName World`");
                        return;
                    }
                    await HandleTargetCommand(fullName, parts[3], channelId);
                    break;

                case "emote":
                    if (channelId.ToString() != _plugin.Config.EmoteLog.ChannelId) return;
                    fullName = parts[2] + " " + parts[3];
                    if (parts.Length < 4)
                    {
                        await SendCommandFeedback(channelId, "❌ Usage: `!emote emotename PlayerName World`");
                        return;
                    }
                    await HandleEmoteCommand(parts[1], fullName, parts[4], channelId);
                    break;
            }
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Error processing Discord command");
            await SendCommandFeedback(channelId, "❌ An error occurred processing the command.");
        }
    }

    private async Task HandleTargetCommand(string name, string world, ulong channelId)
    {
        if (string.IsNullOrEmpty(name))
        {
            await SendCommandFeedback(channelId, "❌ Invalid player name.");
            return;
        }

        Logger.Info($"Targeting {name} {world}");

        bool success = await _plugin.CordiPeep.TargetPlayer(name, world);

        if (success)
        {
            await SendCommandFeedback(channelId, $"✅ Targeted **{name}**{(string.IsNullOrEmpty(world) ? "" : "@" + world)}!");
        }
        else
        {
            await SendCommandFeedback(channelId, $"❌ Could not find **{name}**{(string.IsNullOrEmpty(world) ? "" : "@" + world)}");
        }
    }

    private async Task HandleEmoteCommand(string emoteName, string name, string world, ulong channelId)
    {
        if (string.IsNullOrEmpty(name))
        {
            await SendCommandFeedback(channelId, "❌ Invalid player name.");
            return;
        }

        Logger.Info($"Emoting {emoteName} at {name} {world}");

        float savedRotation = 0;

        bool success = await _plugin.CordiPeep.TargetPlayer(name, world);

        if (success)
        {
            await CordiPlugin.Framework.RunOnFrameworkThread(() =>
            {
                var localPlayer = Service.ClientState.LocalPlayer;
                if (localPlayer != null)
                {
                    savedRotation = localPlayer.Rotation;
                }

                var emoteCommand = "/" + emoteName.ToLower();
                _plugin._chat.SendMessage(emoteCommand);

                _ = Task.Delay(5000).ContinueWith(_ =>
                {
                    CordiPlugin.Framework.RunOnFrameworkThread(() =>
                    {
                        var currentLocalPlayer = Service.ClientState.LocalPlayer;
                        if (currentLocalPlayer != null)
                        {
                            var currentTarget = Service.TargetManager.Target;
                            if (currentTarget != null && currentTarget.Name.ToString() == name)
                            {
                                Service.TargetManager.Target = null;
                            }

                            unsafe
                            {
                                var go = (GameObject*)currentLocalPlayer.Address;
                                go->Rotation = savedRotation;
                            }
                        }
                    });
                });
            });
        }

        await SendCommandFeedback(channelId, $"✅ Emoted **{emoteName}** at **{name}**{(string.IsNullOrEmpty(world) ? "" : "@" + world)}!");
    }

    public (string? name, string? world) ParsePlayerSpec(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return (null, null);

        var atIndex = spec.LastIndexOf('@');
        if (atIndex > 0)
        {
            var name = spec.Substring(0, atIndex).Trim();
            var world = spec.Substring(atIndex + 1).Trim();
            return (name, world);
        }

        return (spec.Trim(), null);
    }

    private async Task SendCommandFeedback(ulong channelId, string message)
    {
        try
        {
            var embedBuilder = _plugin.EmbedFactory.CreateEmbedBuilder(
                string.Empty,
                message,
                message.StartsWith("✅") ? DiscordColor.Green : DiscordColor.Red
            );

            await _plugin.Discord.SendWebhookMessageRaw(channelId, embedBuilder.Build(), "Cordi", null);
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Error sending command feedback");
        }
    }
}
