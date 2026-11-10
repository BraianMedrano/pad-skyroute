using SkyRoute.Application.External;
using SkyRoute.Application.Missions;
using SkyRoute.Domain;
using SkyRoute.Domain.Drones;
using SkyRoute.Domain.Missions;
using SkyRoute.Infrastructure.External;

namespace SkyRoute.Application.Tests;

// Confirming the payment: Paid → Paid and Assigned; Pending or Expired → stays Validated.
public class ConfirmPaymentTests
{
    private static readonly Coordinates Centro = new(-26.8305, -65.2039);
    private static readonly Coordinates Parque9DeJulio = new(-26.8317, -65.1912);
    private static readonly Coordinates YerbaBuena = new(-26.8160, -65.3160);
    private static readonly Coordinates Famailla = new(-27.0540, -65.4030);

    private readonly InMemoryMissionRepository _missions = new();

    private ConfirmPayment UseCase(InMemoryDroneRepository drones, PaymentStatus status = PaymentStatus.Paid) =>
        new(_missions, drones, new FakePaymentGateway(status));

    private async Task<Mission> MissionAwaitingPayment()
    {
        var mission = Mission.RequestDelivery(new Route(Centro, Parque9DeJulio), 1.5, "30111222");
        mission.Validate(2634m);
        mission.AttachPaymentLink("link-1");
        await _missions.AddAsync(mission);
        return mission;
    }

    [Fact]
    public async Task A_paid_link_leaves_the_mission_Assigned_and_its_drone_Busy()
    {
        var drone = new Drone(Guid.NewGuid(), "Falcon-1", Centro);
        var drones = new InMemoryDroneRepository(drone);
        var mission = await MissionAwaitingPayment();

        var outcome = await UseCase(drones).ExecuteAsync(mission.Id);

        Assert.Equal(ConfirmPaymentOutcome.Paid, outcome);
        var saved = await _missions.GetAsync(mission.Id);
        Assert.Equal(MissionStatus.Assigned, saved!.Status);
        Assert.Equal(drone.Id, saved.AssignedDroneId);
        Assert.Equal(DroneStatus.Busy, (await drones.GetAsync(drone.Id))!.Status);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending, ConfirmPaymentOutcome.Pending)]
    [InlineData(PaymentStatus.Expired, ConfirmPaymentOutcome.Expired)]
    public async Task A_pending_or_expired_link_leaves_the_mission_Validated(PaymentStatus status, ConfirmPaymentOutcome expected)
    {
        var drone = new Drone(Guid.NewGuid(), "Falcon-1", Centro);
        var drones = new InMemoryDroneRepository(drone);
        var mission = await MissionAwaitingPayment();

        var outcome = await UseCase(drones, status).ExecuteAsync(mission.Id);

        Assert.Equal(expected, outcome);
        Assert.Equal(MissionStatus.Validated, (await _missions.GetAsync(mission.Id))!.Status);
        Assert.Equal(DroneStatus.Available, (await drones.GetAsync(drone.Id))!.Status);
    }

    [Fact]
    public async Task With_no_available_drone_the_mission_stays_Paid()
    {
        var busy = new Drone(Guid.NewGuid(), "Falcon-1", Centro);
        busy.MarkBusy();
        var mission = await MissionAwaitingPayment();

        await UseCase(new InMemoryDroneRepository(busy)).ExecuteAsync(mission.Id);

        var saved = await _missions.GetAsync(mission.Id);
        Assert.Equal(MissionStatus.Paid, saved!.Status);
        Assert.Null(saved.AssignedDroneId);
    }

    [Fact]
    public async Task The_assigned_drone_is_the_nearest_available_one_to_the_origin()
    {
        var far = new Drone(Guid.NewGuid(), "Falcon-3", YerbaBuena);
        var near = new Drone(Guid.NewGuid(), "Falcon-1", Centro);
        var mission = await MissionAwaitingPayment();

        await UseCase(new InMemoryDroneRepository(far, near)).ExecuteAsync(mission.Id);

        Assert.Equal(near.Id, (await _missions.GetAsync(mission.Id))!.AssignedDroneId);
    }

    [Fact]
    public async Task An_available_drone_out_of_range_is_not_assigned()
    {
        // From Famaillá the trip to Parque 9 de Julio and back is far over 25 km.
        var outOfRange = new Drone(Guid.NewGuid(), "Falcon-9", Famailla);
        var mission = await MissionAwaitingPayment();

        await UseCase(new InMemoryDroneRepository(outOfRange)).ExecuteAsync(mission.Id);

        var saved = await _missions.GetAsync(mission.Id);
        Assert.Equal(MissionStatus.Paid, saved!.Status);
        Assert.Null(saved.AssignedDroneId);
    }

    [Fact]
    public async Task A_mission_without_a_payment_link_is_not_awaiting_payment()
    {
        var mission = Mission.RequestDelivery(new Route(Centro, Parque9DeJulio), 1.5, "30111222");
        mission.Validate(2634m);
        await _missions.AddAsync(mission);

        var outcome = await UseCase(new InMemoryDroneRepository()).ExecuteAsync(mission.Id);

        Assert.Equal(ConfirmPaymentOutcome.NotAwaitingPayment, outcome);
        Assert.Equal(MissionStatus.Validated, (await _missions.GetAsync(mission.Id))!.Status);
    }
}
