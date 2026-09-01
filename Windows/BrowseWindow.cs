using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using MogFlix.Integrations;

namespace MogFlix.Windows;

public class BrowseWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public BrowseWindow(Plugin plugin) : base("Who's Watching##MogFlixBrowse")
    {
        this.plugin = plugin;
        Size = new Vector2(420, 360);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var service = plugin.PresenceService;

        if (string.IsNullOrWhiteSpace(plugin.Configuration.PresenceServerUrl))
        {
            ImGui.TextWrapped("Set a Presence Server URL in /mogflix settings first - this needs " +
                               "the mogflix-presence Worker deployed to work.");
            return;
        }

        if (service.LastError != null)
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), $"Error: {service.LastError}");

        ImGui.Spacing();

        if (service.NearbyWatchers.Count == 0)
        {
            ImGui.TextDisabled("Nobody else is currently sharing.");
        }
        else
        {
            foreach (var entry in service.NearbyWatchers)
            {
                ImGui.PushID($"{entry.Name}|{entry.WorldId}");

                ImGui.Text($"{entry.Name} @ {entry.WorldName}");
                if (!string.IsNullOrEmpty(entry.Watching))
                    ImGui.TextDisabled($"Watching: {entry.Watching}");
                else
                    ImGui.TextDisabled("Not currently watching anything");

                var isTargetOfOutgoing = service.OutgoingState != JoinRequestState.None
                    && service.OutgoingTargetName == entry.Name;

                if (isTargetOfOutgoing)
                {
                    switch (service.OutgoingState)
                    {
                        case JoinRequestState.Pending:
                            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.3f, 1f), "Waiting for response...");
                            break;
                        case JoinRequestState.Accepted:
                            ImGui.TextColored(new Vector4(0.5f, 1f, 0.5f, 1f), "Accepted! Opening browser...");
                            break;
                        case JoinRequestState.Declined:
                            ImGui.TextColored(new Vector4(1f, 0.6f, 0.4f, 1f), "Declined.");
                            break;
                        case JoinRequestState.TimedOut:
                            ImGui.TextColored(new Vector4(1f, 0.6f, 0.4f, 1f), "No response - timed out.");
                            break;
                        case JoinRequestState.Error:
                            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Something went wrong.");
                            break;
                    }

                    if (service.OutgoingState != JoinRequestState.Pending && ImGui.Button("Dismiss"))
                        service.ClearOutgoingState();
                }
                else if (ImGui.Button("Request to Join"))
                {
                    service.SendJoinRequest(entry);
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.PopID();
            }
        }
    }

    public void Dispose() { }
}
