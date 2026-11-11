using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkyRoute.Domain;
using SkyRoute.Domain.Drones;

namespace SkyRoute.Infrastructure.Simulation;

/// <summary>
/// Temporary loop that flies one fake drone around a fixed circuit once per second.
/// Replaced by the real simulator in #13. State lives in memory only.
/// </summary>
public sealed class FakeDroneLoop(TelemetryBroadcaster broadcaster, ILogger<FakeDroneLoop> logger) : BackgroundService
{
    // Same id as the sample drone in the stub contract (#22), so REST and telemetry line up.
    public static readonly Guid DroneId = Guid.Parse("8f14e45f-ceea-4672-9b3a-1d2c3e4f5a6b");

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private const double MetersPerTick = 15;
    private const double BatteryDrainPerTick = 0.1;

    // Closed circuit around Plaza Independencia, San Miguel de Tucumán (clockwise).
    private static readonly Coordinates[] Route =
    [
        new(-26.8280, -65.2065),
        new(-26.8280, -65.2013),
        new(-26.8305, -65.1999),
        new(-26.8330, -65.2013),
        new(-26.8330, -65.2065),
        new(-26.8305, -65.2079),
    ];

    private int _segment;
    private double _metersIntoSegment;
    private double _batteryPct = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await broadcaster.BroadcastAsync(Advance(), stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Failed to broadcast fake drone telemetry");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private TelemetryMessage Advance()
    {
        _metersIntoSegment += MetersPerTick;
        _batteryPct = Math.Max(0, _batteryPct - BatteryDrainPerTick);

        while (_metersIntoSegment >= DistanceMeters(From, To))
        {
            _metersIntoSegment -= DistanceMeters(From, To);
            _segment = (_segment + 1) % Route.Length;
            if (_segment == 0)
            {
                _batteryPct = 100; // New lap: pretend the battery was swapped.
            }
        }

        var position = GeoMath.Interpolate(From, To, _metersIntoSegment / DistanceMeters(From, To));

        return new TelemetryMessage(
            DroneId,
            MissionId: null,
            Math.Round(position.Lat, 6),
            Math.Round(position.Lon, 6),
            Math.Round(GeoMath.BearingDeg(From, To), 1),
            Math.Round(_batteryPct, 1),
            DroneStatus.Busy,
            DateTimeOffset.UtcNow);
    }

    private Coordinates From => Route[_segment];
    private Coordinates To => Route[(_segment + 1) % Route.Length];

    private static double DistanceMeters(Coordinates a, Coordinates b) => GeoMath.DistanceKm(a, b) * 1000;
}
