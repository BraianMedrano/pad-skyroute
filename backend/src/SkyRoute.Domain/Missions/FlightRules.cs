namespace SkyRoute.Domain.Missions;

// Flight rules from docs/architecture.md, "Flight rules" (#29).
public static class FlightRules
{
    public const double MaxPackageWeightKg = 2.5;
    public const double MaxFlightKm = 25;

    public static bool IsPackageWeightAllowed(double weightKg) =>
        weightKg > 0 && weightKg <= MaxPackageWeightKg;

    // The full trip of a drone based at home: base → origin → destination → base.
    public static double TripKilometers(Coordinates home, Route route) =>
        GeoDistance.Kilometers(home, route.Origin)
        + GeoDistance.Kilometers(route.Origin, route.Destination)
        + GeoDistance.Kilometers(route.Destination, home);

    public static bool CanFly(Coordinates home, Route route) =>
        TripKilometers(home, route) <= MaxFlightKm;
}
