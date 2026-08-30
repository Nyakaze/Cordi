using System;
using System.Threading.Tasks;
using Cordi.Domain;
using Cordi.Services.Features;
using Crovus.Factory;

namespace Cordi.Services.Discord;

public class DiscordEmbedFactory
{
    private readonly LodestoneService _lodestone;

    public DiscordEmbedFactory(LodestoneService lodestone)
    {
        _lodestone = lodestone;
    }

    public async Task<EmbedFactory> CreatePlayerEmbedBuilderAsync(
        string title,
        string description,
        int color,
        Player? player = null,
        string? footer = null)
    {
        var builder = EmbedFactory.Create()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(color)
            .WithTimestamp(DateTimeOffset.Now);

        if (player is not null)
        {
            var avatarUrl = await _lodestone.GetAvatarUrlAsync(player);
            if (!string.IsNullOrEmpty(avatarUrl))
                builder.WithThumbnail(avatarUrl);
        }

        if (!string.IsNullOrEmpty(footer))
            builder.WithFooter(footer);

        return builder;
    }

    public EmbedFactory CreateEmbedBuilder(
        string title,
        string description,
        int color,
        string? thumbnailUrl = null,
        string? footer = null)
    {
        var builder = EmbedFactory.Create()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(color)
            .WithTimestamp(DateTimeOffset.Now);

        if (!string.IsNullOrEmpty(thumbnailUrl))
            builder.WithThumbnail(thumbnailUrl);

        if (!string.IsNullOrEmpty(footer))
            builder.WithFooter(footer);

        return builder;
    }
}
