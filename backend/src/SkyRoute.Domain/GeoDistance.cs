namespace SkyRoute.Domain;

public static class GeoDistance
{
    private const double EarthRadiusKm = 6371;

    // Great-circle distance (Haversine formula).
    public static double Kilometers(Coordinates from, Coordinates to)
    {
        var dLat = ToRadians(to.Lat - from.Lat);
        var dLon = ToRadians(to.Lon - from.Lon);
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
            + Math.Cos(ToRadians(from.Lat)) * Math.Cos(ToRadians(to.Lat)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
