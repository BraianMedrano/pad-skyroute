using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using SkyRoute.Api.Endpoints;
using SkyRoute.Api.Hubs;
using SkyRoute.Infrastructure.Persistence;
using SkyRoute.Infrastructure.Simulation;

const string FrontendCorsPolicy = "Frontend";

// Every public route lives under /api: in production nginx sends /api to this container and the rest to the web.
const string OpenApiRoute = "/api/openapi/{documentName}.json";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
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
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddScoped<SkyRoute.Application.Missions.RequestMission>();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

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
