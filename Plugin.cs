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
    public WindowSystem WindowSystem = new("MogFlix");

    private ConfigWindow ConfigWindow { get; }

    private const string CommandName = "/mogflix";

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        PlexService = new PlexService(Configuration, Log);
        DtrBarService = new DtrBarService(DtrBar, Framework, PlexService, Configuration);
        HonorificService = new HonorificService(PluginInterface, ObjectTable, Framework, NamePlateGui, ClientState, Condition, Log, PlexService, Configuration);
        KosmiJoinService = new KosmiJoinService(ContextMenu, ObjectTable, Log, Configuration);

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open MogFlix settings. Use '/mogflix join' to open your Kosmi room.",
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += () => ConfigWindow.IsOpen = true;
        PluginInterface.UiBuilder.OpenMainUi += () => ConfigWindow.IsOpen = true;

        PlexService.Start();
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("join", StringComparison.OrdinalIgnoreCase))
        {
            KosmiJoinService.OpenKosmiLink();
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

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }
}
