using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using MogFlix.Services;

namespace MogFlix.Integrations;

public class PresenceEntry
{
    public string Name { get; set; } = "";
    public int WorldId { get; set; }
    public string WorldName { get; set; } = "";
    public string? Watching { get; set; }
}

public enum JoinRequestState
{
    None,
    Pending,
    Accepted,
    Declined,
    TimedOut,
    Error,
}

public class IncomingRequest
{
    public string RequestId { get; set; } = "";
    public string RequesterName { get; set; } = "";
    public string RequesterWorldName { get; set; } = "";
}

/// <summary>
/// Talks to the mogflix-presence Cloudflare Worker: periodically announces
/// your presence (if enabled), refreshes a browsable list of other watchers,
/// checks for incoming join requests, and drives the outgoing join-request
/// flow (send -> poll -> accepted/declined/timed out).
/// </summary>
public class PresenceService : IDisposable
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ListRefreshInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan InboxPollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OutgoingPollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan OutgoingTimeout = TimeSpan.FromSeconds(90);

    // Shared secret matching the Worker's PRESENCE_API_KEY. Not editable via
    // settings - baked in so it never appears in any UI a user could
    // screenshot or accidentally share. Note this is only a deterrent
    // against casual abuse, not a cryptographic secret: since this DLL is
    // publicly distributed, anyone with a .NET decompiler could still
    // extract this string.
    private const string PresenceApiKey = "7OoZJvgKGSjktWM6z8H8RIqpfBXlot_n9OfhhROMayk";

    private readonly Configuration config;
    private readonly IObjectTable objectTable;
    private readonly PlexService plexService;
    private readonly IPluginLog log;
    private readonly HttpClient http = new();
    private CancellationTokenSource? cts;

    public IReadOnlyList<PresenceEntry> NearbyWatchers { get; private set; } = Array.Empty<PresenceEntry>();
    public IncomingRequest? PendingIncomingRequest { get; private set; }
    public JoinRequestState OutgoingState { get; private set; } = JoinRequestState.None;
    public string? OutgoingTargetName { get; private set; }
    public string? LastError { get; private set; }

    public PresenceService(Configuration config, IObjectTable objectTable, PlexService plexService, IPluginLog log)
    {
        this.config = config;
        this.objectTable = objectTable;
        this.plexService = plexService;
        this.log = log;
        http.Timeout = TimeSpan.FromSeconds(10);
    }

    public void Start()
    {
        Stop();
        cts = new CancellationTokenSource();
        _ = HeartbeatLoop(cts.Token);
        _ = ListRefreshLoop(cts.Token);
        _ = InboxLoop(cts.Token);
    }

    public void Stop()
    {
        cts?.Cancel();
        cts = null;
    }

    private bool TryGetIdentity(out string name, out int worldId, out string worldName)
    {
        name = "";
        worldId = 0;
        worldName = "";

        var localPlayer = objectTable.LocalPlayer;
        if (localPlayer == null)
            return false;

        name = localPlayer.Name.TextValue;
        worldId = (int)localPlayer.HomeWorld.RowId;

        try
        {
            worldName = localPlayer.HomeWorld.Value.Name.ToString() ?? $"World {worldId}";
        }
        catch
        {
            worldName = $"World {worldId}";
        }

        return !string.IsNullOrEmpty(name);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path)
    {
        var baseUrl = config.PresenceServerUrl.TrimEnd('/');
        var req = new HttpRequestMessage(method, $"{baseUrl}{path}");
        req.Headers.Add("X-Mog-Key", PresenceApiKey);
        return req;
    }

    private async Task HeartbeatLoop(CancellationToken token)
    {
        var wasSharing = false;

        while (!token.IsCancellationRequested)
        {
            try
            {
                var isSharing = config.EnablePresenceSharing && !string.IsNullOrWhiteSpace(config.PresenceServerUrl);

                if (isSharing && TryGetIdentity(out var name, out var worldId, out var worldName))
                {
                    var watching = plexService.Current.IsPlaying ? plexService.Current.Title : null;

                    var req = BuildRequest(HttpMethod.Put, "/presence");
                    req.Content = JsonContent.Create(new
                    {
                        name,
                        worldId,
                        worldName,
                        watching,
                    });
                    using var resp = await http.SendAsync(req, token);
                    resp.EnsureSuccessStatusCode();
                    LastError = null;
                }
                else if (wasSharing && TryGetIdentity(out var offName, out var offWorldId, out _))
                {
                    // Sharing just got turned off - clean up immediately
                    // instead of waiting for the TTL to expire.
                    var delReq = BuildRequest(HttpMethod.Delete, "/presence");
                    delReq.Content = JsonContent.Create(new { name = offName, worldId = offWorldId });
                    using var delResp = await http.SendAsync(delReq, token);
                    delResp.EnsureSuccessStatusCode();
                }

                wasSharing = isSharing;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.Warning($"[MogFlix] Presence heartbeat failed: {ex.Message}");
                LastError = ex.Message;
            }

            try { await Task.Delay(HeartbeatInterval, token); }
            catch (TaskCanceledException) { }
        }
    }

    private async Task ListRefreshLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(config.PresenceServerUrl))
                {
                    var req = BuildRequest(HttpMethod.Get, "/presence/list");
                    using var resp = await http.SendAsync(req, token);
                    resp.EnsureSuccessStatusCode();
                    var data = await resp.Content.ReadFromJsonAsync<ListResponse>(cancellationToken: token);

                    var self = TryGetIdentity(out var myName, out var myWorldId, out _)
                        ? (myName, myWorldId)
                        : ((string, int)?)null;

                    NearbyWatchers = data?.entries
                        ?.Where(e => self == null || e.Name != self.Value.Item1 || e.WorldId != self.Value.Item2)
                        .ToList() ?? new List<PresenceEntry>();
                    LastError = null;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.Warning($"[MogFlix] Presence list refresh failed: {ex.Message}");
                LastError = ex.Message;
            }

            try { await Task.Delay(ListRefreshInterval, token); }
            catch (TaskCanceledException) { }
        }
    }

    private async Task InboxLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (config.EnablePresenceSharing && !string.IsNullOrWhiteSpace(config.PresenceServerUrl)
                    && TryGetIdentity(out var name, out var worldId, out _))
                {
                    var req = BuildRequest(HttpMethod.Get, $"/inbox?name={Uri.EscapeDataString(name)}&worldId={worldId}");
                    using var resp = await http.SendAsync(req, token);
                    resp.EnsureSuccessStatusCode();
                    var data = await resp.Content.ReadFromJsonAsync<InboxResponse>(cancellationToken: token);

                    if (data?.request != null)
                    {
                        // Only surface it if it's a different request than
                        // one we've already dismissed/handled.
                        if (PendingIncomingRequest == null || PendingIncomingRequest.RequestId != data.request.id)
                        {
                            PendingIncomingRequest = new IncomingRequest
                            {
                                RequestId = data.request.id,
                                RequesterName = data.request.requesterName,
                                RequesterWorldName = data.request.requesterWorldName,
                            };
                        }
                    }
                    else
                    {
                        PendingIncomingRequest = null;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.Warning($"[MogFlix] Inbox check failed: {ex.Message}");
            }

            try { await Task.Delay(InboxPollInterval, token); }
            catch (TaskCanceledException) { }
        }
    }

    public void RespondToIncomingRequest(bool accept)
    {
        var pending = PendingIncomingRequest;
        if (pending == null)
            return;

        PendingIncomingRequest = null;
        _ = Task.Run(async () =>
        {
            try
            {
                var req = BuildRequest(HttpMethod.Post, $"/request/{pending.RequestId}/respond");
                req.Content = JsonContent.Create(new
                {
                    accepted = accept,
                    kosmiUrl = accept ? config.KosmiUrl : null,
                });
                using var resp = await http.SendAsync(req);
                resp.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                log.Warning($"[MogFlix] Failed to respond to join request: {ex.Message}");
            }
        });
    }

    public void SendJoinRequest(PresenceEntry target)
    {
        if (OutgoingState == JoinRequestState.Pending)
            return; // one at a time

        OutgoingState = JoinRequestState.Pending;
        OutgoingTargetName = target.Name;

        _ = Task.Run(async () =>
        {
            try
            {
                if (!TryGetIdentity(out var myName, out var myWorldId, out var myWorldName))
                {
                    OutgoingState = JoinRequestState.Error;
                    return;
                }

                var createReq = BuildRequest(HttpMethod.Post, "/request");
                createReq.Content = JsonContent.Create(new
                {
                    requesterName = myName,
                    requesterWorldId = myWorldId,
                    requesterWorldName = myWorldName,
                    targetName = target.Name,
                    targetWorldId = target.WorldId,
                });
                using var createResp = await http.SendAsync(createReq);
                createResp.EnsureSuccessStatusCode();
                var created = await createResp.Content.ReadFromJsonAsync<CreateRequestResponse>();
                if (created?.requestId == null)
                {
                    OutgoingState = JoinRequestState.Error;
                    return;
                }

                var deadline = DateTime.UtcNow + OutgoingTimeout;
                while (DateTime.UtcNow < deadline)
                {
                    await Task.Delay(OutgoingPollInterval);

                    var statusReq = BuildRequest(HttpMethod.Get, $"/request/{created.requestId}");
                    using var statusResp = await http.SendAsync(statusReq);
                    if (!statusResp.IsSuccessStatusCode)
                        continue;

                    var status = await statusResp.Content.ReadFromJsonAsync<RequestStatusResponse>();
                    if (status == null || status.status == "pending")
                        continue;

                    if (status.status == "accepted" && !string.IsNullOrWhiteSpace(status.kosmiUrl))
                    {
                        OutgoingState = JoinRequestState.Accepted;
                        Process.Start(new ProcessStartInfo(status.kosmiUrl) { UseShellExecute = true });
                        return;
                    }

                    OutgoingState = JoinRequestState.Declined;
                    return;
                }

                OutgoingState = JoinRequestState.TimedOut;
            }
            catch (Exception ex)
            {
                log.Warning($"[MogFlix] Join request failed: {ex.Message}");
                OutgoingState = JoinRequestState.Error;
            }
        });
    }

    public void ClearOutgoingState() => OutgoingState = JoinRequestState.None;

    private class ListResponse
    {
        public List<PresenceEntry>? entries { get; set; }
    }

    private class InboxRequestData
    {
        public string id { get; set; } = "";
        public string requesterName { get; set; } = "";
        public string requesterWorldName { get; set; } = "";
    }

    private class InboxResponse
    {
        public InboxRequestData? request { get; set; }
    }

    private class CreateRequestResponse
    {
        public string? requestId { get; set; }
    }

    private class RequestStatusResponse
    {
        public string status { get; set; } = "";
        public string? kosmiUrl { get; set; }
    }

    public void Dispose()
    {
        Stop();
        http.Dispose();
    }
}
