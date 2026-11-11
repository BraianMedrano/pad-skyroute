using SkyRoute.Application.Missions;
using SkyRoute.Domain;
using SkyRoute.Domain.Missions;
using SkyRoute.Infrastructure.External;

namespace SkyRoute.Application.Tests;

// Starting the payment: a Padi-style checkout link; the mission stays Validated until ConfirmPayment.
public class PayMissionTests
{
    private static readonly Coordinates Centro = new(-26.8305, -65.2039);
    private static readonly Coordinates Parque9DeJulio = new(-26.8317, -65.1912);
    private const decimal Price = 2634m;

    private readonly InMemoryMissionRepository _missions = new();

    private PayMission UseCase() => new(_missions, new FakePaymentGateway());

    private async Task<Mission> SavedMission(bool validated = true)
    {
        var mission = Mission.RequestDelivery(new Route(Centro, Parque9DeJulio), 1.5, "30111222");
        if (validated)
        {
            mission.Validate(Price);
        }
        await _missions.AddAsync(mission);
        return mission;
    }

    [Fact]
    public async Task Starting_the_payment_returns_a_checkout_link_for_the_price_and_the_mission_stays_Validated()
    {
        var mission = await SavedMission();

        var result = await UseCase().ExecuteAsync(mission.Id);

        Assert.Equal(PayMissionOutcome.Started, result.Outcome);
        Assert.NotNull(result.PaymentLink);
        Assert.Equal(Price, result.PaymentLink.Amount);
        var saved = await _missions.GetAsync(mission.Id);
        Assert.Equal(MissionStatus.Validated, saved!.Status);
        Assert.Equal(result.PaymentLink.Id, saved.PaymentLinkId);
    }

    [Fact]
    public async Task Starting_the_payment_again_replaces_the_link()
    {
        var mission = await SavedMission();
        var first = await UseCase().ExecuteAsync(mission.Id);

        var second = await UseCase().ExecuteAsync(mission.Id);

        Assert.NotEqual(first.PaymentLink!.Id, second.PaymentLink!.Id);
        Assert.Equal(second.PaymentLink.Id, (await _missions.GetAsync(mission.Id))!.PaymentLinkId);
    }

    [Fact]
    public async Task Paying_a_mission_that_is_not_Validated_is_rejected_without_a_link()
    {
        var mission = await SavedMission(validated: false);

        var result = await UseCase().ExecuteAsync(mission.Id);

        Assert.Equal(PayMissionOutcome.Rejected, result.Outcome);
        Assert.Equal("Only a Validated mission can be paid; this one is Requested.", result.RejectionReason);
        Assert.Null(result.PaymentLink);
        var saved = await _missions.GetAsync(mission.Id);
        Assert.Equal(MissionStatus.Requested, saved!.Status);
        Assert.Null(saved.PaymentLinkId);
    }

    [Fact]
    public async Task Paying_an_unknown_mission_returns_not_found()
    {
        var result = await UseCase().ExecuteAsync(Guid.NewGuid());

        Assert.Equal(PayMissionOutcome.NotFound, result.Outcome);
    }
}
