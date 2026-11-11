using SkyRoute.Domain;

namespace SkyRoute.Infrastructure.Simulation;

/// <summary>
/// Great-circle helpers for moving simulated drones: distance, bearing, and intermediate points.
/// Distances use the Haversine formula with the same Earth radius as the flight range rule
/// (docs/architecture.md, "Flight rules"), so a simulated trip measures what the range check measured.
/// </summary>
public static class GeoMath
{
    private const double EarthRadiusKm = 6371;

    /// <summary>
    /// Great-circle distance in kilometers. Same formula as <c>GeoDistance.Kilometers</c> from #10;
    /// switch to it once it is on main, so both sides share one implementation.
    /// </summary>
    public static double DistanceKm(Coordinates from, Coordinates to) =>
        2 * EarthRadiusKm * Math.Asin(Math.Sqrt(Haversine(from, to)));

    /// <summary>Initial bearing from <paramref name="from"/> to <paramref name="to"/>: 0 = north, 90 = east.</summary>
    public static double BearingDeg(Coordinates from, Coordinates to)
    {
        var lat1 = ToRadians(from.Lat);
        var lat2 = ToRadians(to.Lat);
        var dLon = ToRadians(to.Lon - from.Lon);
        var y = Math.Sin(dLon) * Math.Cos(lat2);
        var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
        return (ToDegrees(Math.Atan2(y, x)) + 360) % 360;
    }

    /// <summary>
    /// The point at <paramref name="fraction"/> (0 to 1) of the great circle from <paramref name="from"/>
    /// to <paramref name="to"/>, so the distance flown is exactly that fraction of <see cref="DistanceKm"/>.
    /// </summary>
    public static Coordinates Interpolate(Coordinates from, Coordinates to, double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        var angle = 2 * Math.Asin(Math.Sqrt(Haversine(from, to)));
        if (angle < 1e-12)
        {
            return fraction < 1 ? from : to;
        }

        var lat1 = ToRadians(from.Lat);
        var lon1 = ToRadians(from.Lon);
        var lat2 = ToRadians(to.Lat);
        var lon2 = ToRadians(to.Lon);

        var a = Math.Sin((1 - fraction) * angle) / Math.Sin(angle);
        var b = Math.Sin(fraction * angle) / Math.Sin(angle);
        var x = a * Math.Cos(lat1) * Math.Cos(lon1) + b * Math.Cos(lat2) * Math.Cos(lon2);
        var y = a * Math.Cos(lat1) * Math.Sin(lon1) + b * Math.Cos(lat2) * Math.Sin(lon2);
        var z = a * Math.Sin(lat1) + b * Math.Sin(lat2);

        return new Coordinates(
            ToDegrees(Math.Atan2(z, Math.Sqrt(x * x + y * y))),
            ToDegrees(Math.Atan2(y, x)));
    }

    private static double Haversine(Coordinates from, Coordinates to)
    {
        var dLat = ToRadians(to.Lat - from.Lat);
        var dLon = ToRadians(to.Lon - from.Lon);
        return Math.Pow(Math.Sin(dLat / 2), 2)
            + Math.Cos(ToRadians(from.Lat)) * Math.Cos(ToRadians(to.Lat)) * Math.Pow(Math.Sin(dLon / 2), 2);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private static double ToDegrees(double radians) => radians * 180 / Math.PI;
}
