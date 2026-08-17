using AlSsareea.Modules.Delivery.Domain;
using AlSsareea.Modules.Delivery.Infrastructure.Persistence;
using AlSsareea.Modules.Tracking.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DeliveryAggregate = AlSsareea.Modules.Delivery.Domain.Delivery;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class DeliveryPersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task SchemaMigrationIndexesConstraintsAndIsolationAreCorrect()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        DeliveryDbContext db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
        await db.Database.OpenConnectionAsync();
        Assert.Equal(6L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.tables WHERE table_schema='delivery' AND table_name IN ('deliveries','delivery_status_history','delivery_proofs','delivery_operation_idempotency','delivery_audit','outbox_messages')"));
        Assert.Equal(1L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.tables WHERE table_schema='delivery' AND table_name='__ef_migrations_history'"));
        Assert.Equal(1L, await Scalar<long>(db, "SELECT count(*) FROM pg_indexes WHERE schemaname='delivery' AND indexname='ux_deliveries_order_id'"));
        Assert.True(await Scalar<long>(db, "SELECT count(*) FROM information_schema.check_constraints WHERE constraint_schema='delivery'") >= 10);
        Assert.Equal(0L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.referential_constraints r JOIN information_schema.table_constraints c ON c.constraint_name=r.constraint_name AND c.constraint_schema=r.constraint_schema WHERE c.constraint_schema='delivery' AND r.unique_constraint_schema <> 'delivery'"));
        Assert.Equal(0L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.referential_constraints WHERE constraint_schema='delivery' AND delete_rule='CASCADE'"));
        Assert.True(await Scalar<long>(db, "SELECT count(*) FROM information_schema.columns WHERE table_schema='delivery' AND data_type='timestamp with time zone'") >= 10);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task AggregateTimelineAndProofPersistTogether()
    {
        DateTime now = DateTime.UtcNow; DeliveryAggregate delivery = AtDropOff(Guid.NewGuid(), Guid.NewGuid(), ProofRequirement.Photo | ProofRequirement.RecipientName, now);
        delivery.AddMediaProof(DeliveryProofType.Photo, Guid.NewGuid(), now.AddMinutes(7)); delivery.AddRecipientName("Recipient", now.AddMinutes(8)); delivery.Complete(now.AddMinutes(9));
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); DeliveryDbContext db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>(); db.Deliveries.Add(delivery); await db.SaveChangesAsync();
        db.ChangeTracker.Clear(); DeliveryAggregate stored = await db.Deliveries.AsNoTracking().Include(x => x.StatusHistory).Include(x => x.Proofs).SingleAsync(x => x.Id == delivery.Id);
        Assert.Equal(DeliveryStatus.Delivered, stored.Status); Assert.Equal(8, stored.StatusHistory.Count); Assert.Equal(2, stored.Proofs.Count); Assert.All(stored.Proofs, proof => Assert.NotEqual(Guid.Empty, proof.DriverId));
    }

    [Fact]
    public async Task OrderUniquenessAndOptimisticConcurrencyAreEnforced()
    {
        Guid orderId = Guid.NewGuid(); DeliveryAggregate first = Create(orderId, Guid.NewGuid(), ProofRequirement.None, DateTime.UtcNow); DeliveryAggregate duplicate = Create(orderId, Guid.NewGuid(), ProofRequirement.None, DateTime.UtcNow);
        await using (AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope()) { DeliveryDbContext db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>(); db.Deliveries.Add(first); await db.SaveChangesAsync(); db.Deliveries.Add(duplicate); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); }
        await using AsyncServiceScope leftScope = fixture.ApiFactory.Services.CreateAsyncScope(); await using AsyncServiceScope rightScope = fixture.ApiFactory.Services.CreateAsyncScope(); DeliveryDbContext left = leftScope.ServiceProvider.GetRequiredService<DeliveryDbContext>(); DeliveryDbContext right = rightScope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
        DeliveryAggregate leftValue = await left.Deliveries.Include(x => x.StatusHistory).SingleAsync(x => x.Id == first.Id); DeliveryAggregate rightValue = await right.Deliveries.Include(x => x.StatusHistory).SingleAsync(x => x.Id == first.Id);
        leftValue.Assign(Guid.NewGuid(), DateTime.UtcNow); rightValue.Assign(Guid.NewGuid(), DateTime.UtcNow); await left.SaveChangesAsync(); await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => right.SaveChangesAsync());
    }

    [Fact]
    public async Task TrackingVisibilityRequiresOwnerDriverAndAllowedStateAndClosesAfterCompletion()
    {
        Guid orderId = Guid.NewGuid(); Guid customerUserId = Guid.NewGuid(); DateTime now = DateTime.UtcNow; DeliveryAggregate delivery = Create(orderId, customerUserId, ProofRequirement.None, now);
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); DeliveryDbContext db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>(); ITrackingVisibilityProvider visibility = scope.ServiceProvider.GetRequiredService<ITrackingVisibilityProvider>(); db.Deliveries.Add(delivery); await db.SaveChangesAsync();
        Assert.Null(await visibility.ResolveOrderAsync(orderId, customerUserId));
        delivery.Assign(Guid.NewGuid(), now.AddMinutes(1)); delivery.BeginHeadingToPickup(now.AddMinutes(2)); delivery.ArriveAtPickup(now.AddMinutes(3)); delivery.ConfirmPickup(now.AddMinutes(4)); await db.SaveChangesAsync();
        Assert.Null(await visibility.ResolveOrderAsync(orderId, Guid.NewGuid())); TrackingVisibility? allowed = await visibility.ResolveOrderAsync(orderId, customerUserId); Assert.NotNull(allowed); Assert.Equal(delivery.DriverId, allowed.DriverId);
        delivery.Start(now.AddMinutes(5)); delivery.ArriveAtDropOff(now.AddMinutes(6)); delivery.Complete(now.AddMinutes(7)); await db.SaveChangesAsync(); Assert.Null(await visibility.ResolveOrderAsync(orderId, customerUserId));
    }

    private static DeliveryAggregate AtDropOff(Guid orderId, Guid customerUserId, ProofRequirement requirements, DateTime now)
    {
        DeliveryAggregate delivery = Create(orderId, customerUserId, requirements, now); delivery.Assign(Guid.NewGuid(), now.AddMinutes(1)); delivery.BeginHeadingToPickup(now.AddMinutes(2)); delivery.ArriveAtPickup(now.AddMinutes(3)); delivery.ConfirmPickup(now.AddMinutes(4)); delivery.Start(now.AddMinutes(5)); delivery.ArriveAtDropOff(now.AddMinutes(6)); return delivery;
    }
    private static DeliveryAggregate Create(Guid orderId, Guid customerUserId, ProofRequirement requirements, DateTime now) => DeliveryAggregate.Create(DeliveryId.New(), orderId, Guid.NewGuid(), customerUserId, new PickupSnapshot(Guid.NewGuid(), Guid.NewGuid(), "Pickup", "Merchant", null, null, 31.7, 35.2), new DropOffSnapshot(Guid.NewGuid(), "Drop-off", "Recipient", null, null, null, 31.8, 35.3), requirements, null, null, now);
    private static async Task<T> Scalar<T>(DeliveryDbContext db, string sql)
    {
        if (db.Database.GetDbConnection().State != System.Data.ConnectionState.Open) await db.Database.OpenConnectionAsync(); await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; object? value = await command.ExecuteScalarAsync(); return (T)Convert.ChangeType(value!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }
}
