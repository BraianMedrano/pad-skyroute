using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using SkyRoute.Api.Endpoints;
using SkyRoute.Api.Hubs;
using SkyRoute.Infrastructure.Simulation;

const string FrontendCorsPolicy = "Frontend";

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

var app = builder.Build();

// Contract first: the OpenAPI document is always published so other groups can integrate.
app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);

app.MapMissionEndpoints();
app.MapDroneEndpoints();

app.MapHub<TelemetryHub>("/hubs/telemetry");

app.Run();
