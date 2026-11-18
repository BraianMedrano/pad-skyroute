using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Polly;
using SkyRoute.Application.External;
using SkyRoute.Domain;

namespace SkyRoute.Infrastructure.External;

public sealed class OpenMeteoWeatherService : IWeatherService
{
    private readonly HttpClient httpClient;
    private readonly double maxWindSpeedKmh;
    private readonly double maxPrecipitationMm;

    public OpenMeteoWeatherService(HttpClient httpClient, IConfiguration configuration)
    {
        this.httpClient = httpClient;
        maxWindSpeedKmh = ReadThreshold(configuration, "Weather:MaxWindSpeedKmh", 30.0);
        maxPrecipitationMm = ReadThreshold(configuration, "Weather:MaxPrecipitationMm", 0.0);
    }

    public async Task<WeatherSafetyResult> IsSafeToFlyAsync(
        Coordinates coordinates, CancellationToken cancellationToken = default)
    {
        var uri = string.Create(CultureInfo.InvariantCulture,
            $"https://api.open-meteo.com/v1/forecast?latitude={coordinates.Lat}&longitude={coordinates.Lon}&current=wind_speed_10m,precipitation&wind_speed_unit=kmh&precipitation_unit=mm");

        try
        {
            using var response = await httpClient.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("current", out var current)
                || current.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Open-Meteo did not return current weather.");
            }

            var wind = ReadMeasurement(current, "wind_speed_10m");
            var precipitation = ReadMeasurement(current, "precipitation");
            if (wind > maxWindSpeedKmh || precipitation > maxPrecipitationMm)
            {
                return new WeatherSafetyResult(false, string.Create(CultureInfo.InvariantCulture,
                    $"Unsafe weather: wind {wind} km/h (maximum {maxWindSpeedKmh} km/h), precipitation {precipitation} mm (maximum {maxPrecipitationMm} mm)."));
            }

            return new WeatherSafetyResult(true);
        }
        catch (Exception ex) when (ex is HttpRequestException or ExecutionRejectedException or JsonException)
        {
            throw new ExternalServiceUnavailableException("Open-Meteo", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Provider timeouts are unavailable service errors; caller cancellation still propagates.
            throw new ExternalServiceUnavailableException("Open-Meteo", ex);
        }
    }

    private static double ReadMeasurement(JsonElement current, string name)
    {
        if (!current.TryGetProperty(name, out var reading)
            || reading.ValueKind != JsonValueKind.Number
            || !reading.TryGetDouble(out var value)
            || !double.IsFinite(value)
            || value < 0)
        {
            throw new JsonException($"Open-Meteo returned an invalid {name} reading.");
        }

        return value;
    }

    private static double ReadThreshold(IConfiguration configuration, string key, double defaultValue)
    {
        var value = configuration.GetValue<double?>(key) ?? defaultValue;
        if (!double.IsFinite(value) || value < 0)
        {
            throw new InvalidOperationException($"{key} must be a finite, non-negative number.");
        }

        return value;
    }
}
