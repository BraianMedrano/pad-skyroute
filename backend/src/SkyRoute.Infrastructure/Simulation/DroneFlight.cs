using SkyRoute.Domain;
using SkyRoute.Domain.Drones;

namespace SkyRoute.Infrastructure.Simulation;

public enum FlightLeg
{
    ToOrigin,
    ToDestination,
    Arrived,
}

/// <summary>
/// One drone flying one mission, in memory only: from where it starts to the mission origin, then to the
/// destination, along great circles. The simulator advances it a fixed distance per tick.
/// </summary>
public sealed class DroneFlight
{
    public DroneFlight(Guid droneId, Guid missionId, Coordinates start, Coordinates origin, Coordinates destination)
    {
        DroneId = droneId;
        MissionId = missionId;
        Origin = origin;
        Destination = destination;
        Position = start;
        Leg = FlightLeg.ToOrigin;
        SkipReachedLegs();
        HeadingDeg = Leg == FlightLeg.Arrived ? 0 : GeoMath.BearingDeg(Position, Target);
    }

    public Guid DroneId { get; }
    public Guid MissionId { get; }
    public Coordinates Origin { get; }
    public Coordinates Destination { get; }
    public Coordinates Position { get; private set; }

    /// <summary>Direction of travel toward the current target: 0 = north, 90 = east. Kept as is on arrival.</summary>
    public double HeadingDeg { get; private set; }

    public FlightLeg Leg { get; private set; }

    /// <summary>Distance flown since the flight started.</summary>
    public double FlownKm { get; private set; }

    public bool HasArrived => Leg == FlightLeg.Arrived;

    /// <summary>Distance left to the destination, through the origin if it is not reached yet.</summary>
    public double RemainingKm => Leg switch
    {
        FlightLeg.ToOrigin => GeoMath.DistanceKm(Position, Origin) + GeoMath.DistanceKm(Origin, Destination),
        FlightLeg.ToDestination => GeoMath.DistanceKm(Position, Destination),
        _ => 0,
    };

    private Coordinates Target => Leg == FlightLeg.ToOrigin ? Origin : Destination;

    /// <summary>Advances one simulation tick at cruise speed.</summary>
    public void AdvanceOneTick() => Advance(SimulationPace.KmPerTick);

    /// <summary>
    /// Flies <paramref name="km"/> along the route. Reaching the origin carries the rest of the distance into
    /// the leg to the destination; reaching the destination stops there.
    /// </summary>
    public void Advance(double km)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(km);

        var left = km;
        while (left > 0 && !HasArrived)
        {
            var toTarget = GeoMath.DistanceKm(Position, Target);
            HeadingDeg = GeoMath.BearingDeg(Position, Target);

            if (left < toTarget)
            {
                Position = GeoMath.Interpolate(Position, Target, left / toTarget);
                FlownKm += left;
                return;
            }

            Position = Target;
            FlownKm += toTarget;
            left -= toTarget;
            Leg = Leg == FlightLeg.ToOrigin ? FlightLeg.ToDestination : FlightLeg.Arrived;
            SkipReachedLegs();
        }
    }

    /// <summary>
    /// Telemetry for the current position. The battery level comes from the caller: how it drains and
    /// recharges is not decided yet (#13).
    /// </summary>
    public TelemetryMessage ToTelemetry(double batteryPct, DateTimeOffset timestamp) => new(
        DroneId,
        MissionId,
        Math.Round(Position.Lat, 6),
        Math.Round(Position.Lon, 6),
        Math.Round(HeadingDeg, 1),
        Math.Round(batteryPct, 1),
        DroneStatus.Busy,
        timestamp);

    // A leg whose target is where the drone already is (for example, a mission that starts at its base)
    // is finished without flying.
    private void SkipReachedLegs()
    {
        while (!HasArrived && GeoMath.DistanceKm(Position, Target) == 0)
        {
            Leg = Leg == FlightLeg.ToOrigin ? FlightLeg.ToDestination : FlightLeg.Arrived;
        }
    }
}
