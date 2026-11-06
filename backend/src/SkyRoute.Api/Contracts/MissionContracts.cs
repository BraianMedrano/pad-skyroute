namespace SkyRoute.Api.Contracts;

public record Coordinates(double Lat, double Lon);

public enum MissionStatus
{
    Requested,
    Validated,
    Paid,
    Assigned,
    InProgress,
    Completed,
    Failed,
}

public record RequestMissionRequest(
    Coordinates Origin,
    Coordinates Destination,
    double PackageWeightKg,
    string RecipientDni);

public record MissionResponse(
    Guid Id,
    MissionStatus Status,
    Coordinates Origin,
    Coordinates Destination,
    double PackageWeightKg,
    Guid? AssignedDroneId);
