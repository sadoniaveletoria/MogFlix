using System;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Plugin.Services;

namespace MogFlix.Services;

/// <summary>
/// Shows a compact now-playing entry in the DTR bar (the server info strip
/// near the clock, top-right of the screen by default). Unlike the nameplate
/// title, this stays visible regardless of camera angle, sitting, or whether
/// your own nameplate is shown.
/// </summary>
public class DtrBarService : IDisposable
{
    private readonly IDtrBarEntry entry;
    private readonly IFramework framework;
    private readonly PlexService plexService;
    private readonly Configuration config;

    public DtrBarService(IDtrBar dtrBar, IFramework framework, PlexService plexService, Configuration config)
    {
        this.framework = framework;
        this.plexService = plexService;
        this.config = config;

        entry = dtrBar.Get("MogFlix");
        this.framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework fw)
    {
        if (!config.ShowInDtrBar)
        {
            entry.Shown = false;
            return;
        }

        var info = plexService.Current;
        if (!info.IsPlaying)
        {
            entry.Shown = false;
            return;
        }

        var stateGlyph = info.State switch
        {
            "paused" => "II",
            "buffering" => "...",
            _ => "▸",
        };

        entry.Text = $"{stateGlyph} {Truncate(info.Title, 40)}";
        entry.Shown = true;
    }

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";

    public void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;
        entry.Remove();
    }
}
