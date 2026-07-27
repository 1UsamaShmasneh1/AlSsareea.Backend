using System.Data.Common;
using AlSsareea.Modules.Merchants.Domain;
using AlSsareea.Modules.Merchants.Infrastructure.Persistence;
using AlSsareea.Modules.Pricing.Application;
using AlSsareea.Modules.Pricing.Contracts;
using AlSsareea.Modules.Pricing.Domain;
using AlSsareea.Modules.Pricing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class PricingPersistenceTests(PostgresFixture fixture)
{
    private static readonly DateTime Now = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task MigrationCreatesIndependentSchemaConstraintsIndexesAndRestrictiveForeignKey()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        PricingDbContext db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        await db.Database.OpenConnectionAsync();
        DbConnection connection = db.Database.GetDbConnection();

        Assert.Equal(3, await Scalar<long>(connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema='pricing'"));
        Assert.True(await Scalar<long>(connection,
            "SELECT count(*) FROM information_schema.check_constraints WHERE constraint_schema='pricing'") >= 12);
        Assert.True(await Scalar<long>(connection,
            "SELECT count(*) FROM pg_indexes WHERE schemaname='pricing'") >= 6);
        Assert.Equal(1, await Scalar<long>(connection,
            "SELECT count(*) FROM pg_constraint c JOIN pg_namespace n ON n.oid=c.connamespace WHERE n.nspname='pricing' AND c.contype='f' AND c.confdeltype='r'"));
        Assert.Equal(0, await Scalar<long>(connection,
            "SELECT count(*) FROM information_schema.constraint_column_usage WHERE constraint_schema='pricing' AND table_schema <> 'pricing'"));
    }

    [Fact]
    public async Task ActivePolicyAndOwnedRulesRoundTripAndResolveByEffectivePeriod()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        PricingDbContext db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        PricingPolicy policy = NewPolicy(Guid.NewGuid());
        policy.ReplaceRules([
            PricingRule.Create(PricingRuleId.New(), PricingRuleType.FixedDelivery,
                PricingCalculationKind.Fixed, PricingCalculationBase.ItemsSubtotal, 10, 150),
        ], Now.AddMinutes(1));
        policy.Activate(Now.AddMinutes(2));
        db.Policies.Add(policy);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        PricingPolicy loaded = await db.Policies.Include(x => x.Rules).SingleAsync(x =>
            x.Id == policy.Id && x.Status == PricingPolicyStatus.Active &&
            x.EffectiveFromUtc <= Now.AddMinutes(3) &&
            (!x.EffectiveUntilUtc.HasValue || x.EffectiveUntilUtc > Now.AddMinutes(3)));

        Assert.Equal(policy.Id, loaded.Id);
        Assert.Single(loaded.Rules);
        Assert.Equal(150, PricingPolicyCalculator.Calculate(loaded, 1_000, null).DeliveryFeeMinor);
    }

    [Fact]
    public async Task ConcurrentPolicyUpdatesAreRejected()
    {
        PricingPolicy policy = NewPolicy(Guid.NewGuid());
        await using (AsyncServiceScope setup = fixture.ApiFactory.Services.CreateAsyncScope())
        {
            PricingDbContext db = setup.ServiceProvider.GetRequiredService<PricingDbContext>();
            db.Policies.Add(policy);
            await db.SaveChangesAsync();
        }

        await using AsyncServiceScope firstScope = fixture.ApiFactory.Services.CreateAsyncScope();
        await using AsyncServiceScope secondScope = fixture.ApiFactory.Services.CreateAsyncScope();
        PricingDbContext first = firstScope.ServiceProvider.GetRequiredService<PricingDbContext>();
        PricingDbContext second = secondScope.ServiceProvider.GetRequiredService<PricingDbContext>();
        PricingPolicy firstCopy = await first.Policies.SingleAsync(x => x.Id == policy.Id);
        PricingPolicy secondCopy = await second.Policies.SingleAsync(x => x.Id == policy.Id);
        firstCopy.UpdateDraft("First", Now, null, 10, Now.AddMinutes(1));
        secondCopy.UpdateDraft("Second", Now, null, 10, Now.AddMinutes(2));
        await first.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task DatabaseRejectsNegativeRuleAmounts()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        PricingDbContext db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        PricingPolicy policy = NewPolicy(Guid.NewGuid());
        db.Policies.Add(policy);
        await db.SaveChangesAsync();

        await Assert.ThrowsAnyAsync<DbException>(() => db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO pricing.pricing_rules
                (id, pricing_policy_id, type, kind, calculation_base, priority, amount_minor,
                 percentage_basis_points)
            VALUES
                ({0}, {1}, 1, 1, 1, 1, -1, 0)
            """, Guid.NewGuid(), policy.Id.Value));
    }

    [Fact]
    public async Task EstimateResolvesActiveMerchantPolicyAndReportsCurrencyMismatch()
    {
        Guid merchantId = Guid.NewGuid();
        await SeedActiveMerchant(merchantId);
        PricingPolicy policy = NewPolicy(merchantId);
        policy.ReplaceRules([
            PricingRule.Create(PricingRuleId.New(), PricingRuleType.FixedDelivery,
                PricingCalculationKind.Fixed, PricingCalculationBase.ItemsSubtotal, 10, 150),
            PricingRule.Create(PricingRuleId.New(), PricingRuleType.ServiceFee,
                PricingCalculationKind.Percentage, PricingCalculationBase.ItemsSubtotalPlusDelivery,
                10, 0, percentageBasisPoints: 1_000),
        ], Now.AddMinutes(1));
        policy.Activate(Now.AddMinutes(2));

        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        PricingDbContext db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        db.Policies.Add(policy);
        await db.SaveChangesAsync();
        IPricingService service = scope.ServiceProvider.GetRequiredService<IPricingService>();
        PricingActor platform = new(Guid.NewGuid(), true);

        PricingOperationResult<PricingEstimateResponse> estimate = await service.EstimateAsync(
            new(merchantId, null, null, "ils", 1_000, null, Now.AddMinutes(3)),
            platform, CancellationToken.None);
        PricingOperationResult<PricingEstimateResponse> mismatch = await service.EstimateAsync(
            new(merchantId, null, null, "USD", 1_000, null, Now.AddMinutes(3)),
            platform, CancellationToken.None);

        Assert.Equal(PricingOperationStatus.Success, estimate.Status);
        Assert.Equal(150, estimate.Value!.Breakdown.DeliveryFeeMinor);
        Assert.Equal(115, estimate.Value.Breakdown.ServiceFeeMinor);
        Assert.Equal(1_265, estimate.Value.Breakdown.GrandTotalMinor);
        Assert.Equal(policy.Id.Value, estimate.Value.Snapshot.PolicyId);
        Assert.Equal(policy.Version, estimate.Value.Snapshot.PolicyVersion);
        Assert.Equal(PricingOperationStatus.Invalid, mismatch.Status);
        Assert.Equal(PricingErrorCodes.CurrencyMismatch, mismatch.ErrorCode);
    }

    [Fact]
    public async Task ActivationRejectsAnOverlappingActivePolicy()
    {
        Guid merchantId = Guid.NewGuid();
        await SeedActiveMerchant(merchantId);
        PricingPolicy first = NewPolicy(merchantId);
        PricingPolicy second = NewPolicy(merchantId);
        first.ReplaceRules([FixedDeliveryRule()], Now.AddMinutes(1));
        second.ReplaceRules([FixedDeliveryRule()], Now.AddMinutes(1));

        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        PricingDbContext db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        db.Policies.AddRange(first, second);
        await db.SaveChangesAsync();
        IPricingService service = scope.ServiceProvider.GetRequiredService<IPricingService>();
        PricingActor platform = new(Guid.NewGuid(), true);

        PricingOperationResult<PricingPolicyDto> activated = await service.ChangeStatusAsync(
            first.Id.Value, "activate", new(first.ConcurrencyStamp), platform, CancellationToken.None);
        PricingOperationResult<PricingPolicyDto> conflict = await service.ChangeStatusAsync(
            second.Id.Value, "activate", new(second.ConcurrencyStamp), platform, CancellationToken.None);

        Assert.Equal(PricingOperationStatus.Success, activated.Status);
        Assert.Equal(PricingOperationStatus.Conflict, conflict.Status);
        Assert.Equal("pricing.active_policy_overlap", conflict.ErrorCode);
    }

    private static PricingPolicy NewPolicy(Guid merchantId) => PricingPolicy.Create(
        PricingPolicyId.New(), "Merchant policy",
        PricingScope.Create(PricingScopeType.Merchant, merchantId, null, null),
        "ILS", Now, null, 10, Now);

    private static PricingRule FixedDeliveryRule() => PricingRule.Create(
        PricingRuleId.New(), PricingRuleType.FixedDelivery,
        PricingCalculationKind.Fixed, PricingCalculationBase.ItemsSubtotal, 10, 150);

    private async Task SeedActiveMerchant(Guid merchantId)
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        MerchantsDbContext db = scope.ServiceProvider.GetRequiredService<MerchantsDbContext>();
        Merchant merchant = Merchant.Create(
            new MerchantId(merchantId), "Pricing Merchant", "Pricing Merchant", null, null, null,
            $"{merchantId:N}@example.test", "+970599000000", Guid.NewGuid(), Now.AddDays(-1));
        merchant.Activate(Now.AddHours(-23));
        db.Merchants.Add(merchant);
        await db.SaveChangesAsync();
    }

    private static async Task<T> Scalar<T>(DbConnection connection, string sql)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object result = await command.ExecuteScalarAsync() ??
            throw new InvalidOperationException("SQL scalar query returned null.");
        return (T)Convert.ChangeType(result, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }
}
