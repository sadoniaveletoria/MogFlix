using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using MogFlix.Integrations;

namespace MogFlix.Windows;

public class IncomingRequestWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public IncomingRequestWindow(Plugin plugin)
        : base("Join Request##MogFlixIncomingRequest", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
    {
        this.plugin = plugin;
    }

    public override bool DrawConditions() => plugin.PresenceService.PendingIncomingRequest != null;

    public override void Draw()
    {
        var request = plugin.PresenceService.PendingIncomingRequest;
        if (request == null)
            return;

        ImGui.TextWrapped($"{request.RequesterName} ({request.RequesterWorldName}) is requesting to watch with you.");
        ImGui.Spacing();

        if (ImGui.Button("Accept", new Vector2(120, 0)))
        {
            plugin.PresenceService.RespondToIncomingRequest(true);
        }

        ImGui.SameLine();

        if (ImGui.Button("Decline", new Vector2(120, 0)))
        {
            plugin.PresenceService.RespondToIncomingRequest(false);
        }
    }

    public void Dispose() { }
}
