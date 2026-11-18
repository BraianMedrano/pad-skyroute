using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkyRoute.Application.Drones;
using SkyRoute.Application.Missions;

namespace SkyRoute.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    private const string DefaultDatabasePath = "skyroute.db";

    // Reads the SQLite file path from "Database:Path" (env var Database__Path).
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var databasePath = configuration["Database:Path"] is { Length: > 0 } path ? path : DefaultDatabasePath;

        services.AddDbContext<SkyRouteDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
        services.AddScoped<IMissionRepository, EfMissionRepository>();
        services.AddScoped<IDroneRepository, EfDroneRepository>();
        return services;
    }

    // Creates the schema when the database does not exist (no migrations) and seeds the fleet once.
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SkyRouteDbContext>();

        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!await db.Drones.AnyAsync(cancellationToken))
        {
            db.Drones.AddRange(DroneSeed.Fleet());
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
