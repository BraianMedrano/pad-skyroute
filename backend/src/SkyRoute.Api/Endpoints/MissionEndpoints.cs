using SkyRoute.Api.Contracts;

namespace SkyRoute.Api.Endpoints;

public static class MissionEndpoints
{
    // Stub data until the use cases land (#10, #11). The contract is what matters here.
    // Plaza Independencia and Parque 9 de Julio, San Miguel de Tucumán.
    private static readonly Coordinates SampleOrigin = new(-26.8305, -65.2039);
    private static readonly Coordinates SampleDestination = new(-26.8317, -65.1912);
    private const decimal SamplePrice = 3500m;

    // Base URL of the web app that serves the embeddable tracking page ("PublicWebUrl", env var PublicWebUrl).
    private const string DefaultPublicWebUrl = "http://localhost:3000";

    public static IEndpointRouteBuilder MapMissionEndpoints(this IEndpointRouteBuilder app)
    {
        var missions = app.MapGroup("/missions").WithTags("Missions");

        missions.MapPost("/", (RequestMissionRequest request, IConfiguration configuration) =>
            {
                var id = Guid.NewGuid();
                var mission = new MissionResponse(
                    id,
                    MissionStatus.Validated,
                    request.Origin,
                    request.Destination,
                    request.PackageWeightKg,
                    SamplePrice,
                    AssignedDroneId: null,
                    TrackingUrl(configuration, id));
                return TypedResults.Created($"/api/missions/{mission.Id}", mission);
            })
            .WithName("RequestMission")
            .WithSummary("Request a package delivery.")
            .WithDescription("Validates the recipient and the weather. A rejected request returns 422 with the reason in `detail` and is not saved. If `callbackUrl` is set, the same JSON as `GET /api/missions/{id}` is POSTed to it when the mission ends as Completed or Failed. If an external service is unavailable, it returns 503 with the reason in `detail`, and the call can be retried.")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        missions.MapGet("/", (IConfiguration configuration) => TypedResults.Ok(new[]
            {
                SampleMission(configuration, Guid.NewGuid(), MissionStatus.Validated, assignedDroneId: null),
                SampleMission(configuration, Guid.NewGuid(), MissionStatus.InProgress, DroneEndpoints.SampleDroneId),
            }))
            .WithName("ListMissions")
            .WithSummary("List all missions with their status.");

        missions.MapGet("/{id:guid}", (Guid id, IConfiguration configuration) =>
                TypedResults.Ok(SampleMission(configuration, id, MissionStatus.InProgress, DroneEndpoints.SampleDroneId)))
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
            .WithDescription("Creates a Padi checkout link for the mission price and returns 202 with `checkoutUrl`. The mission stays Validated until the payment is confirmed, then becomes Paid, and Assigned as soon as a drone is available; follow it with `GET /api/missions/{id}`. If the link expires unpaid, call this endpoint again for a new one. A mission that is not Validated returns 422 with the reason in `detail`. If an external service is unavailable, it returns 503 with the reason in `detail`, and the call can be retried.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static MissionResponse SampleMission(IConfiguration configuration, Guid id, MissionStatus status, Guid? assignedDroneId) =>
        new(id, status, SampleOrigin, SampleDestination, PackageWeightKg: 1.5, SamplePrice, assignedDroneId, TrackingUrl(configuration, id));

    private static Uri TrackingUrl(IConfiguration configuration, Guid missionId) =>
        new($"{(configuration["PublicWebUrl"] ?? DefaultPublicWebUrl).TrimEnd('/')}/embed/missions/{missionId}");
}
