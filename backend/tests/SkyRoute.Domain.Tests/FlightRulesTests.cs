using SkyRoute.Domain;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Domain.Tests;

// Flight rules from docs/architecture.md, "Flight rules" (#29).
public class FlightRulesTests
{
    private static readonly Coordinates Centro = new(-26.8305, -65.2039);

    [Fact]
    public void Distance_is_the_great_circle_distance_in_kilometers()
    {
        // One degree of longitude on the equator.
        var km = GeoDistance.Kilometers(new Coordinates(0, 0), new Coordinates(0, 1));

        Assert.Equal(111.19, km, precision: 2);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(2.5)]
    public void Package_weight_up_to_2_5_kg_is_allowed(double weightKg) =>
        Assert.True(FlightRules.IsPackageWeightAllowed(weightKg));

    [Theory]
    [InlineData(2.51)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Package_weight_over_2_5_kg_or_not_positive_is_rejected(double weightKg) =>
        Assert.False(FlightRules.IsPackageWeightAllowed(weightKg));

    [Fact]
    public void Trip_counts_base_to_origin_to_destination_and_back_to_base()
    {
        // Base -> 1° east -> back to base -> base: 2 legs of one degree on the equator.
        var home = new Coordinates(0, 0);
        var route = new Route(new Coordinates(0, 1), home);

        Assert.Equal(2 * 111.19, FlightRules.TripKilometers(home, route), precision: 1);
    }

    [Fact]
    public void A_trip_within_25_km_can_be_flown()
    {
        // Plaza Independencia to Parque 9 de Julio and back: about 3 km.
        var route = new Route(Centro, new Coordinates(-26.8317, -65.1912));

        Assert.True(FlightRules.CanFly(Centro, route));
    }

    [Fact]
    public void A_trip_over_25_km_cannot_be_flown()
    {
        // San Miguel de Tucumán to Famaillá: about 30 km each way.
        var route = new Route(Centro, new Coordinates(-27.0540, -65.4030));

        Assert.False(FlightRules.CanFly(Centro, route));
    }
}
