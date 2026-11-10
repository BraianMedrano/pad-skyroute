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
    string RecipientDni,
    // Optional: we POST the mission to this URL when it ends as Completed or Failed (#15).
    Uri? CallbackUrl = null);

public record MissionResponse(
    Guid Id,
    MissionStatus Status,
    Coordinates Origin,
    Coordinates Destination,
    double PackageWeightKg,
    Guid? AssignedDroneId,
    // Set only when Status is Failed.
    string? FailureReason = null);
