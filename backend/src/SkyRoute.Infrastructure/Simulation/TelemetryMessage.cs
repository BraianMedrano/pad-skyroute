using SkyRoute.Domain.Drones;

namespace SkyRoute.Infrastructure.Simulation;

/// <summary>
/// Live telemetry for one drone, broadcast as the SignalR event <c>TelemetryUpdated</c>.
/// Kept in memory only; never written to the database.
/// </summary>
public sealed record TelemetryMessage(
    Guid DroneId,
    Guid? MissionId,
    double Lat,
    double Lon,
    double HeadingDeg,
    double BatteryPct,
    DroneStatus Status,
    DateTimeOffset Timestamp);
