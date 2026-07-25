using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Maps.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Maps.Infrastructure.Persistence;

internal sealed class MapsDbContext(DbContextOptions<MapsDbContext> options)
    : DbContext(options), IUnitOfWork
{
    internal DbSet<ServiceArea> ServiceAreas => Set<ServiceArea>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfiguration(new ServiceAreaConfiguration());
    }
}
