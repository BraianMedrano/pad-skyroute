using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using SkyRoute.Application.External;

namespace SkyRoute.Infrastructure.External;

public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        if (!bool.TryParse(configuration["UseFakes"], out var useFakes))
        {
            return services;
        }

        if (!useFakes)
        {
            // Weather only until the other real adapters land; never silently substitute Fakes.
            services.AddHttpClient<IWeatherService, OpenMeteoWeatherService>()
                .AddStandardResilienceHandler();
            return services;
        }

        services.AddSingleton<IIdentityService, FakeIdentityService>();
        services.AddSingleton<IWeatherService, FakeWeatherService>();
        services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
        services.AddSingleton<IWebhookNotifier, FakeWebhookNotifier>();
        return services;
    }

    // Call after AddIntegrations only in a local composition with a controlled receiver.
    public static IServiceCollection AddLocalHttpWebhookNotifier(this IServiceCollection services)
    {
        services.AddOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>();
        services.AddHttpClient<IWebhookNotifier, HttpWebhookNotifier>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false,
            })
            .AddStandardResilienceHandler(options => options.Retry.DisableFor(HttpMethod.Post));
        return services;
    }
}
