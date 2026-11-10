using Microsoft.Extensions.Logging;
using SkyRoute.Application.External;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Infrastructure.External;

public sealed class FakeWebhookNotifier(ILogger<FakeWebhookNotifier> logger, bool shouldFail = false) : IWebhookNotifier
{
    public Task NotifyMissionCompletedAsync(Mission mission, CancellationToken cancellationToken = default) =>
        NotifyAsync(mission, MissionStatus.Completed, cancellationToken);

    public Task NotifyMissionFailedAsync(Mission mission, CancellationToken cancellationToken = default) =>
        NotifyAsync(mission, MissionStatus.Failed, cancellationToken);

    private Task NotifyAsync(Mission mission, MissionStatus status, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (shouldFail)
        {
            throw new InvalidOperationException("Fake webhook notification failed");
        }

        logger.LogInformation("Fake webhook notification: mission {MissionId} is {MissionStatus}", mission.Id, status);
        return Task.CompletedTask;
    }
}
