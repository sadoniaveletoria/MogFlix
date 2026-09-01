using System;
using System.Diagnostics;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;

namespace MogFlix.Integrations;

/// <summary>
/// Lets you jump to your own Kosmi watch-party room via '/mogflix join' or
/// by right-clicking your own character and choosing "Join My Movie".
///
/// This is intentionally self-only: it opens *your own* configured Kosmi
/// link. It does not broadcast anything about you to other players, and
/// nothing about it touches your shared movie title in any way.
/// </summary>
public class KosmiJoinService : IDisposable
{
    private readonly IContextMenu contextMenu;
    private readonly IObjectTable objectTable;
    private readonly IPluginLog log;
    private readonly Configuration config;

    public KosmiJoinService(IContextMenu contextMenu, IObjectTable objectTable, IPluginLog log, Configuration config)
    {
        this.contextMenu = contextMenu;
        this.objectTable = objectTable;
        this.log = log;
        this.config = config;

        this.contextMenu.OnMenuOpened += OnMenuOpened;
    }

    public void OpenKosmiLink()
    {
        if (string.IsNullOrWhiteSpace(config.KosmiUrl))
        {
            log.Warning("[MogFlix] No Kosmi URL configured - set one in /mogflix.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(config.KosmiUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            log.Warning($"[MogFlix] Failed to open Kosmi URL: {ex.Message}");
        }
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (string.IsNullOrWhiteSpace(config.KosmiUrl))
            return;

        if (args.MenuType != ContextMenuType.Default)
            return;

        if (args.Target is not MenuTargetDefault target)
            return;

        var localPlayer = objectTable.LocalPlayer;
        if (localPlayer == null || target.TargetObjectId != localPlayer.GameObjectId)
            return; // only offer this on your own character

        args.AddMenuItem(new MenuItem
        {
            Name = "Join My Movie",
            OnClicked = _ => OpenKosmiLink(),
        });
    }

    public void Dispose()
    {
        contextMenu.OnMenuOpened -= OnMenuOpened;
    }
}
