using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace SkyRoute.Infrastructure.Simulation;

public static class SimulationServiceCollectionExtensions
{
    /// <summary>
    /// Registers SignalR, the telemetry broadcaster for <typeparamref name="THub"/>, and the drone simulation.
    /// The hub type comes from the Api so Infrastructure never references it.
    /// </summary>
    public static IServiceCollection AddSimulation<THub>(this IServiceCollection services)
        where THub : Hub
    {
        services.AddSignalR()
            .AddJsonProtocol(options =>
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddSingleton(sp => new TelemetryBroadcaster(sp.GetRequiredService<IHubContext<THub>>().Clients));
        services.AddSingleton<TelemetryStore>();
        services.AddHostedService<DroneSimulator>();

        return services;
    }
}
