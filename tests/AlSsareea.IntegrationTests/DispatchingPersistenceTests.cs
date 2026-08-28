using AlSsareea.Modules.Dispatching.Domain;
using AlSsareea.Modules.Dispatching.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class DispatchingPersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task SchemaMigrationConstraintsIndexesAndIsolationAreCorrect()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); DispatchingDbContext db = scope.ServiceProvider.GetRequiredService<DispatchingDbContext>(); await db.Database.OpenConnectionAsync();
        Assert.Equal(7L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.tables WHERE table_schema='dispatching' AND table_name IN ('dispatch_requests','dispatch_candidates','dispatch_offers','dispatch_history','dispatch_idempotency_records','dispatch_audit','dispatch_outbox_messages')")); Assert.Equal(1L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.tables WHERE table_schema='dispatching' AND table_name='__ef_migrations_history'")); Assert.Equal(1L, await Scalar<long>(db, "SELECT count(*) FROM pg_indexes WHERE schemaname='dispatching' AND indexname='ux_dispatch_requests_delivery_id'")); Assert.True(await Scalar<long>(db, "SELECT count(*) FROM information_schema.check_constraints WHERE constraint_schema='dispatching'") >= 10); Assert.Equal(0L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.referential_constraints r JOIN information_schema.table_constraints c ON c.constraint_name=r.constraint_name AND c.constraint_schema=r.constraint_schema WHERE c.constraint_schema='dispatching' AND r.unique_constraint_schema <> 'dispatching'")); Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task ConcurrentAcceptsProduceExactlyOneWinner()
    {
        DateTime now = DateTime.UtcNow; DispatchRequest request = Create(now); Guid first = Guid.NewGuid(), second = Guid.NewGuid(); request.StartAttempt([Candidate(request, first, 90, 1, now), Candidate(request, second, 80, 2, now)], 3, now); Guid offer = request.Offers.Single().Id.Value;
        await using (AsyncServiceScope seed = fixture.ApiFactory.Services.CreateAsyncScope()) { DispatchingDbContext db = seed.ServiceProvider.GetRequiredService<DispatchingDbContext>(); db.DispatchRequests.Add(request); await db.SaveChangesAsync(); }
        await using AsyncServiceScope leftScope = fixture.ApiFactory.Services.CreateAsyncScope(); await using AsyncServiceScope rightScope = fixture.ApiFactory.Services.CreateAsyncScope(); DispatchingDbContext left = leftScope.ServiceProvider.GetRequiredService<DispatchingDbContext>(); DispatchingDbContext right = rightScope.ServiceProvider.GetRequiredService<DispatchingDbContext>(); DispatchRequest leftValue = await Aggregate(left).SingleAsync(x => x.Id == request.Id); DispatchRequest rightValue = await Aggregate(right).SingleAsync(x => x.Id == request.Id); leftValue.Accept(offer, first, now.AddSeconds(1));
        Task leftSave = left.SaveChangesAsync(); Task rightSave = Task.Run(async () => { await leftSave; rightValue.ManualAssign(second, Guid.NewGuid(), "emergency", now.AddSeconds(2)); await right.SaveChangesAsync(); }); await leftSave; await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => rightSave);
        await using AsyncServiceScope verify = fixture.ApiFactory.Services.CreateAsyncScope(); DispatchRequest stored = await Aggregate(verify.ServiceProvider.GetRequiredService<DispatchingDbContext>()).SingleAsync(x => x.Id == request.Id); Assert.Equal(first, stored.AssignedDriverId); Assert.Single(stored.Offers, x => x.Status == DispatchOfferStatus.Accepted);
    }
    private static IQueryable<DispatchRequest> Aggregate(DispatchingDbContext db) => db.DispatchRequests.Include(x => x.Candidates).Include(x => x.Offers).Include(x => x.History).AsSplitQuery();
    private static DispatchRequest Create(DateTime now) => DispatchRequest.Create(DispatchRequestId.New(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 31.7, 35.2, null, null, now);
    private static DispatchCandidate Candidate(DispatchRequest request, Guid driver, decimal score, int rank, DateTime now) => DispatchCandidate.Create(request.Id, driver, 1, 1000, 300, 0, 2, null, score, rank, "test", now);
    private static async Task<T> Scalar<T>(DispatchingDbContext db, string sql) { if (db.Database.GetDbConnection().State != System.Data.ConnectionState.Open) await db.Database.OpenConnectionAsync(); await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; object? value = await command.ExecuteScalarAsync(); return (T)Convert.ChangeType(value!, typeof(T), System.Globalization.CultureInfo.InvariantCulture); }
}
