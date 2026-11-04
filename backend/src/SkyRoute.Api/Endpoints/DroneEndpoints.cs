using SkyRoute.Api.Contracts;

namespace SkyRoute.Api.Endpoints;

public static class DroneEndpoints
{
    // Stub data until persistence (#9) and the simulator (#13) land.
    public static readonly Guid SampleDroneId = Guid.Parse("8f14e45f-ceea-4672-9b3a-1d2c3e4f5a6b");

    public static IEndpointRouteBuilder MapDroneEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/drones", () => TypedResults.Ok(new[]
            {
                new DroneResponse(SampleDroneId, "Falcon-1", DroneStatus.Busy, new Coordinates(-34.5950, -58.3900)),
                new DroneResponse(Guid.Parse("c9f0f895-fb98-4b91-8f2a-7e6d5c4b3a21"), "Falcon-2", DroneStatus.Available, new Coordinates(-34.6037, -58.3816)),
            }))
            .WithTags("Drones")
            .WithName("ListDrones")
            .WithSummary("List drones with their availability and position.");

        return app;
    }
}
