using System.Net;
using AlSsareea.Modules.Promotions.Domain;
using AlSsareea.Modules.Promotions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class PromotionsPersistenceTests(PostgresFixture fixture)
{
    private static readonly DateTime Now = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid MerchantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task MigrationCreatesOwnedSchemaTablesIndexesAndHasNoPendingChanges()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        PromotionsDbContext db = scope.ServiceProvider.GetRequiredService<PromotionsDbContext>();
        Assert.False(db.Database.HasPendingModelChanges());
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        Assert.Equal(3, await Scalar<long>(connection, "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'promotions' AND table_name IN ('promotions','promotion_redemptions','promotion_audit')"));
        Assert.Equal(1, await Scalar<long>(connection, "SELECT count(*) FROM pg_indexes WHERE schemaname = 'promotions' AND indexname = 'ux_promotions_normalized_coupon_code'"));
        Assert.Equal(1, await Scalar<long>(connection, "SELECT count(*) FROM pg_indexes WHERE schemaname = 'promotions' AND indexname = 'ix_promotions_scope_merchant_id'"));
        Assert.Equal(1, await Scalar<long>(connection, "SELECT count(*) FROM pg_indexes WHERE schemaname = 'promotions' AND indexname = 'ix_promotions_scope_target_ids' AND indexdef ILIKE '%USING gin%'"));
        Assert.Equal(0, await Scalar<long>(connection, "SELECT count(*) FROM information_schema.table_constraints WHERE constraint_schema = 'promotions' AND constraint_type = 'FOREIGN KEY' AND constraint_name NOT LIKE '%promotions%'"));
    }

    [Fact]
    public async Task PromotionRoundTripsWithOwnedValuesAndUtcTimestamps()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        PromotionsDbContext db = scope.ServiceProvider.GetRequiredService<PromotionsDbContext>();
        Promotion value = Create(PromotionId.New(), "roundtrip", "ROUND_TRIP");
        db.Promotions.Add(value);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Promotion loaded = await db.Promotions.SingleAsync(x => x.Id == value.Id);
        Assert.Equal(MerchantId, Assert.Single(loaded.Scope.TargetIds));
        Assert.Equal("ROUND_TRIP", loaded.CouponCode?.Value);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAtUtc.Kind);
        Assert.Equal(4000, loaded.Funding.PlatformShareBasisPoints);
    }

    [Fact]
    public async Task NormalizedCouponCodeIsCaseInsensitiveUnique()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        PromotionsDbContext db = scope.ServiceProvider.GetRequiredService<PromotionsDbContext>();
        db.Promotions.Add(Create(PromotionId.New(), "coupon-one", "save_20"));
        db.Promotions.Add(Create(PromotionId.New(), "coupon-two", " SAVE_20 "));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ConcurrencyStampProducesRealConflict()
    {
        Promotion value = Create(PromotionId.New(), "concurrency", "CONCURRENT");
        await using (AsyncServiceScope seed = fixture.ApiFactory.Services.CreateAsyncScope())
        {
            PromotionsDbContext db = seed.ServiceProvider.GetRequiredService<PromotionsDbContext>();
            db.Add(value);
            await db.SaveChangesAsync();
        }
        await using AsyncServiceScope firstScope = fixture.ApiFactory.Services.CreateAsyncScope();
        await using AsyncServiceScope secondScope = fixture.ApiFactory.Services.CreateAsyncScope();
        PromotionsDbContext first = firstScope.ServiceProvider.GetRequiredService<PromotionsDbContext>();
        PromotionsDbContext second = secondScope.ServiceProvider.GetRequiredService<PromotionsDbContext>();
        Promotion firstValue = await first.Promotions.SingleAsync(x => x.Id == value.Id);
        Promotion secondValue = await second.Promotions.SingleAsync(x => x.Id == value.Id);
        firstValue.Activate(Now);
        await first.SaveChangesAsync();
        secondValue.Activate(Now);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task RedemptionExternalReferenceIsIdempotentAndDeleteIsRestricted()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        PromotionsDbContext db = scope.ServiceProvider.GetRequiredService<PromotionsDbContext>();
        Promotion promotion = Create(PromotionId.New(), "redemption", "REDEEM");
        db.Promotions.Add(promotion);
        db.Redemptions.Add(PromotionRedemption.Create(PromotionRedemptionId.New(), promotion.Id, Guid.NewGuid(), "operation-1", 100, new Currency("ILS"), Now));
        await db.SaveChangesAsync();
        db.Redemptions.Add(PromotionRedemption.Create(PromotionRedemptionId.New(), promotion.Id, null, "operation-1", 100, new Currency("ILS"), Now));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task PromotionEndpointsRequireAuthentication()
    {
        HttpResponseMessage response = await fixture.ApiFactory.CreateClient().GetAsync("/api/v1/promotions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Promotion Create(PromotionId id, string name, string coupon) =>
        Promotion.Create(id, name, new LocalizedText("عرض", null, "Promotion"), null, PromotionType.Coupon, 10,
            StackabilityPolicy.Stackable, null, new FundingPolicy(FundingSource.Shared, 4000, 6000),
            new ValidityPeriod(Now.AddDays(-1), Now.AddDays(2)), new UsageLimits(100, 2, 100000, 1),
            new EligibilityRules(1000, null, false), new PromotionScope(PromotionScopeType.Merchant, [MerchantId]),
            new DiscountBenefit(DiscountKind.Percentage, new Currency("ILS"), 1000, 500), new CouponCode(coupon), Now.AddDays(-2));

    private static async Task<T> Scalar<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        object? result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }
}
