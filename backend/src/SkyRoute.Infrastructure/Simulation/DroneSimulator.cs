using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkyRoute.Application.Drones;
using SkyRoute.Domain.Drones;

namespace SkyRoute.Infrastructure.Simulation;

/// <summary>
/// Drives the live telemetry of the persisted fleet. It loads the drones once, places each one at its
/// <see cref="Drone.HomePosition"/> in the <see cref="TelemetryStore"/>, and broadcasts every drone's
/// telemetry once per tick. Telemetry lives in memory only and is never written to the database.
/// Mission flights (start, complete, fail) are wired in once those use cases land (#12, PR #34).
/// </summary>
public sealed class DroneSimulator(
    IServiceScopeFactory scopeFactory,
    TelemetryStore store,
    TelemetryBroadcaster broadcaster,
    ILogger<DroneSimulator> logger) : BackgroundService
{
    // Battery shown for idle drones. How it drains and recharges is not decided yet (#13).
    private const double IdleBatteryPct = 100;

    private IReadOnlyList<Guid> _fleet = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SimulationPace.Tick);
        try
        {
            do
            {
                try
                {
                    await TickAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    // Anything except a requested shutdown, including a cancellation that did not come from
                    // stoppingToken (for example, a timeout), is logged and the next tick tries again: one bad
                    // tick must not stop the simulator or the API.
                    logger.LogError(ex, "Drone simulator tick failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Requested shutdown: end cleanly.
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        if (_fleet.Count == 0)
        {
            await LoadFleetAsync(cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var droneId in _fleet)
        {
            if (store.Get(droneId) is not { } telemetry)
            {
                continue;
            }

            var current = telemetry with { Timestamp = now };
            store.Set(current);

            try
            {
                await broadcaster.BroadcastAsync(current, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // The other drones keep broadcasting; only a requested shutdown ends the tick.
                logger.LogWarning(ex, "Failed to broadcast telemetry for drone {DroneId}", droneId);
            }
        }
    }

    // The repository is scoped (it uses the DbContext), so it is resolved in a short-lived scope.
    private async Task LoadFleetAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var drones = await scope.ServiceProvider.GetRequiredService<IDroneRepository>().ListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var drone in drones)
        {
            store.Set(new TelemetryMessage(
                drone.Id,
                MissionId: null,
                Math.Round(drone.HomePosition.Lat, 6),
                Math.Round(drone.HomePosition.Lon, 6),
                HeadingDeg: 0,
                IdleBatteryPct,
                drone.Status,
                now));
        }

        _fleet = [.. drones.Select(drone => drone.Id)];
        logger.LogInformation("Drone simulator loaded {Count} drones", _fleet.Count);
    }
}
