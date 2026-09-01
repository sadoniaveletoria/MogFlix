using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using MogFlix.Integrations;
using MogFlix.Services;
using MogFlix.Windows;

namespace MogFlix;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static INamePlateGui NamePlateGui { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IContextMenu ContextMenu { get; private set; } = null!;

    public Configuration Configuration { get; }
    public PlexService PlexService { get; }
    public DtrBarService DtrBarService { get; }
    public HonorificService HonorificService { get; }
    public KosmiJoinService KosmiJoinService { get; }
    public PresenceService PresenceService { get; }
    public BrowseWindow BrowseWindow { get; }
    public WindowSystem WindowSystem = new("MogFlix");

    private ConfigWindow ConfigWindow { get; }
    private IncomingRequestWindow IncomingRequestWindow { get; }

    private const string CommandName = "/mogflix";

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        PlexService = new PlexService(Configuration, Log);
        DtrBarService = new DtrBarService(DtrBar, Framework, PlexService, Configuration);
        HonorificService = new HonorificService(PluginInterface, ObjectTable, Framework, NamePlateGui, ClientState, Condition, Log, PlexService, Configuration);
        KosmiJoinService = new KosmiJoinService(ContextMenu, ObjectTable, Log, Configuration);
        PresenceService = new PresenceService(Configuration, ObjectTable, PlexService, Log);

        ConfigWindow = new ConfigWindow(this);
        BrowseWindow = new BrowseWindow(this);
        IncomingRequestWindow = new IncomingRequestWindow(this);
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(BrowseWindow);
        WindowSystem.AddWindow(IncomingRequestWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open MogFlix settings. '/mogflix join' opens your Kosmi room, '/mogflix browse' shows who's watching.",
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += () => ConfigWindow.IsOpen = true;
        PluginInterface.UiBuilder.OpenMainUi += () => ConfigWindow.IsOpen = true;

        IncomingRequestWindow.IsOpen = true; // visibility is gated by DrawConditions, not this

        PlexService.Start();
        PresenceService.Start();
    }

    public void OpenBrowseWindow() => BrowseWindow.IsOpen = true;

    private void OnCommand(string command, string args)
    {
        var arg = args.Trim();

        if (arg.Equals("join", StringComparison.OrdinalIgnoreCase))
        {
            KosmiJoinService.OpenKosmiLink();
        }
        else if (arg.Equals("browse", StringComparison.OrdinalIgnoreCase))
        {
            OpenBrowseWindow();
        }
        else
        {
            ConfigWindow.IsOpen = true;
        }
    }

    public void Dispose()
    {
        PlexService.Dispose();
        DtrBarService.Dispose();
        HonorificService.Dispose();
        KosmiJoinService.Dispose();
        PresenceService.Dispose();

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        BrowseWindow.Dispose();
        IncomingRequestWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }
}
