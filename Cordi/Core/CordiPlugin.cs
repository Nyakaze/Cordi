using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cordi.Attributes;
using Cordi.Packets;
using Cordi.Packets.Factory;
using Cordi.Packets.Handler;
using Cordi.Packets.Handler.Chat;
using Cordi.Services;
using Cordi.Services.Activity;
using Cordi.Services.Discord;
using Cordi.Services.Features;

using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Lumina.Data.Files;
using Microsoft.Extensions.DependencyInjection;
using ECommons;

using Cordi.Configuration;
using Cordi.Core.Caching;
using Cordi.Core.Scheduling;
using Cordi.Domain;
using Cordi.Services.Discord.Dispatch;
using Cordi.Services.Discord.Presence;
using Cordi.Services.Discord.Projections;
using Cordi.Services.Discord.Webhooks;
using DiscordConnection = Cordi.Services.Discord.Connection.DiscordConnection;
using Cordi.Services.Discord.Queue;
using Cordi.Services.Chatbox;
using Cordi.Services.Emojis;
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
    public EmoteTitleService EmoteTitles { get; private set; }
    public HonorificBridge HonorificBridge { get; private set; }
    public DiscordSlashCommandService SlashCommandService { get; private set; }
    public ScreenshotService Screenshot { get; private set; }


    static readonly IPluginLog Logger = Service.Log;

    public Cordi.Configuration.Configuration Config = null!;

    private readonly ServiceProvider _provider;

    private readonly ChatRouter _router;
    public NotificationManager NotificationManager { get; private set; }
    public CordiLogService LogService { get; private set; }
    public ICacheRegistry CacheRegistry { get; private set; } = null!;
    public PlayerRegistry Players { get; private set; } = null!;
    public LocalPlayerProvider LocalPlayer { get; private set; } = null!;
    public DiscordConnection DiscordConnection { get; private set; } = null!;
    public DiscordEventPump DiscordDispatcher { get; private set; } = null!;
    public DiscordPresenceWatcher PresenceWatcher { get; private set; } = null!;
    public DiscordChannelProjection Channels { get; private set; } = null!;
    public DiscordSendQueue DiscordSendQueue { get; private set; } = null!;
    public FrameworkScheduler FrameworkScheduler { get; private set; } = null!;
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

    public static bool GposeActive => Service.ClientState.IsGPosing;

    public static bool CutsceneActive =>
        !GposeActive
        && (Service.Condition[ConditionFlag.OccupiedInCutSceneEvent]
            || Service.Condition[ConditionFlag.WatchingCutscene78]);

    public static bool CinematicActive => GposeActive || CutsceneActive;

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
    public AudioService Audio { get; private set; }
    public VisibilityService Visibility { get; private set; }
    public PeeperService Peeper { get; private set; }
    public PeeperWindow PeeperWindow { get; private set; }
    public EmoteLogService EmoteLog { get; private set; }
    public EmoteLogWindow EmoteLogWindow { get; private set; }
    public CombinedWindow CombinedWindow { get; private set; }
    public ChatboxService Chatbox { get; private set; }
    public EmojiTranslator Emoji { get; private set; } = null!;
    public ChatboxWindow ChatboxWindow { get; private set; }
    public ConversationWindowManager ConversationWindows { get; private set; }
    public PartyService PartyService { get; private set; }
    public RememberMeService RememberMe { get; private set; }

    public CordiPlugin()
    {
        Plugin = this;
        ECommonsMain.Init(PluginInterface, this);
        PluginInterface.Create<Service>();
        InitializeConfig();

        _provider = ServiceRegistration.Build(
            this, Config, windowSystem, PluginInterface, CommandManager, ChatGui, Framework, ClientState);

        LogService = _provider.GetRequiredService<CordiLogService>();
        CacheRegistry = _provider.GetRequiredService<ICacheRegistry>();
        Players = _provider.GetRequiredService<PlayerRegistry>();
        Player.Registry = Players;
        LocalPlayer = _provider.GetRequiredService<LocalPlayerProvider>();

        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUI;
        Lodestone = _provider.GetRequiredService<LodestoneService>();
        Tomestone = _provider.GetRequiredService<TomestoneService>();
        Webhook = _provider.GetRequiredService<DiscordWebhookService>();
        AdvertisementFilterService = _provider.GetRequiredService<AdvertisementFilterService>();
        Discord = _provider.GetRequiredService<DiscordHandler>();
        Screenshot = _provider.GetRequiredService<ScreenshotService>();
        SlashCommandService = _provider.GetRequiredService<DiscordSlashCommandService>();
        EmbedFactory = _provider.GetRequiredService<DiscordEmbedFactory>();

        NotificationManager = _provider.GetRequiredService<NotificationManager>();
        Audio = _provider.GetRequiredService<AudioService>();

        Visibility = _provider.GetRequiredService<VisibilityService>();
        EmoteLog = _provider.GetRequiredService<EmoteLogService>();
        Peeper = _provider.GetRequiredService<PeeperService>();
        HonorificBridge = _provider.GetRequiredService<HonorificBridge>();
        PartyService = _provider.GetRequiredService<PartyService>();
        RememberMe = _provider.GetRequiredService<RememberMeService>();
        ActivityManager = _provider.GetRequiredService<ActivityManager>();
        EmoteTitles = _provider.GetRequiredService<EmoteTitleService>();

        DiscordConnection = _provider.GetRequiredService<DiscordConnection>();
        DiscordDispatcher = _provider.GetRequiredService<DiscordEventPump>();
        DiscordDispatcher.Bind();
        PresenceWatcher = _provider.GetRequiredService<DiscordPresenceWatcher>();
        Channels = _provider.GetRequiredService<DiscordChannelProjection>();
        Channels.Bind();
        SlashCommandService.Bind();

        DiscordSendQueue = _provider.GetRequiredService<DiscordSendQueue>();
        DiscordSendQueue.Start();

        FrameworkScheduler = _provider.GetRequiredService<FrameworkScheduler>();

        Emoji = _provider.GetRequiredService<EmojiTranslator>();

        Chatbox = _provider.GetRequiredService<ChatboxService>();

        Cordi.UI.Themes.UiTheme.GlobalAccent = Cordi.UI.Themes.UiTheme.ParseAccent(Config.Appearance.AccentColor);

        configWindow = _provider.GetRequiredService<ConfigWindow>();
        discordWindow = _provider.GetRequiredService<DiscordWindow>();
        PeeperWindow = _provider.GetRequiredService<PeeperWindow>();
        this.EmoteLogWindow = _provider.GetRequiredService<EmoteLogWindow>();
        CombinedWindow = _provider.GetRequiredService<CombinedWindow>();
        ChatboxWindow = _provider.GetRequiredService<ChatboxWindow>();

        windowSystem.AddWindow(discordWindow);
        windowSystem.AddWindow(configWindow);
        windowSystem.AddWindow(PeeperWindow);
        windowSystem.AddWindow(this.EmoteLogWindow);
        windowSystem.AddWindow(CombinedWindow);
        windowSystem.AddWindow(ChatboxWindow);

        ConversationWindows = _provider.GetRequiredService<ConversationWindowManager>();

        PluginInterface.UiBuilder.DisableGposeUiHide = true;
        PluginInterface.UiBuilder.DisableCutsceneUiHide = true;

        PluginInterface.UiBuilder.Draw += DrawUI;

        _chat = _provider.GetRequiredService<ChatMessenger>();

        this.commandManager = _provider.GetRequiredService<CordiCommandManager<CordiPlugin>>();

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


        });

        Task.Run(async () =>
        {
            await Task.Delay(1000);
            if (Service.ClientState.IsLoggedIn) ApplyOpenOnLogin();
        });

        Service.Chat.ChatMessageUnhandled += ChatOnChatMessage;
        Service.Chat.ChatMessage += ChatOnSuppressibleMessage;
        Service.ClientState.Login += OnLoginEvent;
        Service.ClientState.Logout += OnLogoutEvent;

        _router = _provider.GetRequiredService<ChatRouter>();

        UpdateCommandVisibility();

        FrameworkScheduler.Bind();
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
    [HelpMessage("Opens the configuration window for Cordi. Use \"/cordi dm <Name@World>\" to open a direct message conversation.")]
    public void OpenConfigCommand(string command, string args)
    {
        var parts = (args ?? string.Empty).Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length > 0 && string.Equals(parts[0], "dm", StringComparison.OrdinalIgnoreCase))
        {
            OpenConversationCommand(parts.Length > 1 ? parts[1].Trim() : string.Empty);
            return;
        }

        configWindow.Toggle();
    }

    private void OpenConversationCommand(string target)
    {
        if (!Config.Chatbox.Enabled || !Config.Chatbox.Conversations.Enabled)
        {
            ChatGui.PrintError("[Cordi] Direct message conversations are disabled.");
            return;
        }

        var split = target.Split('@', 2);
        var name = split[0].Trim();
        var world = split.Length > 1 ? split[1].Trim() : string.Empty;

        if (name.Length == 0)
        {
            ChatGui.PrintError("[Cordi] Usage: /cordi dm <Name@World>");
            return;
        }

        if (Chatbox?.OpenConversationFor(name, world) == null)
            ChatGui.PrintError($"[Cordi] Could not open a conversation with {target}.");
    }

    [Command("/cordidebug")]
    [HelpMessage("Dumps all player characters in ObjectTable for debugging")]
    public unsafe void DebugCommand(string command, string args)
    {
        Service.Log.Info("--- Player Characters in ObjectTable ---");
        foreach (var obj in Service.ObjectTable)
        {
            if (obj is not IPlayerCharacter player) continue;
            var character = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)player.Address;
            var renderFlags = character != null ? character->GameObject.RenderFlags : 0;
            var rawTargetId = character != null ? (ulong)character->TargetId : 0UL;
            Service.Log.Info($"Player: {player.Name}@{player.HomeWorld.Value.Name}, GameObjectId: {player.GameObjectId:X}, TargetObjectId: {player.TargetObjectId:X}, RawTargetId: {rawTargetId:X}, ContentId: {(character != null ? character->ContentId : 0):X}, RenderFlags: {renderFlags}");
        }
        Service.Log.Info("---------------------------------------");
        Visibility.DumpDebugInfo();
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

        const string ChatboxCmd = "/cordichat";
        bool chatboxEnabled = Config.Chatbox.Enabled;
        bool chatboxRegistered = CommandManager.Commands.ContainsKey(ChatboxCmd);

        if (chatboxEnabled && !chatboxRegistered)
        {
            CommandManager.AddHandler(ChatboxCmd, new CommandInfo((cmd, args) =>
            {
                ChatboxWindow.IsOpen = !ChatboxWindow.IsOpen;
            })
            {
                HelpMessage = "Toggles the Chatbox Window"
            });
        }
        else if (!chatboxEnabled && chatboxRegistered)
        {
            CommandManager.RemoveHandler(ChatboxCmd);
        }

        const string PeepCmd = "/cordipeeper";
        bool peepEnabled = Config.Peeper.WindowEnabled;
        bool peepRegistered = CommandManager.Commands.ContainsKey(PeepCmd);

        if (peepEnabled && !peepRegistered)
        {
            CommandManager.AddHandler(PeepCmd, new CommandInfo((cmd, args) =>
            {
                PeeperWindow.IsOpen = !PeeperWindow.IsOpen;
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
        bool comboEnabled = Config.CombinedWindow.Enabled;
        bool comboRegistered = CommandManager.Commands.ContainsKey(ComboCmd);

        if (comboEnabled && !comboRegistered)
        {
            CommandManager.AddHandler(ComboCmd, new CommandInfo((cmd, args) =>
            {
                CombinedWindow.IsOpen = !CombinedWindow.IsOpen;
            })
            {
                HelpMessage = "Toggles the Combined Emote Log & Peeper Window"
            });
        }
        else if (!comboEnabled && comboRegistered)
        {
            CommandManager.RemoveHandler(ComboCmd);
            CombinedWindow.IsOpen = false;
        }
    }

    public void OnFrameworkUpdate(IFramework framework)
    {
        cachedLocalPlayer = Service.ObjectTable.LocalPlayer;
        Visibility?.OnFrameworkUpdate();
        Chatbox?.OnFrameworkUpdate();
        EmoteTitles?.OnFrameworkUpdate();

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

    private void ApplyOpenOnLogin()
    {
        if (Config == null) return;

        if (Config.Peeper.OpenOnLogin) PeeperWindow.IsOpen = true;
        if (Config.EmoteLog.WindowOpenOnLogin) EmoteLogWindow.IsOpen = true;
        if (Config.CombinedWindow.Enabled && Config.CombinedWindow.OpenOnLogin) CombinedWindow.IsOpen = true;
        if (Config.Chatbox.Enabled && Config.Chatbox.OpenOnLogin) ChatboxWindow.IsOpen = true;
    }

    private async void OnLoginEvent()
    {
        LogService.Info("Plugin", "Player logged in");
        ApplyOpenOnLogin();
        cachedLocalPlayer = await Service.Framework.RunOnFrameworkThread(() => Service.ObjectTable.LocalPlayer);
    }
    private async void OnLogoutEvent(int type, int code)
    {
        LogService.Info("Plugin", "Player logged out");
        cachedLocalPlayer = null;
        EmoteTitles?.OnLogout();
    }

    private void ChatOnChatMessage(Dalamud.Game.Chat.IChatMessage message) => HandleChatMessage(BuildChatMessage(message));

    private void ChatOnSuppressibleMessage(Dalamud.Game.Chat.IHandleableChatMessage message)
    {
        if (Chatbox == null || Config?.Chatbox is not { Enabled: true }) return;
        if (message.LogKind is not (XivChatType.TellIncoming or XivChatType.TellOutgoing)) return;

        var msg = BuildChatMessage(message);
        var (name, world) = ChatboxService.SenderOf(msg);

        if (!Chatbox.SuppressesGameLog(message.LogKind, name, world)) return;

        HandleChatMessage(msg);
        message.PreventOriginal();
    }

    private static ChatMessage BuildChatMessage(Dalamud.Game.Chat.IChatMessage message) => new()
    {
        ChatType = message.LogKind,
        Message = message.Message,
        Sender = message.Sender,
        SenderName = message.Sender.TextValue,
        SenderWorld = ""
    };

    private void HandleChatMessage(ChatMessage msg)
    {
        if (Config.MappingCache.ContainsKey(msg.ChatType))
        {
            LogService.Debug("ChatRouter", $"[{msg.ChatType}] {msg.SenderName}: {msg.Message.TextValue}");
        }
        if (Config.Chatbox.Enabled) Chatbox?.IngestGameMessage(msg);
        EmoteLog?.HandleCustomEmote(msg);

        if (msg.ChatType == XivChatType.RetainerSale) return;

        _router.RouteAsync(msg, Discord);

    }

    #region IDisposable Support
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        PluginInterface.UiBuilder.Draw -= DrawUI;

        Service.PluginInterface.UiBuilder.OpenConfigUi -= this.ToggleConfigUI;
        Service.PluginInterface.UiBuilder.OpenMainUi -= this.ToggleConfigUI;

        Service.Chat.ChatMessageUnhandled -= ChatOnChatMessage;
        Service.Chat.ChatMessage -= ChatOnSuppressibleMessage;
        Service.ClientState.Login -= OnLoginEvent;
        Service.ClientState.Logout -= OnLogoutEvent;

        this.Discord?.Stop();

        _provider?.DisposeAsync().AsTask().GetAwaiter().GetResult();

        try
        {
            this.Config?.Save();
        }
        catch (Exception ex)
        {
            LogService?.Error("Plugin", "Failed to save configuration during shutdown", ex);
        }
    }

    public void Dispose()
    {

        Dispose(true);
        ECommonsMain.Dispose();
        GC.SuppressFinalize(this);
    }
    #endregion
}


