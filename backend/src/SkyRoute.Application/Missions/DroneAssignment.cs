using SkyRoute.Domain;
using SkyRoute.Domain.Drones;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Application.Missions;

// Assignment rules from docs/architecture.md, "Flight rules" (#29), shared by #11 and #12.
internal static class DroneAssignment
{
    // A paid mission gets the available drone nearest to its origin whose full trip fits within range.
    public static Drone? NearestAvailableDroneInRange(IEnumerable<Drone> fleet, Route route) =>
        fleet
            .Where(d => d.Status == DroneStatus.Available && FlightRules.CanFly(d.HomePosition, route))
            .OrderBy(d => GeoDistance.Kilometers(d.HomePosition, route.Origin))
            .FirstOrDefault();

    // A freed drone takes the oldest Paid mission whose full trip fits within its range.
    public static Mission? OldestPaidMissionInRange(IEnumerable<Mission> missions, Drone drone) =>
        missions
            .Where(m => m.Status == MissionStatus.Paid && FlightRules.CanFly(drone.HomePosition, m.Route))
            .OrderBy(m => m.RequestedAt)
            .FirstOrDefault();
}
