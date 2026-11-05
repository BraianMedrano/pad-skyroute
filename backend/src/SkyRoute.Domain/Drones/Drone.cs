namespace SkyRoute.Domain.Drones;

public enum DroneStatus
{
    Available,
    Busy,
}

public sealed class Drone(Guid id, string name, Coordinates homePosition)
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public DroneStatus Status { get; private set; } = DroneStatus.Available;
    public Coordinates HomePosition { get; } = homePosition;
}
