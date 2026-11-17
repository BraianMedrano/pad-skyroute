using System.Collections.Concurrent;

namespace SkyRoute.Infrastructure.Simulation;

/// <summary>
/// Latest telemetry of each drone, kept in memory only and never written to the database.
/// The simulator replaces a drone's entry every tick; broadcasts and <c>GET /api/drones</c> read it
/// concurrently. Entries are immutable records, so a reader always sees a whole message.
/// </summary>
public sealed class TelemetryStore
{
    private readonly ConcurrentDictionary<Guid, TelemetryMessage> _latest = new();

    public void Set(TelemetryMessage message) => _latest[message.DroneId] = message;

    public TelemetryMessage? Get(Guid droneId) => _latest.TryGetValue(droneId, out var message) ? message : null;

    /// <summary>A snapshot of every drone, ordered by id so repeated reads list drones in the same order.</summary>
    public IReadOnlyList<TelemetryMessage> All() => [.. _latest.Values.OrderBy(message => message.DroneId)];
}
