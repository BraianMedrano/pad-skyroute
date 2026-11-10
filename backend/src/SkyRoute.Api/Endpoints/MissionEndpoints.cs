using Microsoft.AspNetCore.Http.HttpResults;
using SkyRoute.Api.Contracts;
using SkyRoute.Application.Missions;
using Domain = SkyRoute.Domain;

namespace SkyRoute.Api.Endpoints;

public static class MissionEndpoints
{
    public static IEndpointRouteBuilder MapMissionEndpoints(this IEndpointRouteBuilder app)
    {
        var missions = app.MapGroup("/missions").WithTags("Missions");

        missions.MapPost("/", RequestMissionAsync)
            .WithName("RequestMission")
            .WithSummary("Request a package delivery.")
            .WithDescription("Validates the package weight (at most 2.5 kg), the flight range (at most 25 km from a drone base and back), the recipient, and the weather. A rejected request returns 422 with the reason in `detail` and is not saved. If `callbackUrl` is set, the mission is POSTed to it when it ends as Completed or Failed.")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        missions.MapGet("/", async (IMissionRepository repository, CancellationToken cancellationToken) =>
                TypedResults.Ok((await repository.ListAsync(cancellationToken)).Select(ToResponse).ToArray()))
            .WithName("ListMissions")
            .WithSummary("List all missions with their status.");

        missions.MapGet("/{id:guid}", GetMissionAsync)
            .WithName("GetMission")
            .WithSummary("Get one mission.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        missions.MapPost("/{id:guid}/payment", PayMissionAsync)
            .WithName("PayMission")
            .WithSummary("Pay a validated mission.")
            .WithDescription("On approval the mission becomes Paid, and Assigned as soon as a drone is available. A rejected payment, or a mission that is not Validated, returns 422 with the reason in `detail`.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static async Task<Results<Created<MissionResponse>, ProblemHttpResult>> RequestMissionAsync(
        RequestMissionRequest request, RequestMission requestMission, CancellationToken cancellationToken)
    {
        // The JSON body can omit required fields; reject them like any other invalid request.
        if (request.Origin is null || request.Destination is null || string.IsNullOrWhiteSpace(request.RecipientDni))
        {
            return Unprocessable("Origin, destination, and recipientDni are required.");
        }

        var result = await requestMission.ExecuteAsync(
            new RequestMissionCommand(
                ToDomain(request.Origin),
                ToDomain(request.Destination),
                request.PackageWeightKg,
                request.RecipientDni,
                request.CallbackUrl),
            cancellationToken);

        if (result.Mission is null)
        {
            return Unprocessable(result.RejectionReason ?? "Mission request rejected.");
        }

        var response = ToResponse(result.Mission);
        return TypedResults.Created($"/missions/{response.Id}", response);
    }

    private static async Task<Results<Ok<MissionResponse>, ProblemHttpResult>> GetMissionAsync(
        Guid id, IMissionRepository repository, CancellationToken cancellationToken)
    {
        var mission = await repository.GetAsync(id, cancellationToken);
        return mission is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Mission {id} not found.")
            : TypedResults.Ok(ToResponse(mission));
    }

    private static async Task<Results<Ok<MissionResponse>, ProblemHttpResult>> PayMissionAsync(
        Guid id, PayMission payMission, CancellationToken cancellationToken)
    {
        var result = await payMission.ExecuteAsync(id, cancellationToken);
        return result.Outcome switch
        {
            PayMissionOutcome.Paid => TypedResults.Ok(ToResponse(result.Mission!)),
            PayMissionOutcome.NotFound => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Mission {id} not found."),
            _ => Unprocessable(result.RejectionReason ?? "Payment rejected."),
        };
    }

    private static ProblemHttpResult Unprocessable(string reason) =>
        TypedResults.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, detail: reason);

    private static MissionResponse ToResponse(Domain.Missions.Mission mission) => new(
        mission.Id,
        Enum.Parse<MissionStatus>(mission.Status.ToString()),
        ToContract(mission.Route.Origin),
        ToContract(mission.Route.Destination),
        mission.PackageWeightKg,
        mission.AssignedDroneId,
        mission.FailureReason);

    private static Domain.Coordinates ToDomain(Coordinates coordinates) => new(coordinates.Lat, coordinates.Lon);

    private static Coordinates ToContract(Domain.Coordinates coordinates) => new(coordinates.Lat, coordinates.Lon);
}
