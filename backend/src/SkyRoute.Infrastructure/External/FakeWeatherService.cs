using SkyRoute.Application.External;
using SkyRoute.Domain;

namespace SkyRoute.Infrastructure.External;

public sealed class FakeWeatherService(bool isSafe = true, string? unsafeReason = "Unsafe weather conditions") : IWeatherService
{
    public Task<WeatherSafetyResult> IsSafeToFlyAsync(Coordinates coordinates, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new WeatherSafetyResult(isSafe, isSafe ? null : unsafeReason));
    }
}
