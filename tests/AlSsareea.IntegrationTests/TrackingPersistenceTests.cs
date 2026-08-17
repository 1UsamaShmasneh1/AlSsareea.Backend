using AlSsareea.Modules.Tracking.Application;
using AlSsareea.Modules.Tracking.Domain;
using AlSsareea.Modules.Tracking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class TrackingPersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task SchemaMigrationSpatialColumnsIndexesAndIsolationAreCorrect()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); TrackingDbContext db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>(); await db.Database.OpenConnectionAsync();
        Assert.Equal(2L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.tables WHERE table_schema='tracking' AND table_name IN ('driver_locations','driver_latest_locations')"));
        Assert.Equal(0L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_name LIKE '%location%'"));
        Assert.Equal(1L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.tables WHERE table_schema='tracking' AND table_name='__ef_migrations_history'"));
        Assert.Equal(2L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.columns WHERE table_schema='tracking' AND column_name='position' AND udt_name='geometry'"));
        Assert.True(await Scalar<long>(db, "SELECT count(*) FROM pg_indexes WHERE schemaname='tracking' AND indexdef ILIKE '%USING gist%'") >= 2);
        Assert.Equal(0L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.table_constraints WHERE table_schema='tracking' AND constraint_type='FOREIGN KEY'"));
        Assert.Equal(0L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.referential_constraints WHERE constraint_schema='tracking' AND delete_rule='CASCADE'"));
        Assert.Equal(1L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.table_constraints WHERE table_schema='tracking' AND table_name='driver_latest_locations' AND constraint_type='PRIMARY KEY'"));
        Assert.Equal(8L, await Scalar<long>(db, "SELECT count(*) FROM information_schema.check_constraints WHERE constraint_schema='tracking' AND constraint_name IN ('ck_driver_locations_accuracy','ck_driver_locations_speed','ck_driver_locations_heading','ck_driver_locations_sequence','ck_driver_latest_locations_accuracy','ck_driver_latest_locations_speed','ck_driver_latest_locations_heading','ck_driver_latest_locations_sequence')"));
        Assert.True(await Scalar<long>(db, "SELECT count(*) FROM information_schema.columns WHERE table_schema='tracking' AND data_type='timestamp with time zone'") >= 5);
    }

    [Fact]
    public async Task HistoryLatestDuplicateAndOutOfOrderSemanticsAreStable()
    {
        Guid driverId = Guid.NewGuid(); DateTime now = DateTime.UtcNow;
        StoreLocationResult first = await Store(driverId, 10, now, true); StoreLocationResult duplicate = await Store(driverId, 10, now, true); StoreLocationResult older = await Store(driverId, 9, now.AddSeconds(-1), true); StoreLocationResult newer = await Store(driverId, 11, now.AddSeconds(1), true);
        Assert.True(first.LatestUpdated); Assert.True(duplicate.Duplicate); Assert.Equal(first.LocationId, duplicate.LocationId); Assert.False(older.LatestUpdated); Assert.True(newer.LatestUpdated);
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); TrackingDbContext db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>(); DriverLatestLocation latest = await db.DriverLatestLocations.AsNoTracking().SingleAsync(x => x.DriverId == driverId); Assert.Equal(11, latest.LastSequenceNumber); Assert.Equal(3, await db.DriverLocations.CountAsync(x => x.DriverId == driverId));
    }

    [Fact]
    public async Task ConcurrentOutOfOrderWritesFinishWithNewestSequence()
    {
        Guid driverId = Guid.NewGuid(); DateTime now = DateTime.UtcNow;
        await Task.WhenAll(Store(driverId, 101, now, true), Store(driverId, 102, now.AddSeconds(1), true));
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); TrackingDbContext db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>(); Assert.Equal(102, (await db.DriverLatestLocations.AsNoTracking().SingleAsync(x => x.DriverId == driverId)).LastSequenceNumber);
    }

    [Fact]
    public async Task PostgisPointRoundTripsWithSrid4326AndHistoryIsPaged()
    {
        Guid driverId = Guid.NewGuid(); DateTime now = DateTime.UtcNow; await Store(driverId, 1, now, true); await Store(driverId, 2, now.AddSeconds(1), true); await Store(driverId, 3, now.AddHours(2), true);
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); ITrackingStore store = scope.ServiceProvider.GetRequiredService<ITrackingStore>(); var page = await store.GetHistoryAsync(driverId, now.AddMinutes(-1), now.AddMinutes(1), 1, 1, default); Assert.Single(page.Items); Assert.Equal(2, page.TotalCount);
        TrackingDbContext db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>(); Assert.Equal(4326, await Scalar<int>(db, $"SELECT ST_SRID(position) FROM tracking.driver_latest_locations WHERE driver_id='{driverId}'"));
        Assert.True(await Scalar<bool>(db, $"SELECT ST_DWithin(position, ST_SetSRID(ST_MakePoint(35.2137, 31.7683), 4326), 0.01) FROM tracking.driver_locations WHERE driver_id='{driverId}' ORDER BY sequence_number LIMIT 1"));
    }

    private async Task<StoreLocationResult> Store(Guid driverId, long sequence, DateTime recorded, bool promote)
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); ITrackingStore store = scope.ServiceProvider.GetRequiredService<ITrackingStore>(); DriverLocation location = DriverLocation.Create(DriverLocationId.New(), driverId, LocationPosition.Create(31.7683 + sequence / 1_000_000d, 35.2137), recorded, DateTime.UtcNow, 5, null, null, null, sequence, LocationSource.Live); return await store.StoreAsync(location, promote, default);
    }
    private static async Task<T> Scalar<T>(TrackingDbContext db, string sql)
    {
        if (db.Database.GetDbConnection().State != System.Data.ConnectionState.Open) await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; object? value = await command.ExecuteScalarAsync(); return (T)Convert.ChangeType(value!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }
}
