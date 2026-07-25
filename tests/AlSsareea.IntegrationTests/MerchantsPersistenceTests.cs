using System.Data.Common;
using AlSsareea.Modules.Merchants.Domain;
using AlSsareea.Modules.Merchants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class MerchantsPersistenceTests(PostgresFixture fixture)
{
    private static readonly DateTime Now = new(2026, 7, 25, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task MigrationCreatesSchemaTablesPartialIndexesAndSpatialIndex()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        MerchantsDbContext db = scope.ServiceProvider.GetRequiredService<MerchantsDbContext>();
        await db.Database.OpenConnectionAsync();
        DbConnection connection = db.Database.GetDbConnection();
        Assert.Equal(8, await Scalar<long>(connection, "SELECT count(*) FROM information_schema.tables WHERE table_schema='merchants' AND table_name LIKE 'merchant%'"));
        Assert.Equal(1, await Scalar<long>(connection, "SELECT count(*) FROM pg_indexes WHERE schemaname='merchants' AND indexname='ux_merchant_branches_primary_per_merchant' AND indexdef ILIKE '%WHERE%is_primary%'"));
        Assert.Equal(1, await Scalar<long>(connection, "SELECT count(*) FROM pg_indexes WHERE schemaname='merchants' AND indexname='ix_merchant_branches_location_gist' AND indexdef ILIKE '%gist%'"));
        Assert.Equal(0, await Scalar<long>(connection, "SELECT count(*) FROM information_schema.table_constraints tc JOIN information_schema.constraint_column_usage ccu ON ccu.constraint_name=tc.constraint_name AND ccu.constraint_schema=tc.constraint_schema WHERE tc.constraint_schema='merchants' AND tc.constraint_type='FOREIGN KEY' AND ccu.table_schema IN ('identity','maps')"));
    }

    [Fact]
    public async Task AggregateRoundTripPersistsPointSchedulesOverridesEmployeesAndServiceAreas()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        MerchantsDbContext db = scope.ServiceProvider.GetRequiredService<MerchantsDbContext>();
        Guid ownerUserId = Guid.NewGuid();
        Merchant merchant = Merchant.Create(MerchantId.New(), "Legal", "Display", null, null, null, "owner@example.com", "+970599000000", ownerUserId, Now);
        merchant.Activate(Now.AddMinutes(1));
        MerchantBranch branch = CreateBranch(merchant.Id, true);
        branch.ReplaceBusinessHours(Enum.GetValues<DayOfWeek>().Select(day => (day, day != DayOfWeek.Saturday, day == DayOfWeek.Saturday ? (IEnumerable<OpeningPeriod>)[new(new TimeOnly(10, 0), new TimeOnly(14, 0))] : [])), Now);
        branch.AddClosure(new DateOnly(2026, 7, 26), new DateOnly(2026, 7, 27), "holiday", Now);
        Guid areaId = Guid.NewGuid(); branch.AssignServiceArea(areaId, Now);
        MerchantEmployee owner = MerchantEmployee.Create(MerchantEmployeeId.New(), merchant.Id, ownerUserId, null, MerchantMembershipRole.Owner, false, Now);
        db.AddRange(merchant, branch, owner); await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        MerchantBranch loaded = await db.Branches.Include(x => x.BusinessHours).ThenInclude(x => x.Periods).Include(x => x.ScheduleOverrides).ThenInclude(x => x.Periods).Include(x => x.ServiceAreas).SingleAsync(x => x.Id == branch.Id);
        Assert.Equal(31.9, loaded.Location.Latitude);
        Assert.Equal(7, loaded.BusinessHours.Count);
        Assert.Single(loaded.ScheduleOverrides);
        Assert.Equal(areaId, Assert.Single(loaded.ServiceAreas).ServiceAreaId);
        Assert.Single(await db.Employees.Where(x => x.MerchantId == merchant.Id).ToListAsync());
        await db.Database.OpenConnectionAsync();
        Assert.Equal(4326, await Scalar<int>(db.Database.GetDbConnection(), $"SELECT ST_SRID(location) FROM merchants.merchant_branches WHERE id='{branch.Id.Value}'"));
    }

    [Fact]
    public async Task DatabasePreventsTwoPrimaryBranches()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        MerchantsDbContext db = scope.ServiceProvider.GetRequiredService<MerchantsDbContext>();
        Merchant merchant = Merchant.Create(MerchantId.New(), "Legal", "Display", null, null, null, "owner@example.com", "+970599000000", Guid.NewGuid(), Now);
        db.Add(merchant);
        db.Add(CreateBranch(merchant.Id, true));
        db.Add(CreateBranch(merchant.Id, true));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task MerchantConcurrencyStampProducesConflict()
    {
        MerchantId id;
        await using (AsyncServiceScope setup = fixture.ApiFactory.Services.CreateAsyncScope())
        {
            MerchantsDbContext db = setup.ServiceProvider.GetRequiredService<MerchantsDbContext>();
            Merchant merchant = Merchant.Create(MerchantId.New(), "Legal", "Display", null, null, null, "owner@example.com", "+970599000000", Guid.NewGuid(), Now);
            id = merchant.Id; db.Add(merchant); await db.SaveChangesAsync();
        }
        await using AsyncServiceScope firstScope = fixture.ApiFactory.Services.CreateAsyncScope();
        await using AsyncServiceScope secondScope = fixture.ApiFactory.Services.CreateAsyncScope();
        MerchantsDbContext first = firstScope.ServiceProvider.GetRequiredService<MerchantsDbContext>();
        MerchantsDbContext second = secondScope.ServiceProvider.GetRequiredService<MerchantsDbContext>();
        Merchant one = await first.Merchants.SingleAsync(x => x.Id == id);
        Merchant two = await second.Merchants.SingleAsync(x => x.Id == id);
        one.UpdateProfile("Legal 1", "One", null, null, null, "one@example.com", "+970599000001", Now.AddMinutes(1)); await first.SaveChangesAsync();
        two.UpdateProfile("Legal 2", "Two", null, null, null, "two@example.com", "+970599000002", Now.AddMinutes(2));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task MerchantEndpointRequiresAuthentication()
    {
        HttpClient client = fixture.ApiFactory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/api/v1/merchants");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static MerchantBranch CreateBranch(MerchantId merchantId, bool primary) =>
        MerchantBranch.Create(MerchantBranchId.New(), merchantId, "Central", null, "+970599000000", null,
            BranchAddress.Create("Ramallah", null, "Main", "1", null), new GeoCoordinate(31.9, 35.2), "Asia/Jerusalem", primary, Now);

    private static async Task<T> Scalar<T>(DbConnection connection, string sql)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object? result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }
}
