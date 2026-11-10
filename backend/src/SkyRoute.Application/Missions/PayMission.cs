using SkyRoute.Application.Drones;
using SkyRoute.Application.External;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Application.Missions;

public enum PayMissionOutcome
{
    Paid,
    Rejected,
    NotFound,
}

public sealed record PayMissionResult(PayMissionOutcome Outcome, Mission? Mission = null, string? RejectionReason = null);

// Validated → Paid → Assigned. With no drone available in range, the mission stays Paid (#12 assigns it later).
public sealed class PayMission(IMissionRepository missions, IDroneRepository drones, IPaymentGateway payments)
{
    public async Task<PayMissionResult> ExecuteAsync(Guid missionId, CancellationToken cancellationToken = default)
    {
        var mission = await missions.GetAsync(missionId, cancellationToken);
        if (mission is null)
        {
            return new PayMissionResult(PayMissionOutcome.NotFound);
        }

        // Checked before charging, so a mission is never charged twice.
        if (mission.Status != MissionStatus.Validated)
        {
            return new PayMissionResult(
                PayMissionOutcome.Rejected,
                RejectionReason: $"Only a Validated mission can be paid; this one is {mission.Status}.");
        }

        var payment = await payments.ChargeMissionAsync(mission, cancellationToken);
        if (!payment.Approved)
        {
            return new PayMissionResult(PayMissionOutcome.Rejected, RejectionReason: payment.Reason ?? "Payment rejected.");
        }

        mission.MarkPaid();

        var drone = DroneAssignment.NearestAvailableDroneInRange(await drones.ListAsync(cancellationToken), mission.Route);
        if (drone is not null)
        {
            drone.MarkBusy();
            mission.Assign(drone.Id);
            await drones.UpdateAsync(drone, cancellationToken);
        }

        await missions.UpdateAsync(mission, cancellationToken);
        return new PayMissionResult(PayMissionOutcome.Paid, mission);
    }
}
