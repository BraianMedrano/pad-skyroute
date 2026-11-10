using SkyRoute.Domain;
using SkyRoute.Domain.Drones;

namespace SkyRoute.Domain.Tests;

public class DroneTests
{
    private static Drone NewDrone() => new(Guid.NewGuid(), "Falcon-1", new Coordinates(-26.8305, -65.2039));

    [Fact]
    public void An_available_drone_becomes_busy()
    {
        var drone = NewDrone();

        drone.MarkBusy();

        Assert.Equal(DroneStatus.Busy, drone.Status);
    }

    [Fact]
    public void A_busy_drone_cannot_be_marked_busy_again()
    {
        var drone = NewDrone();
        drone.MarkBusy();

        Assert.Throws<InvalidOperationException>(drone.MarkBusy);
        Assert.Equal(DroneStatus.Busy, drone.Status);
    }

    [Fact]
    public void A_busy_drone_becomes_available_again()
    {
        var drone = NewDrone();
        drone.MarkBusy();

        drone.MarkAvailable();

        Assert.Equal(DroneStatus.Available, drone.Status);
    }

    [Fact]
    public void An_available_drone_cannot_be_marked_available_again()
    {
        var drone = NewDrone();

        Assert.Throws<InvalidOperationException>(drone.MarkAvailable);
        Assert.Equal(DroneStatus.Available, drone.Status);
    }
}
