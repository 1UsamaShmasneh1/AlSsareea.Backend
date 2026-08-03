using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Carts.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Carts.Infrastructure.Persistence;

public sealed class CartsDbContext(DbContextOptions<CartsDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<CartItemOption> CartItemOptions => Set<CartItemOption>();
    public DbSet<CartIdempotencyRecord> IdempotencyRecords => Set<CartIdempotencyRecord>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) { modelBuilder.HasDefaultSchema(CartsPersistence.Schema); modelBuilder.ApplyConfigurationsFromAssembly(typeof(CartsDbContext).Assembly); }
}
internal static class CartsPersistence { internal const string Schema = "carts"; internal const string MigrationsHistoryTable = "__ef_migrations_history"; }

