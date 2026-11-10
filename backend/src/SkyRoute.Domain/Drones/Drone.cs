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

    public void MarkBusy()
    {
        if (Status != DroneStatus.Available)
        {
            throw new InvalidOperationException($"Drone {Name} is {Status}; only an Available drone can become Busy.");
        }
        Status = DroneStatus.Busy;
    }
}
