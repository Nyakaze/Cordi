using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Domain;

namespace Cordi.Services.Features;

public class PartyDiscordNotifier
{
    private readonly Configuration.Configuration _config;
    private readonly Discord.DiscordHandler _discord;
    private readonly Discord.DiscordEmbedFactory _embeds;
    private readonly LodestoneService _lodestone;

    public PartyDiscordNotifier(
        Configuration.Configuration config,
        Discord.DiscordHandler discord,
        Discord.DiscordEmbedFactory embeds,
        LodestoneService lodestone)
    {
        _config = config;
        _discord = discord;
        _embeds = embeds;
        _lodestone = lodestone;
    }

    public async Task<ulong> SendNotificationAsync(string title, string description, int color, Player? character = null)
    {
        if (!_config.Party.DiscordEnabled) return 0;

        var channelIdStr = _config.Party.DiscordChannelId;
        if (!ulong.TryParse(channelIdStr, out var channelId)) return 0;

        string? avatarUrl = null;
        if (character is not null)
        {
            avatarUrl = await _lodestone.GetAvatarUrlAsync(character);
        }

        var embedBuilder = _embeds.CreateEmbedBuilder(title, description, color, avatarUrl);

        var username = character?.FullName ?? "Party Notification";

        return await _discord.SendWebhookMessageRaw(channelId, embedBuilder.Build(), username, avatarUrl);
    }

    public async Task<ulong> SendSummaryAsync(string title, string description, int color,
        IReadOnlyList<(string Name, string Value)> fields, string username)
    {
        if (!_config.Party.DiscordEnabled) return 0;

        var channelIdStr = _config.Party.DiscordChannelId;
        if (!ulong.TryParse(channelIdStr, out var channelId)) return 0;

        var embedBuilder = _embeds.CreateEmbedBuilder(title, description, color);

        foreach (var field in fields)
            embedBuilder.AddField(field.Name, field.Value, inline: true);

        return await _discord.SendWebhookMessageRaw(channelId, embedBuilder.Build(), username, null);
    }

    public async Task UpdateNotificationAsync(ulong msgId, string title, string description, int color, Player? character = null)
    {
        if (!_config.Party.DiscordEnabled) return;

        var channelIdStr = _config.Party.DiscordChannelId;
        if (!ulong.TryParse(channelIdStr, out var channelId)) return;

        var embedBuilder = await _embeds.CreatePlayerEmbedBuilderAsync(title, description, color, character);

        await _discord.EditWebhookMessage(channelId, msgId, embedBuilder.Build());
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
