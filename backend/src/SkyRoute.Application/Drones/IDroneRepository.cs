using SkyRoute.Domain.Drones;

namespace SkyRoute.Application.Drones;

public interface IDroneRepository
{
    Task<Drone?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Drone>> ListAsync(CancellationToken cancellationToken = default);

    // Saves the changes made to a drone obtained from this repository.
    Task UpdateAsync(Drone drone, CancellationToken cancellationToken = default);
}
