using SkyRoute.Application.Missions;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Api.Payments;

// Padi has no webhooks: every few seconds, check the payment of each mission waiting for one.
public sealed class PaymentConfirmationService(IServiceScopeFactory scopes, ILogger<PaymentConfirmationService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopes.CreateScope();
            var missions = scope.ServiceProvider.GetRequiredService<IMissionRepository>();
            var confirmPayment = scope.ServiceProvider.GetRequiredService<ConfirmPayment>();

            var waiting = (await missions.ListAsync(stoppingToken))
                .Where(m => m is { Status: MissionStatus.Validated, PaymentLinkId: not null })
                .Select(m => m.Id)
                .ToList();

            foreach (var missionId in waiting)
            {
                try
                {
                    var outcome = await confirmPayment.ExecuteAsync(missionId, stoppingToken);
                    if (outcome == ConfirmPaymentOutcome.Paid)
                    {
                        logger.LogInformation("Payment confirmed for mission {MissionId}", missionId);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A failed check is retried on the next tick; it never changes the mission.
                    logger.LogWarning(ex, "Could not check the payment of mission {MissionId}", missionId);
                }
            }
        }
    }
}
