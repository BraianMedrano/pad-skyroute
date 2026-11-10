using SkyRoute.Api.Contracts;

namespace SkyRoute.Api.Endpoints;

public static class MissionEndpoints
{
    // Stub data until the use cases land (#10, #11). The contract is what matters here.
    // Plaza Independencia and Parque 9 de Julio, San Miguel de Tucumán.
    private static readonly Coordinates SampleOrigin = new(-26.8305, -65.2039);
    private static readonly Coordinates SampleDestination = new(-26.8317, -65.1912);

    public static IEndpointRouteBuilder MapMissionEndpoints(this IEndpointRouteBuilder app)
    {
        var missions = app.MapGroup("/missions").WithTags("Missions");

        missions.MapPost("/", (RequestMissionRequest request) =>
            {
                var mission = new MissionResponse(
                    Guid.NewGuid(),
                    MissionStatus.Validated,
                    request.Origin,
                    request.Destination,
                    request.PackageWeightKg,
                    AssignedDroneId: null);
                return TypedResults.Created($"/missions/{mission.Id}", mission);
            })
            .WithName("RequestMission")
            .WithSummary("Request a package delivery.")
            .WithDescription("Validates the recipient and the weather. A rejected request returns 422 with the reason in `detail` and is not saved. If `callbackUrl` is set, the mission is POSTed to it when it ends as Completed or Failed.")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        missions.MapGet("/", () => TypedResults.Ok(new[]
            {
                SampleMission(Guid.NewGuid(), MissionStatus.Validated, assignedDroneId: null),
                SampleMission(Guid.NewGuid(), MissionStatus.InProgress, DroneEndpoints.SampleDroneId),
            }))
            .WithName("ListMissions")
            .WithSummary("List all missions with their status.");

        missions.MapGet("/{id:guid}", (Guid id) =>
                TypedResults.Ok(SampleMission(id, MissionStatus.InProgress, DroneEndpoints.SampleDroneId)))
            .WithName("GetMission")
            .WithSummary("Get one mission.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        missions.MapPost("/{id:guid}/payment", (Guid id) =>
                TypedResults.Ok(SampleMission(id, MissionStatus.Assigned, DroneEndpoints.SampleDroneId)))
            .WithName("PayMission")
            .WithSummary("Pay a validated mission.")
            .WithDescription("On approval the mission becomes Paid, and Assigned as soon as a drone is available. A rejected payment, or a mission that is not Validated, returns 422 with the reason in `detail`.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static MissionResponse SampleMission(Guid id, MissionStatus status, Guid? assignedDroneId) =>
        new(id, status, SampleOrigin, SampleDestination, PackageWeightKg: 1.5, assignedDroneId);
}
