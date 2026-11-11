using SkyRoute.Application.Drones;
using SkyRoute.Application.External;
using SkyRoute.Domain;
using SkyRoute.Domain.Drones;
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

        var drone = NearestAvailableDroneInRange(await drones.ListAsync(cancellationToken), mission.Route);
        if (drone is not null)
        {
            drone.MarkBusy();
            mission.Assign(drone.Id);
            await drones.UpdateAsync(drone, cancellationToken);
        }

        await missions.UpdateAsync(mission, cancellationToken);
        return ConfirmPaymentOutcome.Paid;
    }

    // Flight rules (#29): the available drone nearest to the origin whose full trip fits within range.
    private static Drone? NearestAvailableDroneInRange(IEnumerable<Drone> fleet, Route route) =>
        fleet
            .Where(d => d.Status == DroneStatus.Available && FlightRules.CanFly(d.HomePosition, route))
            .OrderBy(d => GeoDistance.Kilometers(d.HomePosition, route.Origin))
            .FirstOrDefault();
}
