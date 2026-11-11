using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using SkyRoute.Api.Endpoints;
using SkyRoute.Api.Hubs;
using SkyRoute.Application.External;
using SkyRoute.Infrastructure.Simulation;

const string FrontendCorsPolicy = "Frontend";

// Every public route lives under /api: in production nginx sends /api to this container and the rest to the web.
const string OpenApiRoute = "/api/openapi/{documentName}.json";

const string ApiDescription = """
    SkyRoute schedules package deliveries with simulated drones in San Miguel de Tucumán.

    - Every route lives under `/api`. The demo base URL is `https://skyroute.casteltech.ar`.
    - No authentication: no API key or token is needed.
    - Payment is asynchronous: `POST /api/missions/{id}/payment` returns a Padi `checkoutUrl`; follow the mission with `GET /api/missions/{id}`.
    - If `callbackUrl` is set, the mission JSON is POSTed to it when the mission ends as Completed or Failed.
    - `trackingUrl` is a public page with the live map of the mission, meant to be embedded in an iframe.
    - Errors use ProblemDetails with the reason in `detail`: 422 for a rejected request, 503 when an external service (identity, weather, payments) is unavailable, so the call can be retried.
    - Changes are additive only: new endpoints or fields may appear; existing ones are never renamed or removed.
    """;

var builder = WebApplication.CreateBuilder(args);

// When set, the published document names this public URL instead of the host that served it (empty in Development).
var publicApiUrl = builder.Configuration["PublicApiUrl"];
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Description = ApiDescription;
    if (!string.IsNullOrWhiteSpace(publicApiUrl))
    {
        document.Servers = [new OpenApiServer { Url = publicApiUrl }];
    }
    return Task.CompletedTask;
}));
// An unavailable external service becomes 503 with the reason in `detail`; anything else stays 500.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    if (context.Exception is ExternalServiceUnavailableException unavailable)
    {
        context.ProblemDetails.Detail = unavailable.Message;
    }
});
SkyRoute.Infrastructure.External.IntegrationServiceCollectionExtensions.AddIntegrations(builder.Services, builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    // Publish numbers as "number" only; the web defaults also accept strings ("number | string").
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        // SignalR (#6) needs credentials.
        .AllowCredentials()));
builder.Services.AddSimulation<TelemetryHub>();

var app = builder.Build();

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is ExternalServiceUnavailableException
        ? StatusCodes.Status503ServiceUnavailable
        : StatusCodes.Status500InternalServerError,
});

// Contract first: the OpenAPI document and its reference page are always published so other groups can integrate.
app.MapOpenApi(OpenApiRoute);
app.MapScalarApiReference("/api/scalar", options => options.WithOpenApiRoutePattern(OpenApiRoute));

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);

var api = app.MapGroup("/api");
api.MapMissionEndpoints();
api.MapDroneEndpoints();
api.MapHub<TelemetryHub>("/hubs/telemetry");

app.Run();
