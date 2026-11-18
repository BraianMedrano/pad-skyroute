using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using SkyRoute.Application.External;

namespace SkyRoute.Infrastructure.External;

public static class IntegrationServiceCollectionExtensions
{
    private const string DefaultPadiBaseUrl = "https://padi.runasp.net";

    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        if (!bool.TryParse(configuration["UseFakes"], out var useFakes))
        {
            return services;
        }

        if (useFakes)
        {
            services.AddSingleton<IIdentityService, FakeIdentityService>();
            services.AddSingleton<IWeatherService, FakeWeatherService>();
            services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
            services.AddSingleton<IWebhookNotifier, FakeWebhookNotifier>();
            return services;
        }

        // Register only available real adapters; never silently substitute Fakes.
        services.AddHttpClient<IIdentityService, RenaperIdentityService>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .AddStandardResilienceHandler();

        services.AddHttpClient<IWeatherService, OpenMeteoWeatherService>()
            .AddStandardResilienceHandler();

        services.AddHttpClient<IPaymentGateway, PadiPaymentGateway>((_, client) =>
            ConfigurePadiClient(client, configuration))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
            })
            .AddStandardResilienceHandler(options => options.Retry.DisableFor(HttpMethod.Post));

        // HttpWebhookNotifier only posts to loopback callbacks; mission completion and failure need it to resolve.
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

    internal static int GetTtlMinutes(IConfiguration configuration)
    {
        var configuredTtl = configuration["Padi:TtlMinutes"];
        if (configuredTtl is null)
        {
            return 30;
        }

        if (!int.TryParse(configuredTtl, NumberStyles.None, CultureInfo.InvariantCulture, out var ttlMinutes) || ttlMinutes <= 0)
        {
            throw new InvalidOperationException("Padi TTL configuration must be a positive number of minutes.");
        }

        return ttlMinutes;
    }

    private static void ConfigurePadiClient(HttpClient client, IConfiguration configuration)
    {
        var apiKey = configuration["Padi:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Padi API key configuration is required when real integrations are enabled.");
        }

        var baseUrl = configuration["Padi:BaseUrl"] ?? DefaultPadiBaseUrl;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(baseUri.UserInfo) ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw new InvalidOperationException("Padi base URL configuration must be an absolute HTTPS URL.");
        }

        _ = GetTtlMinutes(configuration);
        client.BaseAddress = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", apiKey);
    }
}
