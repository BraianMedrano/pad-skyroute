using Microsoft.AspNetCore.Http.HttpResults;
using SkyRoute.Api.Contracts;
using SkyRoute.Application.Missions;
using Domain = SkyRoute.Domain;

namespace SkyRoute.Api.Endpoints;

public static class MissionEndpoints
{
    // Stub payment until the pay and assign use cases land (#11). The contract is what matters here.
    private const decimal SamplePrice = 3500m;

    // Base URL of the web app that serves the embeddable tracking page ("PublicWebUrl", env var PublicWebUrl).
    private const string DefaultPublicWebUrl = "http://localhost:3000";

    public static IEndpointRouteBuilder MapMissionEndpoints(this IEndpointRouteBuilder app)
    {
        var missions = app.MapGroup("/missions").WithTags("Missions");

        missions.MapPost("/", RequestMissionAsync)
            .WithName("RequestMission")
            .WithSummary("Request a package delivery.")
            .WithDescription("Validates the package weight (at most 2.5 kg), the flight range (at most 25 km from a drone base and back), the recipient, and the weather, and sets the mission price. A rejected request returns 422 with the reason in `detail` and is not saved. If `callbackUrl` is set, the same JSON as `GET /api/missions/{id}` is POSTed to it when the mission ends as Completed or Failed.")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        missions.MapGet("/", async (IMissionRepository repository, IConfiguration configuration, CancellationToken cancellationToken) =>
                TypedResults.Ok((await repository.ListAsync(cancellationToken)).Select(mission => ToResponse(mission, configuration)).ToArray()))
            .WithName("ListMissions")
            .WithSummary("List all missions with their status.");

        missions.MapGet("/{id:guid}", GetMissionAsync)
            .WithName("GetMission")
            .WithSummary("Get one mission.")
            .WithDescription("`trackingUrl` is a public page with the live map of the mission, meant to be embedded in an iframe.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        missions.MapPost("/{id:guid}/payment", (Guid id) =>
                TypedResults.Accepted($"/api/missions/{id}", new PaymentResponse(
                    id,
                    SamplePrice,
                    new Uri("https://padi-billetera.netlify.app/checkout/sample"),
                    DateTimeOffset.UtcNow.AddMinutes(30))))
            .WithName("PayMission")
            .WithSummary("Start the payment of a validated mission.")
            .WithDescription("Creates a Padi checkout link for the mission price and returns 202 with `checkoutUrl`. The mission stays Validated until the payment is confirmed, then becomes Paid, and Assigned as soon as a drone is available; follow it with `GET /api/missions/{id}`. If the link expires unpaid, call this endpoint again for a new one. A mission that is not Validated returns 422 with the reason in `detail`.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static async Task<Results<Created<MissionResponse>, ProblemHttpResult>> RequestMissionAsync(
        RequestMissionRequest request, RequestMission requestMission, IConfiguration configuration, CancellationToken cancellationToken)
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
                request.RecipientDni),
            cancellationToken);

        if (result.Mission is null)
        {
            return Unprocessable(result.RejectionReason ?? "Mission request rejected.");
        }

        var response = ToResponse(result.Mission, configuration);
        return TypedResults.Created($"/api/missions/{response.Id}", response);
    }

    private static async Task<Results<Ok<MissionResponse>, ProblemHttpResult>> GetMissionAsync(
        Guid id, IMissionRepository repository, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var mission = await repository.GetAsync(id, cancellationToken);
        return mission is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Mission {id} not found.")
            : TypedResults.Ok(ToResponse(mission, configuration));
    }

    private static ProblemHttpResult Unprocessable(string reason) =>
        TypedResults.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, detail: reason);

    private static MissionResponse ToResponse(Domain.Missions.Mission mission, IConfiguration configuration) => new(
        mission.Id,
        Enum.Parse<MissionStatus>(mission.Status.ToString()),
        ToContract(mission.Route.Origin),
        ToContract(mission.Route.Destination),
        mission.PackageWeightKg,
        mission.Price,
        mission.AssignedDroneId,
        TrackingUrl(configuration, mission.Id),
        mission.FailureReason);

    private static Domain.Coordinates ToDomain(Coordinates coordinates) => new(coordinates.Lat, coordinates.Lon);

    private static Coordinates ToContract(Domain.Coordinates coordinates) => new(coordinates.Lat, coordinates.Lon);

    private static Uri TrackingUrl(IConfiguration configuration, Guid missionId) =>
        new($"{(configuration["PublicWebUrl"] ?? DefaultPublicWebUrl).TrimEnd('/')}/embed/missions/{missionId}");
}
