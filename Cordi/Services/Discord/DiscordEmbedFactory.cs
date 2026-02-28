using System;
using System.Threading.Tasks;
using Cordi.Services.Features;
using DSharpPlus.Entities;

namespace Cordi.Services.Discord;

public class DiscordEmbedFactory
{
    private readonly LodestoneService _lodestone;

    public DiscordEmbedFactory(LodestoneService lodestone)
    {
        _lodestone = lodestone;
    }

    public async Task<DiscordEmbedBuilder> CreatePlayerEmbedBuilderAsync(
        string title,
        string description,
        DiscordColor color,
        string? playerName = null,
        string? playerWorld = null,
        string? footer = null)
    {
        var builder = new DiscordEmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(color)
            .WithTimestamp(DateTime.Now);

        if (!string.IsNullOrEmpty(playerName) && !string.IsNullOrEmpty(playerWorld))
        {
            var avatarUrl = await _lodestone.GetAvatarUrlAsync(playerName, playerWorld);
            if (!string.IsNullOrEmpty(avatarUrl))
                builder.WithThumbnail(avatarUrl);
        }

        if (!string.IsNullOrEmpty(footer))
            builder.WithFooter(footer);

        return builder;
    }

    public DiscordEmbedBuilder CreateEmbedBuilder(
        string title,
        string description,
        DiscordColor color,
        string? thumbnailUrl = null,
        string? footer = null)
    {
        var builder = new DiscordEmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(color)
            .WithTimestamp(DateTime.Now);

        if (!string.IsNullOrEmpty(thumbnailUrl))
            builder.WithThumbnail(thumbnailUrl);

        if (!string.IsNullOrEmpty(footer))
            builder.WithFooter(footer);

        return builder;
    }
}
