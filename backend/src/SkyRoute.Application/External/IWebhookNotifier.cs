using SkyRoute.Domain.Missions;

namespace SkyRoute.Application.External;

public interface IWebhookNotifier
{
    Task NotifyMissionCompletedAsync(Mission mission, CancellationToken cancellationToken = default);
    Task NotifyMissionFailedAsync(Mission mission, CancellationToken cancellationToken = default);
}
