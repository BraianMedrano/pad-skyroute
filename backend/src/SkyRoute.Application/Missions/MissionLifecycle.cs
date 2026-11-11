using SkyRoute.Application.Drones;
using SkyRoute.Application.External;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Application.Missions;

// Called by the simulator (#13), not by HTTP. Each use case returns null when the mission does not
// exist; an invalid transition throws InvalidMissionTransitionException.

// Assigned → InProgress.
public sealed class StartMission(IMissionRepository missions)
{
    public async Task<Mission?> ExecuteAsync(Guid missionId, CancellationToken cancellationToken = default)
    {
        var mission = await missions.GetAsync(missionId, cancellationToken);
        if (mission is null)
        {
            return null;
        }

        mission.Start();
        await missions.UpdateAsync(mission, cancellationToken);
        return mission;
    }
}

// InProgress → Completed. Frees the drone and notifies the consumer.
public sealed class CompleteMission(IMissionRepository missions, IDroneRepository drones, IWebhookNotifier webhook)
{
    public async Task<Mission?> ExecuteAsync(Guid missionId, CancellationToken cancellationToken = default)
    {
        var mission = await missions.GetAsync(missionId, cancellationToken);
        if (mission is null)
        {
            return null;
        }

        mission.Complete();
        await MissionEnd.FreeDroneAsync(mission, missions, drones, cancellationToken);
        await webhook.NotifyMissionCompletedAsync(mission, cancellationToken);
        return mission;
    }
}

// InProgress → Failed with a reason. Frees the drone and notifies the consumer.
public sealed class FailMission(IMissionRepository missions, IDroneRepository drones, IWebhookNotifier webhook)
{
    public async Task<Mission?> ExecuteAsync(Guid missionId, string reason, CancellationToken cancellationToken = default)
    {
        var mission = await missions.GetAsync(missionId, cancellationToken);
        if (mission is null)
        {
            return null;
        }

        mission.Fail(reason);
        await MissionEnd.FreeDroneAsync(mission, missions, drones, cancellationToken);
        await webhook.NotifyMissionFailedAsync(mission, cancellationToken);
        return mission;
    }
}

internal static class MissionEnd
{
    // Saves the ended mission, frees its drone, and hands the drone to the oldest Paid mission in range.
    // Everything is saved before the webhook is called, so a failed webhook never changes this state.
    public static async Task FreeDroneAsync(
        Mission ended, IMissionRepository missions, IDroneRepository drones, CancellationToken cancellationToken)
    {
        await missions.UpdateAsync(ended, cancellationToken);

        if (ended.AssignedDroneId is not { } droneId || await drones.GetAsync(droneId, cancellationToken) is not { } drone)
        {
            return;
        }

        drone.MarkAvailable();

        var next = DroneAssignment.OldestPaidMissionInRange(await missions.ListAsync(cancellationToken), drone);
        if (next is not null)
        {
            drone.MarkBusy();
            next.Assign(drone.Id);
            await missions.UpdateAsync(next, cancellationToken);
        }

        await drones.UpdateAsync(drone, cancellationToken);
    }
}
