using SkyRoute.Api.Contracts;

namespace SkyRoute.Api.Endpoints;

public static class MissionEndpoints
{
    // Stub data until the use cases land (#10, #11). The contract is what matters here.
    // Plaza Independencia and Parque 9 de Julio, San Miguel de Tucumán.
    private static readonly Coordinates SampleOrigin = new(-26.8305, -65.2039);
    private static readonly Coordinates SampleDestination = new(-26.8317, -65.1912);
    private const decimal SamplePrice = 3500m;

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
                    SamplePrice,
                    AssignedDroneId: null);
                return TypedResults.Created($"/missions/{mission.Id}", mission);
            })
            .WithName("RequestMission")
            .WithSummary("Request a package delivery.")
            .WithDescription("Validates the recipient and the weather. A rejected request returns 422 with the reason in `detail` and is not saved. If `callbackUrl` is set, the same JSON as `GET /missions/{id}` is POSTed to it when the mission ends as Completed or Failed.")
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
                TypedResults.Accepted($"/missions/{id}", new PaymentResponse(
                    id,
                    SamplePrice,
                    new Uri("https://padi-billetera.netlify.app/checkout/sample"),
                    DateTimeOffset.UtcNow.AddMinutes(30))))
            .WithName("PayMission")
            .WithSummary("Start the payment of a validated mission.")
            .WithDescription("Creates a Padi checkout link for the mission price and returns 202 with `checkoutUrl`. The mission stays Validated until the payment is confirmed, then becomes Paid, and Assigned as soon as a drone is available; follow it with `GET /missions/{id}`. If the link expires unpaid, call this endpoint again for a new one. A mission that is not Validated returns 422 with the reason in `detail`.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static MissionResponse SampleMission(Guid id, MissionStatus status, Guid? assignedDroneId) =>
        new(id, status, SampleOrigin, SampleDestination, PackageWeightKg: 1.5, SamplePrice, assignedDroneId);
}
