using Microsoft.AspNetCore.SignalR;

namespace SkyRoute.Api.Hubs;

/// <summary>
/// Server-to-client only: pushes <c>TelemetryUpdated</c> to every connected client.
/// </summary>
public sealed class TelemetryHub : Hub;
