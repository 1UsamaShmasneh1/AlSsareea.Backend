using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Tracking.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Tracking.Infrastructure.Persistence;

public sealed class TrackingDbContext(DbContextOptions<TrackingDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<DriverLocation> DriverLocations => Set<DriverLocation>();
    public DbSet<DriverLatestLocation> DriverLatestLocations => Set<DriverLatestLocation>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) { modelBuilder.HasDefaultSchema(TrackingPersistence.Schema); modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrackingDbContext).Assembly); }
}

public static class TrackingPersistence
{
    public const string Schema = "tracking";
    public const string MigrationsHistoryTable = "__ef_migrations_history";
    public const string ConnectionStringName = "TrackingDatabase";
}
