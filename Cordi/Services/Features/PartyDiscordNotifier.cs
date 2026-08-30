using System;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Domain;
using Dalamud.Plugin.Services;

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

    public async Task<ulong> SendNotificationAsync(string title, string description, int color, Player? character = null)
    {
        if (!_plugin.Config.Party.DiscordEnabled) return 0;

        var channelIdStr = _plugin.Config.Party.DiscordChannelId;
        if (!ulong.TryParse(channelIdStr, out var channelId)) return 0;

        string? avatarUrl = null;
        if (character is not null)
        {
            avatarUrl = await _plugin.Lodestone.GetAvatarUrlAsync(character);
        }

        var embedBuilder = _plugin.EmbedFactory.CreateEmbedBuilder(title, description, color, avatarUrl);

        var username = character?.FullName ?? "Party Notification";

        return await _plugin.Discord.SendWebhookMessageRaw(channelId, embedBuilder.Build(), username, avatarUrl);
    }

    public async Task UpdateNotificationAsync(ulong msgId, string title, string description, int color, Player? character = null)
    {
        if (!_plugin.Config.Party.DiscordEnabled) return;

        var channelIdStr = _plugin.Config.Party.DiscordChannelId;
        if (!ulong.TryParse(channelIdStr, out var channelId)) return;

        var embedBuilder = await _plugin.EmbedFactory.CreatePlayerEmbedBuilderAsync(title, description, color, character);

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
