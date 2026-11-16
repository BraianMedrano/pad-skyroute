using SkyRoute.Application.Drones;
using SkyRoute.Application.External;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Application.Missions;

public enum ConfirmPaymentOutcome
{
    Paid,
    Pending,
    Expired,
    NotAwaitingPayment,
}

// Validated → Paid → Assigned once the checkout link is paid.
// With no drone available in range, the mission stays Paid (#12 assigns it later).
public sealed class ConfirmPayment(IMissionRepository missions, IDroneRepository drones, IPaymentGateway payments)
{
    public async Task<ConfirmPaymentOutcome> ExecuteAsync(Guid missionId, CancellationToken cancellationToken = default)
    {
        var mission = await missions.GetAsync(missionId, cancellationToken);
        if (mission is not { Status: MissionStatus.Validated, PaymentLinkId: not null })
        {
            return ConfirmPaymentOutcome.NotAwaitingPayment;
        }

        var status = await payments.GetPaymentStatusAsync(mission.PaymentLinkId, cancellationToken);
        if (status != PaymentStatus.Paid)
        {
            return status == PaymentStatus.Expired ? ConfirmPaymentOutcome.Expired : ConfirmPaymentOutcome.Pending;
        }

        mission.MarkPaid();

        var drone = DroneAssignment.NearestAvailableDroneInRange(await drones.ListAsync(cancellationToken), mission.Route);
        if (drone is not null)
        {
            drone.MarkBusy();
            mission.Assign(drone.Id);
            // With EF Core both repositories share one DbContext per scope, so this save also writes the
            // mission in the same transaction. If another writer changed the drone or the mission after they
            // were read, it throws ConcurrentUpdateException, nothing is saved, and the poller retries.
            await drones.UpdateAsync(drone, cancellationToken);
        }

        await missions.UpdateAsync(mission, cancellationToken);
        return ConfirmPaymentOutcome.Paid;
    }
}
