using SkyRoute.Domain;
using SkyRoute.Domain.Drones;

namespace SkyRoute.Infrastructure.Persistence;

// The demo fleet: 2 drones at each base of docs/architecture.md, "Flight rules".
// Fixed ids keep the fleet stable across fresh databases.
internal static class DroneSeed
{
    private static readonly Coordinates Centro = new(-26.8305, -65.2039);
    private static readonly Coordinates YerbaBuena = new(-26.8160, -65.3160);
    private static readonly Coordinates BandaDelRioSali = new(-26.8400, -65.1700);
    private static readonly Coordinates TafiViejo = new(-26.7320, -65.2590);

    public static IReadOnlyList<Drone> Fleet() =>
    [
        new(Guid.Parse("8f14e45f-ceea-4672-9b3a-1d2c3e4f5a6b"), "Falcon-1", Centro),
        new(Guid.Parse("c9f0f895-fb98-4b91-8f2a-7e6d5c4b3a21"), "Falcon-2", Centro),
        new(Guid.Parse("45c48cce-2e2d-4fbd-a8d1-6f2b3c4d5e01"), "Falcon-3", YerbaBuena),
        new(Guid.Parse("d3d94468-02a4-4b1f-9c2e-7a8b9c0d1e02"), "Falcon-4", YerbaBuena),
        new(Guid.Parse("6512bd43-d9ca-4e6b-8f0a-1b2c3d4e5f03"), "Falcon-5", BandaDelRioSali),
        new(Guid.Parse("c20ad4d7-6fe9-4759-aa27-0c0d1e2f3a04"), "Falcon-6", BandaDelRioSali),
        new(Guid.Parse("c51ce410-c124-4a10-8e31-5f6a7b8c9d05"), "Falcon-7", TafiViejo),
        new(Guid.Parse("aab32389-22bc-4c56-a3b7-9e0f1a2b3c06"), "Falcon-8", TafiViejo),
    ];
}
