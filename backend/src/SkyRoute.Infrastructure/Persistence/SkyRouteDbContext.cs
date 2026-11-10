using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SkyRoute.Domain;
using SkyRoute.Domain.Drones;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Infrastructure.Persistence;

public sealed class SkyRouteDbContext(DbContextOptions<SkyRouteDbContext> options) : DbContext(options)
{
    public DbSet<Mission> Missions => Set<Mission>();

    public DbSet<Drone> Drones => Set<Drone>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Mission>(mission =>
        {
            mission.ToTable("missions");
            mission.HasKey(m => m.Id);
            mission.Property(m => m.Id).ValueGeneratedNever();
            mission.Property(m => m.Type).HasConversion<string>();
            mission.Property(m => m.Status).HasConversion<string>();
            mission.Property(m => m.PackageWeightKg);
            mission.Property(m => m.RecipientDni);
            mission.Property(m => m.AssignedDroneId);
            mission.Property(m => m.FailureReason);
            mission.Property(m => m.CallbackUrl);
            mission.Property(m => m.RequestedAt);
            mission.Property(m => m.Route).HasConversion(
                route => $"{Format(route.Origin)};{Format(route.Destination)}",
                text => ParseRoute(text));
        });

        modelBuilder.Entity<Drone>(drone =>
        {
            drone.ToTable("drones");
            drone.HasKey(d => d.Id);
            drone.Property(d => d.Id).ValueGeneratedNever();
            drone.Property(d => d.Name);
            drone.Property(d => d.Status).HasConversion<string>();
            drone.Property(d => d.HomePosition).HasConversion(
                position => Format(position),
                text => ParseCoordinates(text));
        });
    }

    // Coordinates and Route are stored as text ("lat,lon" and "lat,lon;lat,lon") so EF Core can
    // build the domain types through their existing constructors, without changing the Domain.
    private static string Format(Coordinates coordinates) =>
        string.Create(CultureInfo.InvariantCulture, $"{coordinates.Lat:R},{coordinates.Lon:R}");

    private static Coordinates ParseCoordinates(string text)
    {
        var parts = text.Split(',');
        return new Coordinates(
            double.Parse(parts[0], CultureInfo.InvariantCulture),
            double.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    private static Route ParseRoute(string text)
    {
        var points = text.Split(';');
        return new Route(ParseCoordinates(points[0]), ParseCoordinates(points[1]));
    }
}
