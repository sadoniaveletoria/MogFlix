using System;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using MogFlix.Services;

namespace MogFlix.Integrations;

/// <summary>
/// Broadcasts the current Plex title to other players by calling into the
/// Honorific plugin's IPC (Honorific.SetCharacterTitle / ClearCharacterTitle).
/// This is the standard way third-party plugins push a visible custom title -
/// Dalamud's own nameplate API only affects your own local view, it does not
/// broadcast anything over the network.
///
/// If Honorific isn't installed, calls simply fail silently and
/// <see cref="HonorificAvailable"/> stays false so the settings UI can say so.
/// </summary>
public class HonorificService : IDisposable
{
    // Matches Honorific's own TitleData shape (Caraxi/Honorific, CustomTitle.cs).
    // Field names must match exactly since this is serialized to JSON for IPC.
    private class TitleData
    {
        public string? Title = string.Empty;
        public bool IsPrefix;
        public bool IsOriginal;
        public Vector3? Color;
        public Vector3? Glow;
        public int? GradientColourSet;
        public int? GradientAnimationStyle;
    }

    // Chosen after live-testing Honorific's built-in gradient sets.
    private const int GradientColourSet = 12;
    private const int GradientAnimationStyle = 1;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly INamePlateGui namePlateGui;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IPluginLog log;
    private readonly PlexService plexService;
    private readonly Configuration config;

    private bool titleCurrentlySet;
    private string? lastSentTitleText;

    public bool HonorificAvailable { get; private set; }
    public string? LastError { get; private set; }

    public HonorificService(
        IDalamudPluginInterface pluginInterface,
        IObjectTable objectTable,
        IFramework framework,
        INamePlateGui namePlateGui,
        IClientState clientState,
        ICondition condition,
        IPluginLog log,
        PlexService plexService,
        Configuration config)
    {
        this.pluginInterface = pluginInterface;
        this.objectTable = objectTable;
        this.framework = framework;
        this.namePlateGui = namePlateGui;
        this.clientState = clientState;
        this.condition = condition;
        this.log = log;
        this.plexService = plexService;
        this.config = config;

        this.framework.Update += OnFrameworkUpdate;
        this.clientState.TerritoryChanged += OnTerritoryChanged;
    }

    private void OnTerritoryChanged(uint territoryId)
    {
        // Zoning recreates your character object, which appears to wipe
        // Honorific's internal title assignment for you. Since the title
        // TEXT hasn't changed from our side, we'd otherwise never notice
        // and never re-send it - force a re-send on the next update tick.
        titleCurrentlySet = false;
        lastSentTitleText = null;
    }

    private void OnFrameworkUpdate(IFramework fw)
    {
        if (!config.ShareViaHonorific)
        {
            if (titleCurrentlySet)
                ClearTitle();
            return;
        }

        if (config.AutoHideInDutyOrCombat &&
            (condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.InCombat]))
        {
            if (titleCurrentlySet)
                ClearTitle();
            return;
        }

        var localPlayer = objectTable.LocalPlayer;
        if (localPlayer == null)
            return;

        var info = plexService.Current;

        if (info.IsPlaying)
        {
            var titleText = FormatTitle(info);
            if (titleCurrentlySet && titleText == lastSentTitleText)
                return; // unchanged since last send - avoid re-sending every frame

            try
            {
                var data = new TitleData
                {
                    Title = titleText,
                    IsPrefix = false,
                    GradientColourSet = GradientColourSet,
                    GradientAnimationStyle = GradientAnimationStyle,
                };

                pluginInterface
                    .GetIpcSubscriber<uint, string, object>("Honorific.SetCharacterTitle")
                    .InvokeAction(localPlayer.ObjectIndex, JsonConvert.SerializeObject(data));

                lastSentTitleText = titleText;
                titleCurrentlySet = true;
                HonorificAvailable = true;
                LastError = null;

                // Honorific's internal cache doesn't always pick up an
                // externally-assigned title without a nudge - force the
                // nameplate to redraw so it reflects the new data.
                namePlateGui.RequestRedraw();
            }
            catch (Exception ex)
            {
                // Honorific not installed, or its IPC changed shape.
                log.Warning($"[MogFlix] Honorific SetCharacterTitle failed: {ex}");
                HonorificAvailable = false;
                LastError = ex.Message;
            }
        }
        else if (titleCurrentlySet)
        {
            ClearTitle();
        }
    }

    private void ClearTitle()
    {
        var localPlayer = objectTable.LocalPlayer;
        if (localPlayer == null)
            return;

        try
        {
            pluginInterface
                .GetIpcSubscriber<uint, object>("Honorific.ClearCharacterTitle")
                .InvokeAction(localPlayer.ObjectIndex);
            HonorificAvailable = true;
            LastError = null;
            namePlateGui.RequestRedraw();
        }
        catch (Exception ex)
        {
            log.Warning($"[MogFlix] Honorific ClearCharacterTitle failed: {ex}");
            HonorificAvailable = false;
            LastError = ex.Message;
        }
        finally
        {
            titleCurrentlySet = false;
            lastSentTitleText = null;
        }
    }

    // Honorific enforces a 32-character limit on titles.
    private const int HonorificMaxTitleLength = 32;

    private static string FormatTitle(NowPlayingInfo info)
    {
        // Drop a trailing "(YYYY)" year, e.g. "Movie Name (2006)" -> "Movie Name"
        var titleWithoutYear = Regex.Replace(info.Title, @"\s*\(\d{4}\)$", "");

        var leadingWord = info.State switch
        {
            "paused" => "Paused",
            "buffering" => "Buffering",
            _ => "Watching",
        };

        var prefix = $"{leadingWord} 『";
        const string suffix = "』";
        var overhead = prefix.Length + suffix.Length;
        var maxTitleLength = Math.Max(0, HonorificMaxTitleLength - overhead);

        var shortTitle = titleWithoutYear.Length <= maxTitleLength
            ? titleWithoutYear
            : maxTitleLength > 1
                ? titleWithoutYear[..(maxTitleLength - 1)] + "…"
                : titleWithoutYear[..maxTitleLength];

        return $"{prefix}{shortTitle}{suffix}";
    }

    public void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;
        clientState.TerritoryChanged -= OnTerritoryChanged;
        if (titleCurrentlySet)
            ClearTitle();
    }
}
