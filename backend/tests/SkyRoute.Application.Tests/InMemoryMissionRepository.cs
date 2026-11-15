using SkyRoute.Application.Missions;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Application.Tests;

// In-memory stand-in for the EF Core repository, for use case tests.
public sealed class InMemoryMissionRepository : IMissionRepository
{
    private readonly Dictionary<Guid, Mission> _missions = [];

    public Task AddAsync(Mission mission, CancellationToken cancellationToken = default)
    {
        _missions.Add(mission.Id, mission);
        return Task.CompletedTask;
    }

    public Task<Mission?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_missions.GetValueOrDefault(id));

    public Task<IReadOnlyList<Mission>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Mission>>([.. _missions.Values]);

    public Task UpdateAsync(Mission mission, CancellationToken cancellationToken = default)
    {
        _missions[mission.Id] = mission;
        return Task.CompletedTask;
    }
}
