using SkyRoute.Api.Hubs;
using SkyRoute.Infrastructure.Simulation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSimulation<TelemetryHub>();

var app = builder.Build();

// Contract first: the OpenAPI document is always published so other groups can integrate.
app.MapOpenApi();

app.UseHttpsRedirection();

app.MapHub<TelemetryHub>("/hubs/telemetry");

app.Run();
