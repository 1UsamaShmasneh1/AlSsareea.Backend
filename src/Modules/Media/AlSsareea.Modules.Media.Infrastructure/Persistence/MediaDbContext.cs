using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Media.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Media.Infrastructure.Persistence;

public sealed class MediaDbContext(DbContextOptions<MediaDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<MediaAsset> Assets => Set<MediaAsset>();
    public DbSet<MediaVariant> Variants => Set<MediaVariant>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) { modelBuilder.HasDefaultSchema(MediaPersistence.Schema); modelBuilder.ApplyConfigurationsFromAssembly(typeof(MediaDbContext).Assembly); }
}
internal static class MediaPersistence { internal const string Schema = "media"; internal const string MigrationsHistoryTable = "__ef_migrations_history"; }
