using SkyRoute.Application.External;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Application.Missions;

public enum PayMissionOutcome
{
    Started,
    Rejected,
    NotFound,
}

public sealed record PayMissionResult(PayMissionOutcome Outcome, PaymentLink? PaymentLink = null, string? RejectionReason = null);

// Starts the payment: creates a checkout link for the mission price. The mission stays Validated
// until ConfirmPayment sees the link paid. Calling it again replaces the link.
public sealed class PayMission(IMissionRepository missions, IPaymentGateway payments)
{
    public async Task<PayMissionResult> ExecuteAsync(Guid missionId, CancellationToken cancellationToken = default)
    {
        var mission = await missions.GetAsync(missionId, cancellationToken);
        if (mission is null)
        {
            return new PayMissionResult(PayMissionOutcome.NotFound);
        }

        if (mission.Status != MissionStatus.Validated)
        {
            return new PayMissionResult(
                PayMissionOutcome.Rejected,
                RejectionReason: $"Only a Validated mission can be paid; this one is {mission.Status}.");
        }

        var link = await payments.CreatePaymentLinkAsync(mission, mission.Price, cancellationToken);
        mission.AttachPaymentLink(link.Id);
        await missions.UpdateAsync(mission, cancellationToken);
        return new PayMissionResult(PayMissionOutcome.Started, link);
    }
}
