using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Promotions.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Promotions.Infrastructure.Persistence;

public sealed class PromotionsDbContext(DbContextOptions<PromotionsDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<PromotionRedemption> Redemptions => Set<PromotionRedemption>();
    public DbSet<PromotionAudit> Audits => Set<PromotionAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(PromotionsPersistence.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PromotionsDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (ChangeTracker.Entries().Any(entry =>
            entry.Entity is PromotionAudit or PromotionRedemption &&
            entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Promotion audit and redemption records are append-only.");
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}

internal static class PromotionsPersistence
{
    internal const string Schema = "promotions";
    internal const string MigrationsHistoryTable = "__ef_migrations_history";
}
