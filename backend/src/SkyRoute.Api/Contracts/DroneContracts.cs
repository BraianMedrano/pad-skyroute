namespace SkyRoute.Api.Contracts;

public enum DroneStatus
{
    Available,
    Busy,
}

public record DroneResponse(
    Guid Id,
    string Name,
    DroneStatus Status,
    Coordinates Position);
