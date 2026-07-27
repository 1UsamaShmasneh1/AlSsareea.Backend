using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Pricing.Domain;

namespace AlSsareea.UnitTests.Pricing;

public sealed class PricingPolicyTests
{
    private static readonly DateTime Now = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PolicyLifecycleIsExplicitAndArchivedPolicyCannotReactivate()
    {
        PricingPolicy policy = Policy();
        policy.ReplaceRules([Fixed(PricingRuleType.FixedDelivery, 100)], Now.AddMinutes(1));
        policy.Activate(Now.AddMinutes(2));
        policy.Deactivate(Now.AddMinutes(3));
        policy.Archive(Now.AddMinutes(4));

        Assert.Equal(PricingPolicyStatus.Archived, policy.Status);
        Assert.Throws<DomainException>(() => policy.Activate(Now.AddMinutes(5)));
        Assert.Contains(policy.DomainEvents, x => x is PricingPolicyActivatedDomainEvent);
    }

    [Fact]
    public void ActivePolicyCannotBeSilentlyEdited()
    {
        PricingPolicy policy = Active(Fixed(PricingRuleType.FixedDelivery, 100));

        Assert.Throws<DomainException>(() =>
            policy.UpdateDraft("Changed", Now, null, 1, Now.AddMinutes(2)));
        Assert.Throws<DomainException>(() =>
            policy.ReplaceRules([Fixed(PricingRuleType.FixedDelivery, 200)], Now.AddMinutes(2)));
    }

    [Theory]
    [InlineData("il")]
    [InlineData("123")]
    [InlineData("TOOLONG")]
    public void InvalidCurrencyIsRejected(string currency) =>
        Assert.Throws<DomainException>(() => PricingPolicy.Create(
            PricingPolicyId.New(), "Policy", PricingScope.Create(PricingScopeType.Global, null, null, null),
            currency, Now, null, 1, Now));

    [Fact]
    public void InvalidEffectiveRangeAndPriorityAreRejected()
    {
        PricingScope scope = PricingScope.Create(PricingScopeType.Global, null, null, null);
        Assert.Throws<DomainException>(() => PricingPolicy.Create(
            PricingPolicyId.New(), "Policy", scope, "ILS", Now, Now, 1, Now));
        Assert.Throws<DomainException>(() => PricingPolicy.Create(
            PricingPolicyId.New(), "Policy", scope, "ILS", Now, null, 1_001, Now));
    }

    [Fact]
    public void EffectivePeriodUsesInclusiveStartAndExclusiveEnd()
    {
        PricingPolicy policy = Policy(Now, Now.AddHours(1));
        policy.ReplaceRules([Fixed(PricingRuleType.FixedDelivery, 100)], Now);
        policy.Activate(Now);

        Assert.True(policy.IsEffectiveAt(Now));
        Assert.True(policy.IsEffectiveAt(Now.AddHours(1).AddTicks(-1)));
        Assert.False(policy.IsEffectiveAt(Now.AddHours(1)));
    }

    [Fact]
    public void FullBreakdownUsesDocumentedOrderAndDiscountsStayZero()
    {
        PricingPolicy policy = Active(
            Fixed(PricingRuleType.FixedDelivery, 100, priority: 10),
            Percentage(PricingRuleType.ServiceFee, 1_000, PricingCalculationBase.ItemsSubtotalPlusDelivery),
            Fixed(PricingRuleType.PlatformFee, 20),
            PricingRule.Create(PricingRuleId.New(), PricingRuleType.SmallOrderFee, PricingCalculationKind.Fixed, PricingCalculationBase.ItemsSubtotal, 1, 50, thresholdMinor: 1_500),
            Percentage(PricingRuleType.Tax, 500, PricingCalculationBase.PreTaxTotal));

        PricingComputation result = PricingPolicyCalculator.Calculate(policy, 1_000, null);

        Assert.Equal(100, result.DeliveryFeeMinor);
        Assert.Equal(110, result.ServiceFeeMinor);
        Assert.Equal(20, result.PlatformFeeMinor);
        Assert.Equal(50, result.SmallOrderFeeMinor);
        Assert.Equal(64, result.TaxMinor);
        Assert.Equal(0, result.DiscountsMinor);
        Assert.Equal(1_344, result.GrandTotalMinor);
        Assert.True(result.IsEligible);
    }

    [Theory]
    [InlineData(999, 50)]
    [InlineData(1_000, 0)]
    [InlineData(1_001, 0)]
    public void SmallOrderFeeUsesStrictThreshold(long subtotal, long expected)
    {
        PricingRule small = PricingRule.Create(
            PricingRuleId.New(), PricingRuleType.SmallOrderFee, PricingCalculationKind.Fixed,
            PricingCalculationBase.ItemsSubtotal, 1, 50, thresholdMinor: 1_000);
        PricingComputation result = PricingPolicyCalculator.Calculate(Active(small), subtotal, null);
        Assert.Equal(expected, result.SmallOrderFeeMinor);
    }

    [Theory]
    [InlineData(4_999, false)]
    [InlineData(5_000, true)]
    [InlineData(5_001, true)]
    public void MinimumOrderRejectsBeforeFees(long subtotal, bool eligible)
    {
        PricingRule minimum = PricingRule.Create(
            PricingRuleId.New(), PricingRuleType.MinimumOrder, PricingCalculationKind.Fixed,
            PricingCalculationBase.ItemsSubtotal, 1, 0, thresholdMinor: 5_000);
        PricingComputation result = PricingPolicyCalculator.Calculate(
            Active(minimum, Fixed(PricingRuleType.FixedDelivery, 100)), subtotal, null);
        Assert.Equal(eligible, result.IsEligible);
        if (!eligible) Assert.Equal("pricing.minimum_order_not_met", result.FailureCode);
    }

    [Theory]
    [InlineData(2_000, 100)]
    [InlineData(2_001, 125)]
    [InlineData(3_000, 125)]
    [InlineData(3_001, 150)]
    [InlineData(10_000, 250)]
    public void DistanceDeliveryChargesStartedKilometersAndHonorsMaximum(int meters, long expected)
    {
        PricingRule distance = PricingRule.Create(
            PricingRuleId.New(), PricingRuleType.DistanceDelivery, PricingCalculationKind.Fixed,
            PricingCalculationBase.ItemsSubtotal, 10, 100,
            minimumMinor: 75, maximumMinor: 250, includedDistanceMeters: 2_000,
            maximumDistanceMeters: 10_000, additionalFeePerKilometerMinor: 25);
        PricingComputation result = PricingPolicyCalculator.Calculate(Active(distance), 1_000, meters);
        Assert.Equal(expected, result.DeliveryFeeMinor);
    }

    [Fact]
    public void MaximumDistanceIsRejected()
    {
        PricingRule distance = PricingRule.Create(
            PricingRuleId.New(), PricingRuleType.DistanceDelivery, PricingCalculationKind.Fixed,
            PricingCalculationBase.ItemsSubtotal, 10, 100,
            includedDistanceMeters: 0, maximumDistanceMeters: 1_000,
            additionalFeePerKilometerMinor: 10);
        Assert.Throws<DomainException>(() =>
            PricingPolicyCalculator.Calculate(Active(distance), 1_000, 1_001));
    }

    [Fact]
    public void HigherPriorityDeliveryRuleWinsAndEqualPriorityIsAmbiguous()
    {
        PricingRule fixedRule = Fixed(PricingRuleType.FixedDelivery, 100, priority: 1);
        PricingRule zoneRule = Fixed(PricingRuleType.ZoneDelivery, 80, priority: 2);
        Assert.Equal(80, PricingPolicyCalculator.Calculate(Active(fixedRule, zoneRule), 1_000, null).DeliveryFeeMinor);

        PricingRule samePriority = Fixed(PricingRuleType.FixedDelivery, 90, priority: 2);
        Assert.Throws<DomainException>(() =>
            PricingPolicyCalculator.Calculate(Active(zoneRule, samePriority), 1_000, null));
    }

    [Fact]
    public void FixedPercentageAndDisabledTaxAreSupported()
    {
        PricingRule fixedTax = Fixed(PricingRuleType.Tax, 30);
        PricingRule percentageTax = Percentage(PricingRuleType.Tax, 1_000, PricingCalculationBase.ItemsSubtotal);
        PricingRule disabledTax = PricingRule.Create(
            PricingRuleId.New(), PricingRuleType.Tax, PricingCalculationKind.Disabled,
            PricingCalculationBase.ItemsSubtotal, 1, 0);

        Assert.Equal(30, PricingPolicyCalculator.Calculate(Active(fixedTax), 100, null).TaxMinor);
        Assert.Equal(10, PricingPolicyCalculator.Calculate(Active(percentageTax), 100, null).TaxMinor);
        Assert.Equal(0, PricingPolicyCalculator.Calculate(Active(disabledTax), 100, null).TaxMinor);
    }

    [Fact]
    public void ScopeShapesAreValidatedAndHaveStablePrecedence()
    {
        Guid merchant = Guid.NewGuid();
        PricingScope global = PricingScope.Create(PricingScopeType.Global, null, null, null);
        PricingScope zone = PricingScope.Create(PricingScopeType.ServiceZone, null, null, Guid.NewGuid());
        PricingScope merchantScope = PricingScope.Create(PricingScopeType.Merchant, merchant, null, null);
        PricingScope branch = PricingScope.Create(PricingScopeType.MerchantBranch, merchant, Guid.NewGuid(), null);

        Assert.True(branch.Specificity > merchantScope.Specificity);
        Assert.True(merchantScope.Specificity > zone.Specificity);
        Assert.True(zone.Specificity > global.Specificity);
        Assert.Throws<DomainException>(() =>
            PricingScope.Create(PricingScopeType.MerchantBranch, merchant, null, null));
    }

    private static PricingPolicy Policy(DateTime? from = null, DateTime? until = null) =>
        PricingPolicy.Create(PricingPolicyId.New(), "Default", PricingScope.Create(
            PricingScopeType.Global, null, null, null), "ILS", from ?? Now, until, 10, Now);

    private static PricingPolicy Active(params PricingRule[] rules)
    {
        PricingPolicy policy = Policy();
        policy.ReplaceRules(rules, Now.AddMinutes(1));
        policy.Activate(Now.AddMinutes(2));
        return policy;
    }

    private static PricingRule Fixed(PricingRuleType type, long amount, int priority = 1) =>
        PricingRule.Create(PricingRuleId.New(), type, PricingCalculationKind.Fixed,
            PricingCalculationBase.ItemsSubtotal, priority, amount);

    private static PricingRule Percentage(
        PricingRuleType type,
        int basisPoints,
        PricingCalculationBase calculationBase) =>
        PricingRule.Create(PricingRuleId.New(), type, PricingCalculationKind.Percentage,
            calculationBase, 1, 0, basisPoints);
}
