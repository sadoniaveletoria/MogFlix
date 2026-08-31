using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Dalamud.Plugin.Services;

namespace MogFlix.Services;

public class NowPlayingInfo
{
    public bool IsPlaying { get; set; }
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; } // show name, for episodes
    public string State { get; set; } = ""; // playing / paused / buffering
    public double ProgressPercent { get; set; }
    public string UserName { get; set; } = "";
    public string DeviceName { get; set; } = "";
}

/// <summary>
/// A single playable session, normalized across Plex's different content
/// types (video vs. track) so selection logic only needs to handle one shape.
/// </summary>
internal class SessionCandidate
{
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public long DurationMs { get; set; }
    public long ViewOffsetMs { get; set; }
    public PlexUser? User { get; set; }
    public PlexPlayer? Player { get; set; }
}

/// <summary>
/// Polls the local/remote Plex Media Server's session endpoint on a timer
/// and exposes the current "now playing" state.
/// </summary>
public class PlexService : IDisposable
{
    // How long to keep showing the last known session after Plex reports
    // nothing playing, before actually clearing it. Smooths over the brief
    // gap between episodes/tracks instead of flickering to "nothing playing".
    private static readonly TimeSpan FlickerGracePeriod = TimeSpan.FromSeconds(20);

    private readonly Configuration config;
    private readonly IPluginLog log;
    private readonly HttpClient http = new();
    private CancellationTokenSource? cts;
    private DateTime lastPlayingAtUtc = DateTime.MinValue;

    public NowPlayingInfo Current { get; private set; } = new();
    public string? LastError { get; private set; }

    public PlexService(Configuration config, IPluginLog log)
    {
        this.config = config;
        this.log = log;
        http.Timeout = TimeSpan.FromSeconds(10);
    }

    public void Start()
    {
        Stop();
        cts = new CancellationTokenSource();
        _ = PollLoop(cts.Token);
    }

    public void Stop()
    {
        cts?.Cancel();
        cts = null;
    }

    private async Task PollLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Poll();

            try
            {
                var delaySeconds = Math.Max(2, config.PollIntervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token);
            }
            catch (TaskCanceledException)
            {
                // expected on shutdown/restart
            }
        }
    }

    private async Task Poll()
    {
        if (string.IsNullOrWhiteSpace(config.PlexServerUrl) || string.IsNullOrWhiteSpace(config.PlexToken))
        {
            Current = new NowPlayingInfo { IsPlaying = false };
            LastError = "Server URL or token not set.";
            return;
        }

        try
        {
            var url = $"{config.PlexServerUrl.TrimEnd('/')}/status/sessions?X-Plex-Token={Uri.EscapeDataString(config.PlexToken)}";
            using var resp = await http.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var xml = await resp.Content.ReadAsStringAsync();

            var serializer = new XmlSerializer(typeof(MediaContainer));
            using var reader = new StringReader(xml);
            var container = (MediaContainer?)serializer.Deserialize(reader);

            var candidates = new List<SessionCandidate>();

            foreach (var v in container?.Videos ?? new())
            {
                if (v.Type != "movie" && v.Type != "episode")
                    continue;

                candidates.Add(new SessionCandidate
                {
                    Title = v.Type == "episode"
                        ? $"{v.ShowTitle} - {v.Title}"
                        : (v.Year != null ? $"{v.Title} ({v.Year})" : v.Title),
                    Subtitle = v.Type == "episode" ? v.ShowTitle : null,
                    DurationMs = v.DurationMs,
                    ViewOffsetMs = v.ViewOffsetMs,
                    User = v.User,
                    Player = v.Player,
                });
            }

            if (config.IncludeMusicSessions)
            {
                foreach (var t in container?.Tracks ?? new())
                {
                    candidates.Add(new SessionCandidate
                    {
                        Title = !string.IsNullOrEmpty(t.ArtistName) ? $"{t.ArtistName} - {t.Title}" : t.Title,
                        Subtitle = t.AlbumName,
                        DurationMs = t.DurationMs,
                        ViewOffsetMs = t.ViewOffsetMs,
                        User = t.User,
                        Player = t.Player,
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(config.FilterByUsername))
            {
                candidates = candidates
                    .Where(c => string.Equals(c.User?.Title, config.FilterByUsername, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // If multiple sessions are active (e.g. an old player wasn't
            // fully closed before starting something new), prefer whichever
            // one is actually playing over a lingering paused/stale session.
            var chosen = candidates.FirstOrDefault(c => c.Player?.State == "playing")
                ?? candidates.FirstOrDefault();

            if (chosen == null)
            {
                // Nothing playing right now - but don't flicker off
                // immediately, in case this is just the brief gap between
                // episodes/tracks. Keep showing the last known info until
                // the grace period elapses.
                if (Current.IsPlaying && DateTime.UtcNow - lastPlayingAtUtc < FlickerGracePeriod)
                {
                    LastError = null;
                    return;
                }

                Current = new NowPlayingInfo { IsPlaying = false };
                LastError = null;
                return;
            }

            lastPlayingAtUtc = DateTime.UtcNow;

            var progress = chosen.DurationMs > 0
                ? (double)chosen.ViewOffsetMs / chosen.DurationMs * 100.0
                : 0;

            Current = new NowPlayingInfo
            {
                IsPlaying = true,
                Title = chosen.Title,
                Subtitle = chosen.Subtitle,
                State = chosen.Player?.State ?? "playing",
                ProgressPercent = progress,
                UserName = chosen.User?.Title ?? "",
                DeviceName = chosen.Player?.DeviceTitle ?? "",
            };
            LastError = null;
        }
        catch (Exception ex)
        {
            log.Warning($"[MogFlix] Poll failed: {ex.Message}");
            LastError = ex.Message;
            Current = new NowPlayingInfo { IsPlaying = false };
        }
    }

    public void Dispose()
    {
        Stop();
        http.Dispose();
    }
}
