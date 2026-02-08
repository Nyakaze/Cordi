using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Cordi.Attributes;
using Cordi.Packets;
using Cordi.Packets.Factory;
using Cordi.Packets.Handler;
using Cordi.Packets.Handler.Chat;
using Cordi.Services;
using Cordi.Services.Discord;
using Cordi.Services.Features;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.SlashCommands;
using Lumina.Data.Files;
using Microsoft.Extensions.DependencyInjection;
using ECommons;

using Cordi.Configuration;
using Cordi.UI.Windows;
using Newtonsoft.Json;

namespace Cordi.Core;

public class CordiPlugin : IDalamudPlugin
{
    public static CordiPlugin Plugin { get; private set; }
    private readonly CordiCommandManager<CordiPlugin> commandManager;
    private readonly ConfigWindow configWindow;
    public readonly DiscordWindow discordWindow;
    public DiscordHandler Discord { get; set; }
    public LodestoneService Lodestone { get; private set; }
    public TomestoneService Tomestone { get; private set; }
    public DiscordWebhookService Webhook { get; private set; }
    public ActivityManager ActivityManager { get; private set; }
    public HonorificBridge HonorificBridge { get; private set; }
    public DiscordChannelCache ChannelCache { get; private set; }


    static readonly IPluginLog Logger = Service.Log;

    public Cordi.Configuration.Configuration Config = null!;

    public ServiceCollection Services = new();

    private readonly ChatRouter _router;
    public NotificationManager NotificationManager { get; private set; }


    public IPlayerCharacter cachedLocalPlayer;


    public string Name => "Cordi";
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("Cordi");

    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;

    public ChatMessenger _chat = null!;
    public CordiPeepService CordiPeep { get; private set; }
    public CordiPeepWindow CordiPeepWindow { get; private set; }
    public EmoteLogService EmoteLog { get; private set; }
    public EmoteLogWindow EmoteLogWindow { get; private set; }
    public PartyService PartyService { get; private set; }
    public RememberMeService RememberMe { get; private set; }

    public CordiPlugin()
    {
        Plugin = this;
        ECommonsMain.Init(PluginInterface, this);
        PluginInterface.Create<Service>();
        InitializeConfig();

        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUI;
        Lodestone = new LodestoneService(this);
        Tomestone = new TomestoneService(this);
        Webhook = new DiscordWebhookService(this);
        ChannelCache = new DiscordChannelCache(this);
        Discord = new DiscordHandler(this, Webhook);

        NotificationManager = new NotificationManager();



        EmoteLog = new EmoteLogService(this);
        CordiPeep = new CordiPeepService(this);
        HonorificBridge = new HonorificBridge(PluginInterface);
        PartyService = new PartyService(this, NotificationManager);
        RememberMe = new RememberMeService(this);
        ActivityManager = new ActivityManager(this, Discord, HonorificBridge);

        configWindow = new ConfigWindow(this);
        discordWindow = new DiscordWindow(this);
        CordiPeepWindow = new CordiPeepWindow(this);
        this.EmoteLogWindow = new EmoteLogWindow(this);

        windowSystem.AddWindow(discordWindow);
        windowSystem.AddWindow(configWindow);
        windowSystem.AddWindow(CordiPeepWindow);
        windowSystem.AddWindow(this.EmoteLogWindow);

        PluginInterface.UiBuilder.Draw += DrawUI;

        _chat = new ChatMessenger(ChatGui, Framework, ClientState, CommandManager)
        {
            MinInterval = TimeSpan.FromSeconds(1.2),
            MaxLength = 450
        };


        this.commandManager = new CordiCommandManager<CordiPlugin>(this, CommandManager);

        Task.Run(async () =>
        {
            await Lodestone.InitializeAsync();
            await Tomestone.InitializeAsync();
            await this.Discord.Start();
            this.EmoteLog.Initialize();


            await Task.Delay(1000);
            if (Service.ClientState.IsLoggedIn)
            {
                if (Config!.CordiPeep.OpenOnLogin) CordiPeepWindow.IsOpen = true;
                if (Config!.EmoteLog.WindowOpenOnLogin) this.EmoteLogWindow.IsOpen = true;
            }
        });

        Service.Chat.ChatMessage += ChatOnChatMessage;
        Service.Framework.Update += OnFrameworkUpdate;
        Service.ClientState.Login += OnLoginEvent;
        Service.ClientState.Logout += OnLogoutEvent;

        Services.AddSingleton<IChatHandlerFactory, AttributeChatHandlerFactory>();
        Services.AddSingleton<ChatRouter>();
        var provider = Services.BuildServiceProvider();
        _router = provider.GetRequiredService<ChatRouter>();


        UpdateCommandVisibility();
    }

    private void InitializeConfig()
    {
        var oldConfig = PluginInterface.ConfigFile;
        var newConfigDir = PluginInterface.ConfigDirectory;
        var newConfigFile = Path.Combine(newConfigDir.FullName, "Config.json");

        if (!newConfigDir.Exists)
        {
            newConfigDir.Create();
        }

        if (oldConfig.Exists && !File.Exists(newConfigFile))
        {
            try
            {
                oldConfig.MoveTo(newConfigFile);
                Logger.Info($"Migrated config from {oldConfig.FullName} to {newConfigFile}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to migrate config file.");
            }
        }

        if (File.Exists(newConfigFile))
        {
            try
            {
                var json = File.ReadAllText(newConfigFile);
                Config = JsonConvert.DeserializeObject<Cordi.Configuration.Configuration>(json) ?? new Cordi.Configuration.Configuration();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load config file.");
            }
        }

        Config ??= new Cordi.Configuration.Configuration();
        Config.Initialize(PluginInterface);
    }

    private void DrawUI()
    {

        windowSystem.Draw();
        NotificationManager.Draw();
    }

    public void OpenConfigUi()
    {
        configWindow.IsOpen = true;
    }
    public void ToggleConfigUi()
    {
        configWindow.IsOpen = !configWindow.IsOpen;
    }

    [Command("/cordi")]
    [HelpMessage("Opens the configuration window for Cordi")]
    public void OpenConfigCommand(string command, string args)
    {
        configWindow.Toggle();
    }

    public void ToggleConfigUI() => configWindow.Toggle();

    public void UpdateCommandVisibility()
    {
        const string ElCmd = "/cordiel";
        bool elEnabled = Config.EmoteLog.WindowEnabled;
        bool elRegistered = CommandManager.Commands.ContainsKey(ElCmd);

        if (elEnabled && !elRegistered)
        {
            CommandManager.AddHandler(ElCmd, new CommandInfo((cmd, args) =>
            {
                EmoteLogWindow.IsOpen = !EmoteLogWindow.IsOpen;
            })
            {
                HelpMessage = "Toggles the Emote Log Window"
            });
        }
        else if (!elEnabled && elRegistered)
        {
            CommandManager.RemoveHandler(ElCmd);
        }

        const string PeepCmd = "/cordipeeper";
        bool peepEnabled = Config.CordiPeep.WindowEnabled;
        bool peepRegistered = CommandManager.Commands.ContainsKey(PeepCmd);

        if (peepEnabled && !peepRegistered)
        {
            CommandManager.AddHandler(PeepCmd, new CommandInfo((cmd, args) =>
            {
                CordiPeepWindow.IsOpen = !CordiPeepWindow.IsOpen;
            })
            {
                HelpMessage = "Toggles the Cordi Peeper Window"
            });
        }
        else if (!peepEnabled && peepRegistered)
        {
            CommandManager.RemoveHandler(PeepCmd);
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {

        cachedLocalPlayer = Service.ClientState.LocalPlayer;
    }

    private async void OnLoginEvent()
    {
        cachedLocalPlayer = await Service.Framework.RunOnFrameworkThread(() => Service.ClientState.LocalPlayer);
        if (Config.CordiPeep.OpenOnLogin)
        {
            CordiPeepWindow.IsOpen = true;
        }
        if (Config.EmoteLog.WindowOpenOnLogin)
        {
            EmoteLogWindow.IsOpen = true;
        }
    }
    private async void OnLogoutEvent(int type, int code)
    {
        cachedLocalPlayer = null;
    }

    private void ChatOnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool ishandled)
    {
        if (ishandled) return;

        if (type == XivChatType.RetainerSale) return;

        var msg = new ChatMessage
        {
            ChatType = type,
            Message = message,
            Sender = sender,
            SenderName = sender.TextValue,
            SenderWorld = ""
        };
        _router.RouteAsync(msg, Discord);

    }

    #region IDisposable Support
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        this.Discord?.Stop();


        this.commandManager.Dispose();
        this.CordiPeep?.Dispose();
        this.EmoteLog?.Dispose();
        this.ActivityManager?.Dispose();
        this.HonorificBridge?.Dispose();
        this.Lodestone?.Dispose();
        this.Tomestone?.Dispose();
        this.PartyService?.Dispose();
        this.RememberMe?.Dispose();

        Service.PluginInterface.UiBuilder.OpenConfigUi -= this.ToggleConfigUI;
        Service.PluginInterface.UiBuilder.OpenMainUi -= this.ToggleConfigUI;

        this.Config?.Save();

        Service.PluginInterface.UiBuilder.Draw -= configWindow.Draw;


    }

    public void Dispose()
    {

        Dispose(true);
        ECommonsMain.Dispose();
        GC.SuppressFinalize(this);
    }
    #endregion
}

internal interface IPacketHandlerAsync<T> { }


