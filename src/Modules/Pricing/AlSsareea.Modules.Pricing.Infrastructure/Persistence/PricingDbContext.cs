using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Pricing.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Pricing.Infrastructure.Persistence;

public sealed class PricingDbContext(DbContextOptions<PricingDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<PricingPolicy> Policies => Set<PricingPolicy>();
    public DbSet<PricingRule> Rules => Set<PricingRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(PricingPersistence.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PricingDbContext).Assembly);
    }
}

internal static class PricingPersistence
{
    internal const string Schema = "pricing";
    internal const string MigrationsHistoryTable = "__ef_migrations_history";
}
