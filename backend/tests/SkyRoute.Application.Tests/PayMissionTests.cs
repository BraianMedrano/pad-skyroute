using SkyRoute.Application.Missions;
using SkyRoute.Domain;
using SkyRoute.Domain.Drones;
using SkyRoute.Domain.Missions;
using SkyRoute.Infrastructure.External;

namespace SkyRoute.Application.Tests;

public class PayMissionTests
{
    private static readonly Coordinates Centro = new(-26.8305, -65.2039);
    private static readonly Coordinates Parque9DeJulio = new(-26.8317, -65.1912);
    private static readonly Coordinates YerbaBuena = new(-26.8160, -65.3160);
    private static readonly Coordinates Famailla = new(-27.0540, -65.4030);

    private readonly InMemoryMissionRepository _missions = new();

    private PayMission UseCase(InMemoryDroneRepository drones, bool paymentApproved = true) =>
        new(_missions, drones, new FakePaymentGateway(approved: paymentApproved, rejectionReason: "Insufficient funds"));

    private async Task<Mission> ValidatedMission(Coordinates origin, Coordinates destination)
    {
        var mission = Mission.RequestDelivery(new Route(origin, destination), 1.5, "30111222");
        mission.Validate();
        await _missions.AddAsync(mission);
        return mission;
    }

    [Fact]
    public async Task An_approved_payment_leaves_the_mission_Assigned_and_its_drone_Busy()
    {
        var drone = new Drone(Guid.NewGuid(), "Falcon-1", Centro);
        var drones = new InMemoryDroneRepository(drone);
        var mission = await ValidatedMission(Centro, Parque9DeJulio);

        var result = await UseCase(drones).ExecuteAsync(mission.Id);

        Assert.Equal(PayMissionOutcome.Paid, result.Outcome);
        var saved = await _missions.GetAsync(mission.Id);
        Assert.Equal(MissionStatus.Assigned, saved!.Status);
        Assert.Equal(drone.Id, saved.AssignedDroneId);
        Assert.Equal(DroneStatus.Busy, (await drones.GetAsync(drone.Id))!.Status);
    }

    [Fact]
    public async Task A_rejected_payment_returns_the_reason_and_the_mission_stays_Validated()
    {
        var drone = new Drone(Guid.NewGuid(), "Falcon-1", Centro);
        var drones = new InMemoryDroneRepository(drone);
        var mission = await ValidatedMission(Centro, Parque9DeJulio);

        var result = await UseCase(drones, paymentApproved: false).ExecuteAsync(mission.Id);

        Assert.Equal(PayMissionOutcome.Rejected, result.Outcome);
        Assert.Equal("Insufficient funds", result.RejectionReason);
        Assert.Equal(MissionStatus.Validated, (await _missions.GetAsync(mission.Id))!.Status);
        Assert.Equal(DroneStatus.Available, (await drones.GetAsync(drone.Id))!.Status);
    }

    [Fact]
    public async Task With_no_available_drone_the_mission_stays_Paid()
    {
        var busy = new Drone(Guid.NewGuid(), "Falcon-1", Centro);
        busy.MarkBusy();
        var mission = await ValidatedMission(Centro, Parque9DeJulio);

        var result = await UseCase(new InMemoryDroneRepository(busy)).ExecuteAsync(mission.Id);

        Assert.Equal(PayMissionOutcome.Paid, result.Outcome);
        var saved = await _missions.GetAsync(mission.Id);
        Assert.Equal(MissionStatus.Paid, saved!.Status);
        Assert.Null(saved.AssignedDroneId);
    }

    [Fact]
    public async Task The_assigned_drone_is_the_nearest_available_one_to_the_origin()
    {
        var far = new Drone(Guid.NewGuid(), "Falcon-3", YerbaBuena);
        var near = new Drone(Guid.NewGuid(), "Falcon-1", Centro);
        var mission = await ValidatedMission(Centro, Parque9DeJulio);

        await UseCase(new InMemoryDroneRepository(far, near)).ExecuteAsync(mission.Id);

        Assert.Equal(near.Id, (await _missions.GetAsync(mission.Id))!.AssignedDroneId);
    }

    [Fact]
    public async Task An_available_drone_out_of_range_is_not_assigned()
    {
        // From Famaillá the trip to Parque 9 de Julio and back is far over 25 km.
        var outOfRange = new Drone(Guid.NewGuid(), "Falcon-9", Famailla);
        var mission = await ValidatedMission(Centro, Parque9DeJulio);

        await UseCase(new InMemoryDroneRepository(outOfRange)).ExecuteAsync(mission.Id);

        var saved = await _missions.GetAsync(mission.Id);
        Assert.Equal(MissionStatus.Paid, saved!.Status);
        Assert.Null(saved.AssignedDroneId);
    }

    [Fact]
    public async Task Paying_a_mission_that_is_not_Validated_is_rejected_without_charging()
    {
        var drones = new InMemoryDroneRepository(new Drone(Guid.NewGuid(), "Falcon-1", Centro));
        var mission = await ValidatedMission(Centro, Parque9DeJulio);
        await UseCase(drones).ExecuteAsync(mission.Id);

        var second = await UseCase(drones, paymentApproved: false).ExecuteAsync(mission.Id);

        Assert.Equal(PayMissionOutcome.Rejected, second.Outcome);
        Assert.Equal("Only a Validated mission can be paid; this one is Assigned.", second.RejectionReason);
        Assert.Equal(MissionStatus.Assigned, (await _missions.GetAsync(mission.Id))!.Status);
    }

    [Fact]
    public async Task Paying_an_unknown_mission_returns_not_found()
    {
        var result = await UseCase(new InMemoryDroneRepository()).ExecuteAsync(Guid.NewGuid());

        Assert.Equal(PayMissionOutcome.NotFound, result.Outcome);
    }
}
