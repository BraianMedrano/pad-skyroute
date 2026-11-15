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
    Coordinates Position,
    // The drone's base: where it starts and returns to (docs/architecture.md, "Flight rules").
    Coordinates HomePosition);
