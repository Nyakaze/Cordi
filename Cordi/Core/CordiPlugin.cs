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
using Cordi.Core.Caching;
using Cordi.Core.Scheduling;
using Cordi.Domain;
using Cordi.Services.Discord.Dispatch;
using Cordi.Services.Discord.Queue;
using Cordi.Services.Observations;
using Cordi.Services.Storage;
using Cordi.UI.Windows;
using Newtonsoft.Json;

namespace Cordi.Core;

public class CordiPlugin : IDalamudPlugin
{
    public static CordiPlugin Plugin { get; private set; }
    private readonly CordiCommandManager<CordiPlugin> commandManager;
    private readonly ConfigWindow configWindow;
    public ConfigWindow MainConfigWindow => configWindow;
    public readonly DiscordWindow discordWindow;
    public DiscordHandler Discord { get; set; }
    public LodestoneService Lodestone { get; private set; }
    public TomestoneService Tomestone { get; private set; }
    public DiscordWebhookService Webhook { get; private set; }
    public DiscordEmbedFactory EmbedFactory { get; private set; }
    public AdvertisementFilterService AdvertisementFilterService { get; private set; }
    public ActivityManager ActivityManager { get; private set; }
    public HonorificBridge HonorificBridge { get; private set; }
    public DiscordChannelCache ChannelCache { get; private set; }
    public DiscordSlashCommandService SlashCommandService { get; private set; }
    public ScreenshotService Screenshot { get; private set; }


    static readonly IPluginLog Logger = Service.Log;

    public Cordi.Configuration.Configuration Config = null!;

    public ServiceCollection Services = new();

    private readonly ChatRouter _router;
    public NotificationManager NotificationManager { get; private set; }
    public CordiLogService LogService { get; private set; }
    public ICacheRegistry CacheRegistry { get; private set; } = null!;
    public LocalPlayerProvider LocalPlayer { get; private set; } = null!;
    public PlayerTrackingService PlayerTracker { get; private set; } = null!;
    public PlayerObservationDispatcher PlayerObservations { get; private set; } = null!;
    public NearbyPlayerScanner NearbyScanner { get; private set; } = null!;
    public DiscordEventDispatcher DiscordDispatcher { get; private set; } = null!;
    public DiscordSendQueue DiscordSendQueue { get; private set; } = null!;
    public FrameworkScheduler FrameworkScheduler { get; private set; } = null!;
    private IPlayerTrackingStorage _playerTrackingStorage = null!;
    public bool IsLogsTabVisible
    {
        get => Config?.LogsTabVisible ?? false;
        set
        {
            if (Config != null)
            {
                Config.LogsTabVisible = value;
                Config.Save();
            }
        }
    }
    private bool _prevComboPressed;

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
    public PlayerDetailWindow PlayerDetailWindow { get; private set; } = null!;
    public EmoteLogService EmoteLog { get; private set; }
    public EmoteLogWindow EmoteLogWindow { get; private set; }
    public CombinedWindow CombinedWindow { get; private set; }
    public PartyService PartyService { get; private set; }
    public RememberMeService RememberMe { get; private set; }

    public CordiPlugin()
    {
        Plugin = this;
        ECommonsMain.Init(PluginInterface, this);
        PluginInterface.Create<Service>();
        InitializeConfig();

        LogService = new CordiLogService();
        CacheRegistry = new CacheRegistry();
        LocalPlayer = new LocalPlayerProvider();

        var trackerDbPath = Path.Combine(PluginInterface.ConfigDirectory.FullName, "tracked-players.db");
        _playerTrackingStorage = new LitePlayerTrackingStorage(trackerDbPath);
        PlayerTracker = new PlayerTrackingService(this, _playerTrackingStorage, CacheRegistry);
        PlayerTracker.MigrateFromRememberedPlayers(Config.RememberMe.RememberedPlayers);

        PlayerObservations = new PlayerObservationDispatcher(this);
        NearbyScanner = new NearbyPlayerScanner(this);

        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUI;
        Lodestone = new LodestoneService(this);
        Tomestone = new TomestoneService(this);
        Webhook = new DiscordWebhookService(this);
        ChannelCache = new DiscordChannelCache(this);
        AdvertisementFilterService = new AdvertisementFilterService(this, Webhook);
        Discord = new DiscordHandler(this, Webhook, AdvertisementFilterService);
        Screenshot = new ScreenshotService();
        SlashCommandService = new DiscordSlashCommandService(this, Screenshot);
        EmbedFactory = new DiscordEmbedFactory(Lodestone);

        NotificationManager = new NotificationManager();



        EmoteLog = new EmoteLogService(this);
        CordiPeep = new CordiPeepService(this);
        HonorificBridge = new HonorificBridge(PluginInterface);
        PartyService = new PartyService(this, NotificationManager);
        RememberMe = new RememberMeService(this);
        ActivityManager = new ActivityManager(this, HonorificBridge);

        DiscordDispatcher = new DiscordEventDispatcher(this);
        DiscordDispatcher.Bind();

        DiscordSendQueue = new DiscordSendQueue(this);
        DiscordSendQueue.Start();

        FrameworkScheduler = new FrameworkScheduler(this);
        FrameworkScheduler.Bind();

        configWindow = new ConfigWindow(this);
        discordWindow = new DiscordWindow(this);
        CordiPeepWindow = new CordiPeepWindow(this);
        this.EmoteLogWindow = new EmoteLogWindow(this);
        CombinedWindow = new CombinedWindow(this);
        PlayerDetailWindow = new PlayerDetailWindow(this);

        windowSystem.AddWindow(discordWindow);
        windowSystem.AddWindow(configWindow);
        windowSystem.AddWindow(CordiPeepWindow);
        windowSystem.AddWindow(this.EmoteLogWindow);
        windowSystem.AddWindow(PlayerDetailWindow);
        windowSystem.AddWindow(CombinedWindow);

        PluginInterface.UiBuilder.Draw += DrawUI;

        _chat = new ChatMessenger(ChatGui, Framework, ClientState, CommandManager)
        {
            MinInterval = TimeSpan.FromSeconds(1.2),
            MaxLength = 450
        };


        this.commandManager = new CordiCommandManager<CordiPlugin>(this, CommandManager);

        LogService.Info("Plugin", "Cordi plugin loading");
        Task.Run(async () =>
        {
            LogService.Info("Plugin", "Initializing services...");
            await Lodestone.InitializeAsync();
            LogService.Debug("Lodestone", "Initialized");
            await Tomestone.InitializeAsync();
            LogService.Debug("Tomestone", "Initialized");
            await this.Discord.Start();
            LogService.Info("Discord", "Bot started");
            this.EmoteLog.Initialize();
            LogService.Debug("EmoteLog", "Initialized");


            await Task.Delay(1000);
            if (Service.ClientState.IsLoggedIn)
            {
                if (Config!.CordiPeep.OpenOnLogin) CordiPeepWindow.IsOpen = true;
                if (Config!.EmoteLog.WindowOpenOnLogin) this.EmoteLogWindow.IsOpen = true;
                if (Config!.CombinedWindow.OpenOnLogin) CombinedWindow.IsOpen = true;
            }
        });

        Service.Chat.ChatMessage += ChatOnChatMessage;
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

        const string ComboCmd = "/cordicombo";
        bool comboRegistered = CommandManager.Commands.ContainsKey(ComboCmd);

        if (!comboRegistered)
        {
            CommandManager.AddHandler(ComboCmd, new CommandInfo((cmd, args) =>
            {
                CombinedWindow.IsOpen = !CombinedWindow.IsOpen;
            })
            {
                HelpMessage = "Toggles the Combined Emote Log & Peeper Window"
            });
        }
    }

    public void OnFrameworkUpdate(IFramework framework)
    {
        cachedLocalPlayer = Service.ObjectTable.LocalPlayer;

        // Ctrl+Shift+L toggles hidden Logs tab (edge-triggered, only when config window is open)
        bool ctrl = Service.KeyState[0x11];   // VK_CONTROL
        bool shift = Service.KeyState[0x10];  // VK_SHIFT
        bool l = Service.KeyState[0x4C];      // VK_L
        bool comboPressed = ctrl && shift && l && configWindow.IsOpen;

        if (comboPressed && !_prevComboPressed)
        {
            IsLogsTabVisible = !IsLogsTabVisible;
            LogService.Info("Plugin", IsLogsTabVisible ? "Logs tab revealed" : "Logs tab hidden");
        }
        _prevComboPressed = comboPressed;
    }

    private async void OnLoginEvent()
    {
        LogService.Info("Plugin", "Player logged in");
        cachedLocalPlayer = await Service.Framework.RunOnFrameworkThread(() => Service.ObjectTable.LocalPlayer);
        if (Config.CordiPeep.OpenOnLogin)
        {
            CordiPeepWindow.IsOpen = true;
        }
        if (Config.EmoteLog.WindowOpenOnLogin)
        {
            EmoteLogWindow.IsOpen = true;
        }
        if (Config.CombinedWindow.OpenOnLogin)
        {
            CombinedWindow.IsOpen = true;
        }
    }
    private async void OnLogoutEvent(int type, int code)
    {
        LogService.Info("Plugin", "Player logged out");
        cachedLocalPlayer = null;
    }

    private void ChatOnChatMessage(Dalamud.Game.Chat.IHandleableChatMessage message)
    {
        if (message.IsHandled) return;

        if (message.LogKind == XivChatType.RetainerSale) return;

        var msg = new ChatMessage
        {
            ChatType = message.LogKind,
            Message = message.Message,
            Sender = message.Sender,
            SenderName = message.Sender.TextValue,
            SenderWorld = ""
        };
        if (Config.MappingCache.ContainsKey(message.LogKind))
        {
            LogService.Debug("ChatRouter", $"[{message.LogKind}] {msg.SenderName}: {msg.Message.TextValue}");
        }
        _router.RouteAsync(msg, Discord);

    }

    #region IDisposable Support
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        // Unregister draw callback FIRST to stop all ImGui rendering immediately
        PluginInterface.UiBuilder.Draw -= DrawUI;

        this.SlashCommandService?.Dispose();
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
        this.FrameworkScheduler?.Dispose();
        this.NearbyScanner?.Dispose();
        this.DiscordDispatcher?.Dispose();
        this.DiscordSendQueue?.Dispose();
        this.LocalPlayer?.Dispose();
        this.PlayerTracker?.Dispose();
        this._playerTrackingStorage?.Dispose();

        Service.PluginInterface.UiBuilder.OpenConfigUi -= this.ToggleConfigUI;
        Service.PluginInterface.UiBuilder.OpenMainUi -= this.ToggleConfigUI;

        // Unregister framework/chat event handlers
        Service.Chat.ChatMessage -= ChatOnChatMessage;
        Service.ClientState.Login -= OnLoginEvent;
        Service.ClientState.Logout -= OnLogoutEvent;

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


