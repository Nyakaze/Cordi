using System;
using System.Threading.Tasks;
using Cordi.Core;
using Dalamud.Plugin.Services;
using DSharpPlus.Entities;

namespace Cordi.Services.Features;

public class PartyDiscordNotifier
{
    private readonly CordiPlugin _plugin;
    private readonly IPluginLog _logger;

    public PartyDiscordNotifier(CordiPlugin plugin)
    {
        _plugin = plugin;
        _logger = Service.Log;
    }

    public async Task<ulong> SendNotificationAsync(string title, string description, DiscordColor color, string? characterName = null, string? characterWorld = null)
    {
        if (!_plugin.Config.Party.DiscordEnabled) return 0;

        var channelIdStr = _plugin.Config.Party.DiscordChannelId;
        if (!ulong.TryParse(channelIdStr, out var channelId)) return 0;

        string? avatarUrl = null;
        if (!string.IsNullOrEmpty(characterName) && !string.IsNullOrEmpty(characterWorld))
        {
            avatarUrl = await _plugin.Lodestone.GetAvatarUrlAsync(characterName, characterWorld);
        }

        var embedBuilder = _plugin.EmbedFactory.CreateEmbedBuilder(title, description, color, avatarUrl);

        string username = "Party Notification";
        if (!string.IsNullOrEmpty(characterName) && !string.IsNullOrEmpty(characterWorld))
        {
            username = $"{characterName}@{characterWorld}";
        }

        return await _plugin.Discord.SendWebhookMessageRaw(channelId, embedBuilder.Build(), username, avatarUrl);
    }

    public async Task UpdateNotificationAsync(ulong msgId, string title, string description, DiscordColor color, string? characterName = null, string? characterWorld = null)
    {
        if (!_plugin.Config.Party.DiscordEnabled) return;

        var channelIdStr = _plugin.Config.Party.DiscordChannelId;
        if (!ulong.TryParse(channelIdStr, out var channelId)) return;

        var embedBuilder = await _plugin.EmbedFactory.CreatePlayerEmbedBuilderAsync(title, description, color, characterName, characterWorld);

        await _plugin.Discord.EditWebhookMessage(channelId, msgId, embedBuilder.Build());
    }

    public static string CleanRaidName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var patterns = new[]
        {
            "AAC Heavyweight",
            "Eden's Promise",
            "Anabaseios",
            "Abyssos",
            "Asphodelos",
            "(Savage)",
            " - "
        };

        var result = name;
        foreach (var pattern in patterns)
        {
            result = result.Replace(pattern, "", StringComparison.OrdinalIgnoreCase);
        }

        return result.Trim();
    }
}
