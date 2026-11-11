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
    private const double EarthRadiusMeters = 6_371_000;

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

        var fraction = _metersIntoSegment / DistanceMeters(From, To);
        var lat = From.Lat + (To.Lat - From.Lat) * fraction;
        var lon = From.Lon + (To.Lon - From.Lon) * fraction;

        return new TelemetryMessage(
            DroneId,
            MissionId: null,
            Math.Round(lat, 6),
            Math.Round(lon, 6),
            Math.Round(BearingDeg(From, To), 1),
            Math.Round(_batteryPct, 1),
            DroneStatus.Busy,
            DateTimeOffset.UtcNow);
    }

    private Coordinates From => Route[_segment];
    private Coordinates To => Route[(_segment + 1) % Route.Length];

    // Equirectangular approximation: accurate enough over a few hundred meters.
    private static double DistanceMeters(Coordinates a, Coordinates b)
    {
        var x = ToRadians(b.Lon - a.Lon) * Math.Cos(ToRadians((a.Lat + b.Lat) / 2));
        var y = ToRadians(b.Lat - a.Lat);
        return Math.Sqrt(x * x + y * y) * EarthRadiusMeters;
    }

    // Initial bearing from a to b: 0 = north, 90 = east.
    private static double BearingDeg(Coordinates a, Coordinates b)
    {
        var lat1 = ToRadians(a.Lat);
        var lat2 = ToRadians(b.Lat);
        var dLon = ToRadians(b.Lon - a.Lon);
        var y = Math.Sin(dLon) * Math.Cos(lat2);
        var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
        return (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
