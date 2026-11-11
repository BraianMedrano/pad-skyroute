namespace SkyRoute.Infrastructure.Simulation;

/// <summary>
/// How fast simulated drones fly. Cruise speed comes from docs/architecture.md, "Flight rules";
/// the demo speeds up time by a fixed factor so a delivery takes seconds instead of minutes.
/// </summary>
public static class SimulationPace
{
    public const double CruiseSpeedKmh = 60;

    /// <summary>One real second is this many simulated seconds.</summary>
    public const double TimeScale = 10;

    public static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

    /// <summary>Distance a drone flies in one tick: 60 km/h × 10 × 1 s ≈ 0.167 km.</summary>
    public static double KmPerTick => CruiseSpeedKmh / 3600 * Tick.TotalSeconds * TimeScale;
}
