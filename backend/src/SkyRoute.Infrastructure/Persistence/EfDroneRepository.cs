using Microsoft.EntityFrameworkCore;
using SkyRoute.Application.Drones;
using SkyRoute.Domain.Drones;

namespace SkyRoute.Infrastructure.Persistence;

public sealed class EfDroneRepository(SkyRouteDbContext db) : IDroneRepository
{
    public Task<Drone?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Drones.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Drone>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Drones.OrderBy(d => d.Name).ToListAsync(cancellationToken);

    public async Task UpdateAsync(Drone drone, CancellationToken cancellationToken = default)
    {
        db.Drones.Update(drone);
        await db.SaveChangesAsync(cancellationToken);
    }
}
