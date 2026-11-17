using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkyRoute.Application.Drones;
using SkyRoute.Application.Missions;
using SkyRoute.Domain;
using SkyRoute.Domain.Drones;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Infrastructure.Simulation;

/// <summary>
/// Flies the persisted fleet. Every tick it:
/// <list type="number">
/// <item>reads the persisted missions: starts <c>Assigned</c> ones through <see cref="StartMission"/>, resumes
/// <c>InProgress</c> ones that have no flight (after a restart, from the drone's base), and drops flights whose
/// mission was ended;</item>
/// <item>moves each flight from the drone's base to the origin and then to the destination, calling
/// <see cref="CompleteMission"/> on arrival or <see cref="FailMission"/> if the battery runs out;</item>
/// <item>broadcasts every drone's telemetry, flying or idle.</item>
/// </list>
/// The persisted mission status is the source of truth: a use case that throws is not repeated blindly, the
/// next tick reads the status again and only retries a mission that is still <c>InProgress</c>. Each use case
/// call gets its own DI scope, so no DbContext is shared between calls. Telemetry lives in memory only and is
/// never written to the database. When a flight ends, the drone is back at its base with a full battery
/// (no return flight, #13).
/// </summary>
public sealed class DroneSimulator(
    IServiceScopeFactory scopeFactory,
    TelemetryStore store,
    TelemetryBroadcaster broadcaster,
    ILogger<DroneSimulator> logger) : BackgroundService
{
    private const string BatteryDepletedReason = "Battery depleted before reaching the destination.";

    // Only the simulator loop touches these, one tick at a time.
    private readonly Dictionary<Guid, Coordinates> _homes = [];
    private readonly Dictionary<Guid, DroneFlight> _flights = [];

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
        if (_homes.Count == 0)
        {
            await LoadFleetAsync(cancellationToken);
        }

        try
        {
            await SyncWithMissionsAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The flights already in the air keep moving and every drone keeps broadcasting.
            logger.LogWarning(ex, "Could not read the missions; retrying on the next tick");
        }

        foreach (var flight in _flights.Values.ToList())
        {
            await FlyAsync(flight, cancellationToken);
        }

        await BroadcastAsync(cancellationToken);
    }

    // The repository is scoped (it uses the DbContext), so it is resolved in a short-lived scope.
    private async Task LoadFleetAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var drones = await scope.ServiceProvider.GetRequiredService<IDroneRepository>().ListAsync(cancellationToken);

        foreach (var drone in drones)
        {
            _homes[drone.Id] = drone.HomePosition;
            SetIdle(drone.Id, drone.Status);
        }

        logger.LogInformation("Drone simulator loaded {Count} drones", _homes.Count);
    }

    private async Task SyncWithMissionsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Mission> missions;
        IReadOnlyList<Drone> drones;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            missions = await scope.ServiceProvider.GetRequiredService<IMissionRepository>().ListAsync(cancellationToken);
            drones = await scope.ServiceProvider.GetRequiredService<IDroneRepository>().ListAsync(cancellationToken);
        }

        var droneStatus = drones.ToDictionary(d => d.Id, d => d.Status);
        var inProgress = missions
            .Where(m => m.Status == MissionStatus.InProgress)
            .Select(m => m.Id)
            .ToHashSet();

        // A flight whose mission is no longer InProgress was ended already, for example by a CompleteMission
        // whose webhook failed after saving: the drone goes back to its base and nothing is called again.
        foreach (var flight in _flights.Values.Where(f => !inProgress.Contains(f.MissionId)).ToList())
        {
            logger.LogInformation(
                "Mission {MissionId} is no longer in progress; drone {DroneId} is back at its base",
                flight.MissionId, flight.DroneId);
            EndFlight(flight.DroneId, droneStatus.GetValueOrDefault(flight.DroneId, DroneStatus.Available));
        }

        foreach (var mission in missions)
        {
            if (mission.AssignedDroneId is not { } droneId || !_homes.ContainsKey(droneId) || _flights.ContainsKey(droneId))
            {
                continue;
            }

            if (mission.Status == MissionStatus.InProgress)
            {
                // No flight for an InProgress mission: the API restarted mid-flight. Resume from the base.
                logger.LogInformation("Resuming mission {MissionId} with drone {DroneId} from its base", mission.Id, droneId);
                StartFlight(mission, droneId);
            }
            else if (mission.Status == MissionStatus.Assigned && await TryStartMissionAsync(mission.Id, cancellationToken))
            {
                StartFlight(mission, droneId);
            }
        }

        // Idle drones show their persisted status: Busy when already handed to a mission that has not started.
        foreach (var (droneId, status) in droneStatus)
        {
            if (!_flights.ContainsKey(droneId) && store.Get(droneId) is { } telemetry && telemetry.Status != status)
            {
                store.Set(telemetry with { Status = status });
            }
        }
    }

    private async Task<bool> TryStartMissionAsync(Guid missionId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var started = await scope.ServiceProvider.GetRequiredService<StartMission>().ExecuteAsync(missionId, cancellationToken);
            if (started is null)
            {
                return false;
            }

            logger.LogInformation("Mission {MissionId} started", missionId);
            return true;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Invalid transition or concurrent update: the next tick reads the status again.
            logger.LogWarning(ex, "Could not start mission {MissionId}; checking it again on the next tick", missionId);
            return false;
        }
    }

    private async Task FlyAsync(DroneFlight flight, CancellationToken cancellationToken)
    {
        // A flight waiting for its mission to end (arrived, or out of battery) stays where it is.
        if (!flight.IsBatteryDepleted)
        {
            flight.AdvanceOneTick();
        }
        var telemetry = flight.ToTelemetry(DateTimeOffset.UtcNow);
        store.Set(telemetry);

        if (flight.HasArrived)
        {
            // Show the drone at the destination before it goes back to its base.
            await SendAsync(telemetry, cancellationToken);
            await EndMissionAsync(flight, failureReason: null, cancellationToken);
        }
        else if (flight.IsBatteryDepleted)
        {
            await EndMissionAsync(flight, BatteryDepletedReason, cancellationToken);
        }
    }

    private async Task EndMissionAsync(DroneFlight flight, string? failureReason, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var ended = failureReason is null
                ? await services.GetRequiredService<CompleteMission>().ExecuteAsync(flight.MissionId, cancellationToken)
                : await services.GetRequiredService<FailMission>().ExecuteAsync(flight.MissionId, failureReason, cancellationToken);

            // The use case may have handed the drone straight to the next Paid mission, so its status is read
            // back instead of assumed.
            var drone = await services.GetRequiredService<IDroneRepository>().GetAsync(flight.DroneId, cancellationToken);
            EndFlight(flight.DroneId, drone?.Status ?? DroneStatus.Available);

            if (ended is null)
            {
                logger.LogWarning("Mission {MissionId} no longer exists; its flight was dropped", flight.MissionId);
            }
            else
            {
                logger.LogInformation("Mission {MissionId} ended as {Status}", ended.Id, ended.Status);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The flight stays where it is. If the state was saved before the error (a failed webhook), the
            // next tick sees the mission ended and drops the flight; if nothing was saved (a concurrent
            // update), the mission is still InProgress and the next tick tries again.
            logger.LogWarning(ex, "Could not end mission {MissionId}; checking it again on the next tick", flight.MissionId);
        }
    }

    private void StartFlight(Mission mission, Guid droneId)
    {
        var flight = new DroneFlight(droneId, mission.Id, _homes[droneId], mission.Route.Origin, mission.Route.Destination);
        _flights[droneId] = flight;
        store.Set(flight.ToTelemetry(DateTimeOffset.UtcNow));
    }

    // Back at the base with a full battery: there is no return flight (#13).
    private void EndFlight(Guid droneId, DroneStatus status)
    {
        _flights.Remove(droneId);
        SetIdle(droneId, status);
    }

    private void SetIdle(Guid droneId, DroneStatus status)
    {
        var home = _homes[droneId];
        store.Set(new TelemetryMessage(
            droneId,
            MissionId: null,
            Math.Round(home.Lat, 6),
            Math.Round(home.Lon, 6),
            HeadingDeg: 0,
            BatteryPct: 100,
            status,
            DateTimeOffset.UtcNow));
    }

    private async Task BroadcastAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var droneId in _homes.Keys)
        {
            if (store.Get(droneId) is not { } telemetry)
            {
                continue;
            }

            var current = telemetry with { Timestamp = now };
            store.Set(current);
            await SendAsync(current, cancellationToken);
        }
    }

    private async Task SendAsync(TelemetryMessage telemetry, CancellationToken cancellationToken)
    {
        try
        {
            await broadcaster.BroadcastAsync(telemetry, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The other drones keep broadcasting; only a requested shutdown ends the tick.
            logger.LogWarning(ex, "Failed to broadcast telemetry for drone {DroneId}", telemetry.DroneId);
        }
    }
}
