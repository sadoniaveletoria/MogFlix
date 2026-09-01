using Dalamud.Configuration;
using System;

namespace MogFlix;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>
    /// Base URL of your Plex Media Server, e.g. http://127.0.0.1:32400
    /// or http://192.168.1.50:32400 if the server is on another machine.
    /// </summary>
    public string PlexServerUrl { get; set; } = "http://127.0.0.1:32400";

    /// <summary>
    /// Your Plex account's X-Plex-Token. See README for how to find this.
    /// </summary>
    public string PlexToken { get; set; } = "";

    /// <summary>
    /// How often (in seconds) to poll the Plex server for session info.
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// If true, broadcasts the title to other players via the Honorific
    /// plugin's IPC (requires Honorific to be installed and enabled). This
    /// is also how the title shows on your own screen - Honorific renders
    /// locally too, so no separate local-only nameplate option is needed.
    /// </summary>
    public bool ShareViaHonorific { get; set; } = false;

    /// <summary>
    /// If true, shows a compact now-playing entry in the DTR bar (server
    /// info strip near the clock).
    /// </summary>
    public bool ShowInDtrBar { get; set; } = true;

    /// <summary>
    /// If true, the Honorific-shared title is cleared while in combat or
    /// bound by a duty, and restored afterward.
    /// </summary>
    public bool AutoHideInDutyOrCombat { get; set; } = true;

    /// <summary>
    /// If true, music/track sessions from Plex are included, not just
    /// movies and TV episodes.
    /// </summary>
    public bool IncludeMusicSessions { get; set; } = false;

    /// <summary>
    /// Your Kosmi watch-party room URL, e.g. https://kosmi.io/room/xxxxxxxx
    /// Used by the "Join My Movie" context menu entry and /mogflix join.
    /// </summary>
    public string KosmiUrl { get; set; } = "";

    /// <summary>
    /// URL of your deployed mogflix-presence Cloudflare Worker, e.g.
    /// https://mogflix-presence.yoursubdomain.workers.dev
    /// </summary>
    public string PresenceServerUrl { get; set; } = "";

    /// <summary>
    /// If true, periodically announces your presence (name, world, and what
    /// you're watching) to the presence server so others can see you in the
    /// browse list and send join requests.
    /// </summary>
    public bool EnablePresenceSharing { get; set; } = false;

    /// <summary>
    /// Optional: only show sessions belonging to this Plex username.
    /// Leave blank to show whatever is playing on the server (useful if
    /// multiple people use the same Plex server).
    /// </summary>
    public string FilterByUsername { get; set; } = "";

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
