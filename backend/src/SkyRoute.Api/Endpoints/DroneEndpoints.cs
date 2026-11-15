using Microsoft.AspNetCore.Http.HttpResults;
using SkyRoute.Api.Contracts;
using SkyRoute.Application.Drones;
using SkyRoute.Infrastructure.Simulation;

namespace SkyRoute.Api.Endpoints;

public static class DroneEndpoints
{
    public static IEndpointRouteBuilder MapDroneEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/drones", ListDronesAsync)
            .WithTags("Drones")
            .WithName("ListDrones")
            .WithSummary("List drones with their availability, position, and base.");

        return app;
    }

    // The fleet and its status come from the database; the position is the live one from the simulator's
    // in-memory telemetry (#13), or the base before the simulator has placed the drone.
    private static async Task<Ok<DroneResponse[]>> ListDronesAsync(
        IDroneRepository drones, TelemetryStore telemetry, CancellationToken cancellationToken)
    {
        var fleet = await drones.ListAsync(cancellationToken);
        return TypedResults.Ok(fleet
            .Select(drone =>
            {
                var position = telemetry.Get(drone.Id) is { } live
                    ? new Coordinates(live.Lat, live.Lon)
                    : new Coordinates(drone.HomePosition.Lat, drone.HomePosition.Lon);
                return new DroneResponse(
                    drone.Id,
                    drone.Name,
                    drone.Status == Domain.Drones.DroneStatus.Busy ? DroneStatus.Busy : DroneStatus.Available,
                    position,
                    new Coordinates(drone.HomePosition.Lat, drone.HomePosition.Lon));
            })
            .ToArray());
    }
}
