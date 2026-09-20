using System;
using Cordi.Core.Caching;
using Cordi.Core.Scheduling;
using Cordi.Domain;
using Cordi.Packets.Factory;
using Cordi.Packets.Handler.Chat;
using Cordi.Services;
using Cordi.Services.Activity;
using Cordi.Services.Chatbox;
using Cordi.Services.Discord;
using Cordi.Services.Discord.Dispatch;
using Cordi.Services.Discord.Presence;
using Cordi.Services.Discord.Projections;
using Cordi.Services.Discord.Send;
using Cordi.Services.Discord.Queue;
using Cordi.Services.Discord.Webhooks;
using Cordi.Services.Emojis;
using Cordi.Services.Features;
using Cordi.UI.Windows;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using DiscordConnection = Cordi.Services.Discord.Connection.DiscordConnection;

namespace Cordi.Core;

internal static class ServiceRegistration
{
    public static ServiceProvider Build(
        CordiPlugin plugin,
        Configuration.Configuration config,
        WindowSystem windowSystem,
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IChatGui chatGui,
        IFramework framework,
        IClientState clientState)
    {
        var configDirectory = pluginInterface.ConfigDirectory.FullName;
        var services = new ServiceCollection();

        services.AddSingleton(plugin);
        services.AddSingleton(config);
        services.AddSingleton(windowSystem);

        services.AddSingleton<CordiLogService>();
        services.AddSingleton<ICacheRegistry, CacheRegistry>();
        services.AddSingleton<PlayerRegistry>();
        services.AddSingleton<LocalPlayerProvider>();
        services.AddSingleton<NotificationManager>();
        services.AddSingleton<ScreenshotService>();
        services.AddSingleton(_ => new HonorificBridge(pluginInterface));

        services.AddSingleton(_ => new ChatMessenger(chatGui, framework, clientState, commandManager)
        {
            MinInterval = TimeSpan.FromSeconds(1.2),
            MaxLength = 450,
        });

        services.AddSingleton<AudioService>();
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<LodestoneService>(sp, configDirectory));
        services.AddSingleton<TomestoneService>();
        services.AddSingleton<DiscordEmbedFactory>();
        services.AddSingleton<DiscordConnection>();
        services.AddSingleton<AdvertisementFilterService>();
        services.AddSingleton<DiscordWebhookService>();
        services.AddSingleton<DiscordChannelProjection>();
        services.AddSingleton(sp => new DiscordSendQueue(sp.GetRequiredService<CordiLogService>()));
        services.AddSingleton<DiscordSender>();
        services.AddSingleton<DiscordMessageRouter>();
        services.AddSingleton<DiscordHandler>();
        services.AddSingleton<DiscordEventPump>();
        services.AddSingleton<DiscordPresenceWatcher>();
        services.AddSingleton<DiscordSlashCommandService>();

        services.AddSingleton<RememberMeService>();
        services.AddSingleton<ActivityManager>();
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<EmoteTitleService>(sp, configDirectory));
        services.AddSingleton<EmoteBackAction>();
        services.AddSingleton<EmoteDiscordNotifier>();
        services.AddSingleton<EmoteLogService>();
        services.AddSingleton<CordiPeepService>();
        services.AddSingleton<PartyDiscordNotifier>();
        services.AddSingleton<PartyService>();

        services.AddSingleton(_ => new EmojiTranslator(
            new GuildEmoteCache(() => plugin.Channels?.Guilds),
            () => plugin.Chatbox?.Emotes,
            () => config.Chatbox.UseEmoticonsInGameChat));

        services.AddSingleton<ChatboxService>();
        services.AddSingleton(sp => new Lazy<ChatboxService>(sp.GetRequiredService<ChatboxService>));
        services.AddSingleton(sp => new Lazy<ChatboxWindow>(sp.GetRequiredService<ChatboxWindow>));
        services.AddSingleton(sp => new Lazy<ConversationWindowManager>(sp.GetRequiredService<ConversationWindowManager>));
        services.AddSingleton(sp => new Lazy<CordiPeepWindow>(sp.GetRequiredService<CordiPeepWindow>));
        services.AddSingleton(sp => new Lazy<EmoteLogWindow>(sp.GetRequiredService<EmoteLogWindow>));

        services.AddSingleton<ConfigWindow>();
        services.AddSingleton<DiscordWindow>();
        services.AddSingleton<CordiPeepWindow>();
        services.AddSingleton<EmoteLogWindow>();
        services.AddSingleton<CombinedWindow>();
        services.AddSingleton<ChatboxWindow>();
        services.AddSingleton<ConversationWindowManager>();

        services.AddSingleton<FrameworkScheduler>();
        services.AddSingleton(_ => new CordiCommandManager<CordiPlugin>(plugin, commandManager));

        services.AddSingleton<IChatHandlerFactory, AttributeChatHandlerFactory>();
        services.AddSingleton<ChatRouter>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = false,
            ValidateScopes = false,
        });
    }
}
