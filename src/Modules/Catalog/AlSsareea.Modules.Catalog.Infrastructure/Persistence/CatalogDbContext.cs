using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Catalog.Domain.Catalog> Catalogs => Set<Catalog.Domain.Catalog>(); public DbSet<Category> Categories => Set<Category>(); public DbSet<MenuSection> MenuSections => Set<MenuSection>(); public DbSet<Product> Products => Set<Product>(); public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>(); public DbSet<OptionGroup> OptionGroups => Set<OptionGroup>(); public DbSet<ProductOption> ProductOptions => Set<ProductOption>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) { modelBuilder.HasDefaultSchema(CatalogPersistence.Schema); modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly); }
}
internal static class CatalogPersistence { internal const string Schema = "catalog"; internal const string MigrationsHistoryTable = "__ef_migrations_history"; }
