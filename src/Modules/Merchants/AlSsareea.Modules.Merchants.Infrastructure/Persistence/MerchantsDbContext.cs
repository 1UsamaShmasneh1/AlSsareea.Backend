using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Merchants.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Merchants.Infrastructure.Persistence;

public sealed class MerchantsDbContext(DbContextOptions<MerchantsDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<MerchantBranch> Branches => Set<MerchantBranch>();
    public DbSet<MerchantEmployee> Employees => Set<MerchantEmployee>();
    public DbSet<BusinessHour> BusinessHours => Set<BusinessHour>();
    public DbSet<BusinessHourPeriod> BusinessHourPeriods => Set<BusinessHourPeriod>();
    public DbSet<BranchScheduleOverride> ScheduleOverrides => Set<BranchScheduleOverride>();
    public DbSet<SpecialHourPeriod> SpecialHourPeriods => Set<SpecialHourPeriod>();
    public DbSet<BranchServiceArea> BranchServiceAreas => Set<BranchServiceArea>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.HasDefaultSchema(MerchantsPersistence.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MerchantsDbContext).Assembly);
    }
}

internal static class MerchantsPersistence
{
    internal const string Schema = "merchants";
    internal const string MigrationsHistoryTable = "__ef_migrations_history";
}
