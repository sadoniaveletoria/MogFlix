using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace MogFlix.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string serverUrl;
    private string token;
    private string filterUsername;
    private int pollInterval;
    private string kosmiUrl;
    private string presenceServerUrl;

    public ConfigWindow(Plugin plugin) : base("MogFlix - Settings##MogFlixConfig")
    {
        this.plugin = plugin;
        Size = new Vector2(460, 760);
        SizeCondition = ImGuiCond.FirstUseEver;

        serverUrl = plugin.Configuration.PlexServerUrl;
        token = plugin.Configuration.PlexToken;
        filterUsername = plugin.Configuration.FilterByUsername;
        pollInterval = plugin.Configuration.PollIntervalSeconds;
        kosmiUrl = plugin.Configuration.KosmiUrl;
        presenceServerUrl = plugin.Configuration.PresenceServerUrl;
    }

    public override void Draw()
    {
        ImGui.TextWrapped("Enter your Plex server address and X-Plex-Token. " +
                           "See the plugin README for how to find your token.");
        ImGui.Spacing();

        ImGui.Text("Server URL");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##ServerUrl", ref serverUrl, 256);
        ImGui.TextDisabled("e.g. http://127.0.0.1:32400 (local) or http://192.168.1.x:32400 (LAN)");

        ImGui.Spacing();
        ImGui.Text("Plex Token");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##PlexToken", ref token, 128, ImGuiInputTextFlags.Password);

        ImGui.Spacing();
        ImGui.Text("Filter by username (optional)");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##FilterUsername", ref filterUsername, 64);
        ImGui.TextDisabled("Leave blank to show whatever is playing on the server.");

        ImGui.Spacing();
        ImGui.SetNextItemWidth(150);
        ImGui.SliderInt("Poll interval (s)", ref pollInterval, 2, 30);

        var includeMusic = plugin.Configuration.IncludeMusicSessions;
        if (ImGui.Checkbox("Include music/track sessions", ref includeMusic))
            plugin.Configuration.IncludeMusicSessions = includeMusic;
        ImGui.TextDisabled("Off by default - only movies and TV episodes are tracked.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextDisabled("Visible to other players");
        ImGui.TextWrapped("Shows your title as a nameplate title, visible both to you and other " +
                           "nearby players, using the Honorific plugin's sharing system - you'll " +
                           "need Honorific installed and enabled.");

        var shareViaHonorific = plugin.Configuration.ShareViaHonorific;
        if (ImGui.Checkbox("Share via Honorific", ref shareViaHonorific))
            plugin.Configuration.ShareViaHonorific = shareViaHonorific;

        if (shareViaHonorific)
        {
            if (plugin.HonorificService.HonorificAvailable)
                ImGui.TextColored(new Vector4(0.5f, 1f, 0.5f, 1f), "Connected to Honorific.");
            else
                ImGui.TextColored(new Vector4(1f, 0.7f, 0.3f, 1f),
                    "Honorific not detected - install it from the Dalamud plugin installer (/xlplugins).");

            if (plugin.HonorificService.LastError != null)
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), $"Honorific error: {plugin.HonorificService.LastError}");

            var autoHide = plugin.Configuration.AutoHideInDutyOrCombat;
            if (ImGui.Checkbox("Hide title during duties/combat", ref autoHide))
                plugin.Configuration.AutoHideInDutyOrCombat = autoHide;
        }
        else
        {
            ImGui.TextDisabled("Requires the separate 'Honorific' plugin (by Caraxi) to be installed and enabled.");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextDisabled("Join my watch party (Kosmi)");
        ImGui.Text("Kosmi room URL");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##KosmiUrl", ref kosmiUrl, 256);
        ImGui.TextDisabled("e.g. https://kosmi.io/room/xxxxxxxx - used by '/mogflix join' and the " +
                            "\"Join My Movie\" right-click option on your own character.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextDisabled("See who's watching (optional, needs a small server)");
        ImGui.TextWrapped("Deploy the mogflix-presence Cloudflare Worker (see its README) to let " +
                           "others browse a list of who's currently sharing and send join requests - " +
                           "your Kosmi link itself is only revealed if you accept a request.");

        ImGui.Text("Presence Server URL");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##PresenceServerUrl", ref presenceServerUrl, 256);
        ImGui.TextDisabled("e.g. https://mogflix-presence.yoursubdomain.workers.dev");

        ImGui.Spacing();
        var enablePresence = plugin.Configuration.EnablePresenceSharing;
        if (ImGui.Checkbox("Let others see me and request to join", ref enablePresence))
            plugin.Configuration.EnablePresenceSharing = enablePresence;

        if (plugin.PresenceService.LastError != null)
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), $"Presence error: {plugin.PresenceService.LastError}");

        ImGui.Spacing();
        if (ImGui.Button("Browse Who's Watching"))
        {
            plugin.OpenBrowseWindow();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var dtrBar = plugin.Configuration.ShowInDtrBar;
        if (ImGui.Checkbox("Show in server info bar (DTR)", ref dtrBar))
            plugin.Configuration.ShowInDtrBar = dtrBar;

        ImGui.Spacing();
        if (ImGui.Button("Save"))
        {
            plugin.Configuration.PlexServerUrl = serverUrl.Trim();
            plugin.Configuration.PlexToken = token.Trim();
            plugin.Configuration.FilterByUsername = filterUsername.Trim();
            plugin.Configuration.PollIntervalSeconds = pollInterval;
            plugin.Configuration.KosmiUrl = kosmiUrl.Trim();
            plugin.Configuration.PresenceServerUrl = presenceServerUrl.Trim();
            plugin.Configuration.Save();
            plugin.PlexService.Start();
        }

        ImGui.SameLine();
        if (ImGui.Button("Test Connection"))
        {
            plugin.PlexService.Start();
        }

        if (plugin.PlexService.LastError != null)
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), $"Last error: {plugin.PlexService.LastError}");
        }
        else if (plugin.PlexService.Current.IsPlaying)
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.5f, 1f, 0.5f, 1f), $"Connected - now playing: {plugin.PlexService.Current.Title}");
        }
    }

    public void Dispose() { }
}
