using SkyRoute.Domain;

namespace SkyRoute.Application.External;

public sealed record WeatherSafetyResult(bool IsSafe, string? Reason = null);

public interface IWeatherService
{
    Task<WeatherSafetyResult> IsSafeToFlyAsync(Coordinates coordinates, CancellationToken cancellationToken = default);
}
