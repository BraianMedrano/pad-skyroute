using Microsoft.AspNetCore.SignalR;

namespace SkyRoute.Infrastructure.Simulation;

/// <summary>
/// Sends telemetry to every client connected to the telemetry hub.
/// Shared by the temporary fake drone loop and the real simulator (#13).
/// </summary>
public sealed class TelemetryBroadcaster(IHubClients clients)
{
    public const string EventName = "TelemetryUpdated";

    public Task BroadcastAsync(TelemetryMessage message, CancellationToken cancellationToken) =>
        clients.All.SendAsync(EventName, message, cancellationToken);
}
