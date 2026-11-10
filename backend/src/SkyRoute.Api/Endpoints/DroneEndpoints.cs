using SkyRoute.Api.Contracts;

namespace SkyRoute.Api.Endpoints;

public static class DroneEndpoints
{
    // Stub data until persistence (#9) and the simulator (#13) land.
    public static readonly Guid SampleDroneId = Guid.Parse("8f14e45f-ceea-4672-9b3a-1d2c3e4f5a6b");
    private static readonly Coordinates CentroBase = new(-26.8305, -65.2039);
    private static readonly Coordinates YerbaBuenaBase = new(-26.8160, -65.3160);

    public static IEndpointRouteBuilder MapDroneEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/drones", () => TypedResults.Ok(new[]
            {
                new DroneResponse(SampleDroneId, "Falcon-1", DroneStatus.Busy, new Coordinates(-26.8311, -65.1975), CentroBase),
                new DroneResponse(Guid.Parse("c9f0f895-fb98-4b91-8f2a-7e6d5c4b3a21"), "Falcon-2", DroneStatus.Available, YerbaBuenaBase, YerbaBuenaBase),
            }))
            .WithTags("Drones")
            .WithName("ListDrones")
            .WithSummary("List drones with their availability, position, and base.");

        return app;
    }
}
