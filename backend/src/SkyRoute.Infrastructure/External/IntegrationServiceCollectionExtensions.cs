using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkyRoute.Application.External;

namespace SkyRoute.Infrastructure.External;

public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        if (!bool.TryParse(configuration["UseFakes"], out var useFakes) || !useFakes)
        {
            return services;
        }

        services.AddSingleton<IIdentityService, FakeIdentityService>();
        services.AddSingleton<IWeatherService, FakeWeatherService>();
        services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
        services.AddSingleton<IWebhookNotifier, FakeWebhookNotifier>();
        return services;
    }
}
