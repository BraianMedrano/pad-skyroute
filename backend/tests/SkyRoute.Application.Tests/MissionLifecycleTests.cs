using SkyRoute.Application.Missions;
using SkyRoute.Domain;
using SkyRoute.Domain.Drones;
using SkyRoute.Domain.Missions;
using SkyRoute.Infrastructure.External;

namespace SkyRoute.Application.Tests;

public class MissionLifecycleTests
{
    private static readonly Coordinates Centro = new(-26.8305, -65.2039);
    private static readonly Coordinates Parque9DeJulio = new(-26.8317, -65.1912);
    private static readonly Coordinates Famailla = new(-27.0540, -65.4030);
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    private readonly InMemoryMissionRepository _missions = new();
    private readonly InMemoryDroneRepository _drones;
    private readonly Drone _drone = new(Guid.NewGuid(), "Falcon-1", Centro);
    private readonly ListLogger<FakeWebhookNotifier> _webhookLog = new();

    public MissionLifecycleTests() => _drones = new InMemoryDroneRepository(_drone);

    private FakeWebhookNotifier Webhook(bool shouldFail = false) => new(_webhookLog, shouldFail);

    private StartMission Start() => new(_missions);

    private CompleteMission Complete(bool webhookFails = false) => new(_missions, _drones, Webhook(webhookFails));

    private FailMission Fail() => new(_missions, _drones, Webhook());

    private async Task<Mission> MissionIn(MissionStatus status, Coordinates? origin = null, DateTimeOffset? requestedAt = null)
    {
        var mission = Mission.RequestDelivery(
            new Route(origin ?? Centro, Parque9DeJulio), 1.5, "30111222", requestedAt: requestedAt ?? Now);
        mission.Validate(2634m);
        if (status >= MissionStatus.Paid) mission.MarkPaid();
        if (status >= MissionStatus.Assigned)
        {
            _drone.MarkBusy();
            mission.Assign(_drone.Id);
        }
        if (status >= MissionStatus.InProgress) mission.Start();
        await _missions.AddAsync(mission);
        return mission;
    }

    [Fact]
    public async Task Starting_an_assigned_mission_moves_it_to_InProgress()
    {
        var mission = await MissionIn(MissionStatus.Assigned);

        await Start().ExecuteAsync(mission.Id);

        Assert.Equal(MissionStatus.InProgress, (await _missions.GetAsync(mission.Id))!.Status);
    }

    [Fact]
    public async Task Completing_a_mission_frees_its_drone_and_notifies_the_webhook()
    {
        var mission = await MissionIn(MissionStatus.InProgress);

        await Complete().ExecuteAsync(mission.Id);

        Assert.Equal(MissionStatus.Completed, (await _missions.GetAsync(mission.Id))!.Status);
        Assert.Equal(DroneStatus.Available, (await _drones.GetAsync(_drone.Id))!.Status);
        Assert.Equal([$"Fake webhook notification: mission {mission.Id} is Completed"], _webhookLog.Messages);
    }

    [Fact]
    public async Task Failing_a_mission_keeps_the_reason_frees_its_drone_and_notifies_the_webhook()
    {
        var mission = await MissionIn(MissionStatus.InProgress);

        await Fail().ExecuteAsync(mission.Id, "Battery depleted");

        var saved = await _missions.GetAsync(mission.Id);
        Assert.Equal(MissionStatus.Failed, saved!.Status);
        Assert.Equal("Battery depleted", saved.FailureReason);
        Assert.Equal(DroneStatus.Available, (await _drones.GetAsync(_drone.Id))!.Status);
        Assert.Equal([$"Fake webhook notification: mission {mission.Id} is Failed"], _webhookLog.Messages);
    }

    [Fact]
    public async Task A_freed_drone_is_assigned_to_the_oldest_Paid_mission()
    {
        var flying = await MissionIn(MissionStatus.InProgress);
        var newer = await MissionIn(MissionStatus.Paid, requestedAt: Now.AddMinutes(10));
        var older = await MissionIn(MissionStatus.Paid, requestedAt: Now.AddMinutes(5));

        await Complete().ExecuteAsync(flying.Id);

        var assigned = await _missions.GetAsync(older.Id);
        Assert.Equal(MissionStatus.Assigned, assigned!.Status);
        Assert.Equal(_drone.Id, assigned.AssignedDroneId);
        Assert.Equal(MissionStatus.Paid, (await _missions.GetAsync(newer.Id))!.Status);
        Assert.Equal(DroneStatus.Busy, (await _drones.GetAsync(_drone.Id))!.Status);
    }

    [Fact]
    public async Task A_freed_drone_skips_Paid_missions_out_of_its_range()
    {
        var flying = await MissionIn(MissionStatus.InProgress);
        var outOfRange = await MissionIn(MissionStatus.Paid, origin: Famailla, requestedAt: Now.AddMinutes(1));
        var inRange = await MissionIn(MissionStatus.Paid, requestedAt: Now.AddMinutes(2));

        await Complete().ExecuteAsync(flying.Id);

        Assert.Equal(MissionStatus.Paid, (await _missions.GetAsync(outOfRange.Id))!.Status);
        Assert.Equal(MissionStatus.Assigned, (await _missions.GetAsync(inRange.Id))!.Status);
    }

    [Fact]
    public async Task A_failed_webhook_does_not_change_the_completed_mission_or_the_freed_drone()
    {
        var mission = await MissionIn(MissionStatus.InProgress);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Complete(webhookFails: true).ExecuteAsync(mission.Id));

        Assert.Equal(MissionStatus.Completed, (await _missions.GetAsync(mission.Id))!.Status);
        Assert.Equal(DroneStatus.Available, (await _drones.GetAsync(_drone.Id))!.Status);
    }
}
