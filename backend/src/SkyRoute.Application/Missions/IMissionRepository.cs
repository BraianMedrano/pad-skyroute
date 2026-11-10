using SkyRoute.Domain.Missions;

namespace SkyRoute.Application.Missions;

public interface IMissionRepository
{
    Task AddAsync(Mission mission, CancellationToken cancellationToken = default);

    Task<Mission?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Mission>> ListAsync(CancellationToken cancellationToken = default);

    // Saves the changes made to a mission obtained from this repository.
    Task UpdateAsync(Mission mission, CancellationToken cancellationToken = default);
}
