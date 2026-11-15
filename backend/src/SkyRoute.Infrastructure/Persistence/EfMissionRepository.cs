using Microsoft.EntityFrameworkCore;
using SkyRoute.Application.Missions;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Infrastructure.Persistence;

public sealed class EfMissionRepository(SkyRouteDbContext db) : IMissionRepository
{
    public async Task AddAsync(Mission mission, CancellationToken cancellationToken = default)
    {
        db.Missions.Add(mission);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<Mission?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Missions.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Mission>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Missions.ToListAsync(cancellationToken);

    public async Task UpdateAsync(Mission mission, CancellationToken cancellationToken = default)
    {
        db.Missions.Update(mission);
        await db.SaveChangesAsync(cancellationToken);
    }
}
