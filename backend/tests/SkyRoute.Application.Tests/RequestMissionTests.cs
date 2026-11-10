using SkyRoute.Application.Missions;
using SkyRoute.Domain;
using SkyRoute.Domain.Drones;
using SkyRoute.Domain.Missions;
using SkyRoute.Infrastructure.External;

namespace SkyRoute.Application.Tests;

public class RequestMissionTests
{
    private static readonly Coordinates Centro = new(-26.8305, -65.2039);
    private static readonly Coordinates Parque9DeJulio = new(-26.8317, -65.1912);
    private static readonly Coordinates Famailla = new(-27.0540, -65.4030);
    private const string RejectedDni = "00000000";

    private readonly InMemoryMissionRepository _missions = new();

    private RequestMission UseCase(bool weatherIsSafe = true) => new(
        _missions,
        new InMemoryDroneRepository(new Drone(Guid.NewGuid(), "Falcon-1", Centro)),
        new FakeIdentityService(rejectedDni: RejectedDni, rejectionReason: "Recipient DNI rejected"),
        new FakeWeatherService(isSafe: weatherIsSafe, unsafeReason: "High wind at origin"));

    private static RequestMissionCommand Command(
        Coordinates? destination = null, double weightKg = 1.5, string dni = "30111222") =>
        new(Centro, destination ?? Parque9DeJulio, weightKg, dni);

    [Fact]
    public async Task A_valid_request_leaves_the_mission_Validated_and_saved()
    {
        var result = await UseCase().ExecuteAsync(Command());

        Assert.Null(result.RejectionReason);
        Assert.NotNull(result.Mission);
        Assert.Equal(MissionStatus.Validated, result.Mission.Status);
        var saved = await _missions.GetAsync(result.Mission.Id);
        Assert.NotNull(saved);
        Assert.Equal(MissionStatus.Validated, saved.Status);
        Assert.Equal(new Route(Centro, Parque9DeJulio), saved.Route);
        Assert.Equal(1.5, saved.PackageWeightKg);
        Assert.Equal("30111222", saved.RecipientDni);
    }

    [Fact]
    public async Task A_rejected_recipient_returns_the_reason_and_saves_nothing()
    {
        var result = await UseCase().ExecuteAsync(Command(dni: RejectedDni));

        Assert.Null(result.Mission);
        Assert.Equal("Recipient DNI rejected", result.RejectionReason);
        Assert.Empty(await _missions.ListAsync());
    }

    [Fact]
    public async Task Unsafe_weather_returns_the_reason_and_saves_nothing()
    {
        var result = await UseCase(weatherIsSafe: false).ExecuteAsync(Command());

        Assert.Null(result.Mission);
        Assert.Equal("High wind at origin", result.RejectionReason);
        Assert.Empty(await _missions.ListAsync());
    }

    [Fact]
    public async Task A_package_over_2_5_kg_returns_the_reason_and_saves_nothing()
    {
        var result = await UseCase().ExecuteAsync(Command(weightKg: 2.6));

        Assert.Null(result.Mission);
        Assert.Equal("Package weight must be greater than 0 and at most 2.5 kg.", result.RejectionReason);
        Assert.Empty(await _missions.ListAsync());
    }

    [Fact]
    public async Task A_trip_no_base_can_fly_within_25_km_returns_the_reason_and_saves_nothing()
    {
        var result = await UseCase().ExecuteAsync(Command(destination: Famailla));

        Assert.Null(result.Mission);
        Assert.Equal("Out of range: no base can fly base → origin → destination → base within 25 km.", result.RejectionReason);
        Assert.Empty(await _missions.ListAsync());
    }
}
