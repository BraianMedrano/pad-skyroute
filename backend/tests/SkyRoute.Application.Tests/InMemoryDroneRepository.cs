using SkyRoute.Application.Drones;
using SkyRoute.Domain.Drones;

namespace SkyRoute.Application.Tests;

// In-memory stand-in for the EF Core repository, for use case tests.
public sealed class InMemoryDroneRepository(params Drone[] drones) : IDroneRepository
{
    private readonly Dictionary<Guid, Drone> _drones = drones.ToDictionary(d => d.Id);

    public Task<Drone?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_drones.GetValueOrDefault(id));

    public Task<IReadOnlyList<Drone>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Drone>>([.. _drones.Values.OrderBy(d => d.Name)]);

    public Task UpdateAsync(Drone drone, CancellationToken cancellationToken = default)
    {
        _drones[drone.Id] = drone;
        return Task.CompletedTask;
    }
}
