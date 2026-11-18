using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkyRoute.Application.External;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Infrastructure.External;

internal sealed class HttpWebhookNotifier(
    HttpClient client,
    IConfiguration configuration,
    IOptions<JsonOptions> jsonOptions,
    ILogger<HttpWebhookNotifier> logger) : IWebhookNotifier
{
    public Task NotifyMissionCompletedAsync(Mission mission, CancellationToken cancellationToken = default) =>
        NotifyAsync(mission, cancellationToken);

    public Task NotifyMissionFailedAsync(Mission mission, CancellationToken cancellationToken = default) =>
        NotifyAsync(mission, cancellationToken);

    private async Task NotifyAsync(Mission mission, CancellationToken cancellationToken)
    {
        if (mission.CallbackUrl is not { } callback)
            return;

        if (!callback.IsAbsoluteUri || (callback.Scheme is not ("http" or "https")) ||
            callback.Host is not ("127.0.0.1" or "[::1]") || callback.UserInfo.Length != 0)
        {
            logger.LogWarning("Webhook for mission {MissionId} skipped: only loopback HTTP(S) destinations are supported.", mission.Id);
            return;
        }

        try
        {
            var publicWebUrl = configuration["PublicWebUrl"] ?? "http://localhost:3000";
            // Mirror the public MissionResponse JSON without referencing the API layer.
            var payload = new
            {
                mission.Id,
                Status = mission.Status.ToString(),
                Origin = new { mission.Route.Origin.Lat, mission.Route.Origin.Lon },
                Destination = new { mission.Route.Destination.Lat, mission.Route.Destination.Lon },
                mission.PackageWeightKg,
                mission.Price,
                mission.AssignedDroneId,
                TrackingUrl = new Uri($"{publicWebUrl.TrimEnd('/')}/embed/missions/{mission.Id}"),
                mission.FailureReason,
            };

            using var response = await client.PostAsJsonAsync(callback, payload, jsonOptions.Value.SerializerOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Webhook for mission {MissionId} returned HTTP {StatusCode}.", mission.Id, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or Polly.ExecutionRejectedException or UriFormatException ||
                                   ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Webhook for mission {MissionId} failed ({FailureType}).", mission.Id, ex.GetType().Name);
        }
    }
}
