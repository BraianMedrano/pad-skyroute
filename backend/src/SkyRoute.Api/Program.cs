using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using SkyRoute.Api.Endpoints;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        // SignalR (#6) needs credentials.
        .AllowCredentials()));

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

app.Run();
