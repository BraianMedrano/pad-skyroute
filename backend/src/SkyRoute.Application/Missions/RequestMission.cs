using SkyRoute.Application.Drones;
using SkyRoute.Application.External;
using SkyRoute.Domain;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Application.Missions;

public sealed record RequestMissionCommand(
    Coordinates Origin,
    Coordinates Destination,
    double PackageWeightKg,
    string RecipientDni);

// Either the validated mission, or the reason it was rejected.
public sealed record RequestMissionResult(Mission? Mission, string? RejectionReason)
{
    public static RequestMissionResult Accepted(Mission mission) => new(mission, null);

    public static RequestMissionResult Rejected(string reason) => new(null, reason);
}

// Requested → Validated. A rejected request is not saved.
public sealed class RequestMission(
    IMissionRepository missions,
    IDroneRepository drones,
    IIdentityService identity,
    IWeatherService weather)
{
    public async Task<RequestMissionResult> ExecuteAsync(RequestMissionCommand command, CancellationToken cancellationToken = default)
    {
        var route = new Route(command.Origin, command.Destination);
        var mission = Mission.RequestDelivery(route, command.PackageWeightKg, command.RecipientDni);

        if (!FlightRules.IsPackageWeightAllowed(mission.PackageWeightKg))
        {
            return RequestMissionResult.Rejected(
                $"Package weight must be greater than 0 and at most {FlightRules.MaxPackageWeightKg} kg.");
        }

        // A base is the home position of its drones.
        var bases = (await drones.ListAsync(cancellationToken)).Select(d => d.HomePosition).Distinct();
        if (!bases.Any(home => FlightRules.CanFly(home, route)))
        {
            return RequestMissionResult.Rejected(
                $"Out of range: no base can fly base → origin → destination → base within {FlightRules.MaxFlightKm} km.");
        }

        var recipient = await identity.ValidateRecipientAsync(mission.RecipientDni, cancellationToken);
        if (!recipient.IsValid)
        {
            return RequestMissionResult.Rejected(recipient.Reason ?? "Recipient rejected.");
        }

        foreach (var point in new[] { route.Origin, route.Destination })
        {
            var conditions = await weather.IsSafeToFlyAsync(point, cancellationToken);
            if (!conditions.IsSafe)
            {
                return RequestMissionResult.Rejected(conditions.Reason ?? "Unsafe weather.");
            }
        }

        mission.Validate();
        await missions.AddAsync(mission, cancellationToken);
        return RequestMissionResult.Accepted(mission);
    }
}
