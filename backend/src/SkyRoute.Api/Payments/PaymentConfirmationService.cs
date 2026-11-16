using SkyRoute.Application.Missions;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Api.Payments;

// Padi has no webhooks: every few seconds, check the payment of each mission waiting for one.
// A failure is logged and retried on the next tick; it never stops the API.
public sealed class PaymentConfirmationService(IServiceScopeFactory scopes, ILogger<PaymentConfirmationService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            IReadOnlyList<Guid> waiting;
            try
            {
                waiting = await ListMissionsWaitingForPaymentAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Could not list the missions waiting for a payment");
                continue;
            }

            foreach (var missionId in waiting)
            {
                await ConfirmAsync(missionId, stoppingToken);
            }
        }
    }

    private async Task<IReadOnlyList<Guid>> ListMissionsWaitingForPaymentAsync(CancellationToken stoppingToken)
    {
        using var scope = scopes.CreateScope();
        var missions = scope.ServiceProvider.GetRequiredService<IMissionRepository>();
        return (await missions.ListAsync(stoppingToken))
            .Where(m => m is { Status: MissionStatus.Validated, PaymentLinkId: not null })
            .Select(m => m.Id)
            .ToList();
    }

    // One scope per mission: a failed confirmation leaves no tracked changes for the next one to save.
    private async Task ConfirmAsync(Guid missionId, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var confirmPayment = scope.ServiceProvider.GetRequiredService<ConfirmPayment>();
            var outcome = await confirmPayment.ExecuteAsync(missionId, stoppingToken);
            if (outcome == ConfirmPaymentOutcome.Paid)
            {
                logger.LogInformation("Payment confirmed for mission {MissionId}", missionId);
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            // Includes timeouts of the payment provider; the check runs again on the next tick.
            logger.LogWarning(ex, "Could not check the payment of mission {MissionId}", missionId);
        }
    }
}
