using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Pricing.Contracts;
using AlSsareea.Modules.Promotions.Application;
using AlSsareea.Modules.Promotions.Contracts;
using AlSsareea.Modules.Promotions.Domain;

namespace AlSsareea.UnitTests.Promotions;

public sealed class PromotionDomainTests
{
    private static readonly DateTime Now = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid MerchantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void ValidPromotionStartsDraftAndRaisesCreatedEvent()
    {
        Promotion promotion = Create();
        Assert.Equal(PromotionStatus.Draft, promotion.Status);
        Assert.IsType<PromotionCreatedDomainEvent>(Assert.Single(promotion.DomainEvents));
        Assert.NotEqual(Guid.Empty, promotion.ConcurrencyStamp);
    }

    [Fact]
    public void LifecycleIsEnforcedAndArchiveIsTerminal()
    {
        Promotion promotion = Create();
        promotion.Activate(Now);
        promotion.Suspend(Now.AddMinutes(1));
        promotion.Activate(Now.AddMinutes(2));
        promotion.Archive(Now.AddMinutes(3));
        Assert.Equal(PromotionStatus.Archived, promotion.Status);
        Assert.Throws<DomainException>(() => promotion.Activate(Now.AddMinutes(4)));
        Assert.Throws<DomainException>(() => promotion.Update("x", Text(), null, 1, StackabilityPolicy.Stackable, null, FundingPolicy.Platform, Period(), UsageLimits.Unlimited, EligibilityRules.None, Scope(), Fixed(), null, Now.AddMinutes(4)));
    }

    [Fact]
    public void ExpiredPromotionCannotActivate() =>
        Assert.Throws<DomainException>(() => Create(period: new ValidityPeriod(Now.AddDays(-2), Now.AddDays(-1))).Activate(Now));

    [Fact]
    public void ValidityRequiresUtcOrderedValues()
    {
        Assert.Throws<DomainException>(() => new ValidityPeriod(Now, Now));
        Assert.Throws<DomainException>(() => new ValidityPeriod(DateTime.SpecifyKind(Now, DateTimeKind.Local), Now.AddDays(1)));
    }

    [Theory]
    [InlineData(1000, 400, 400)]
    [InlineData(100, 200, 100)]
    public void FixedDiscountNeverMakesAmountNegative(long eligible, long value, long expected) =>
        Assert.Equal(expected, new DiscountBenefit(DiscountKind.FixedAmount, new Currency("ils"), value).Calculate(eligible, 0));

    [Fact]
    public void PercentageDiscountHonorsMaximumCap()
    {
        var value = new DiscountBenefit(DiscountKind.Percentage, new Currency("USD"), 2500, 100);
        Assert.Equal(100, value.Calculate(1000, 0));
    }

    [Fact]
    public void FreeDeliveryUsesDeliveryFee()
    {
        var value = new DiscountBenefit(DiscountKind.FreeDelivery, new Currency("ILS"), 0);
        Assert.Equal(275, value.Calculate(1000, 275));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10001)]
    public void PercentageRejectsInvalidBasisPoints(long basisPoints) =>
        Assert.Throws<DomainException>(() => new DiscountBenefit(DiscountKind.Percentage, new Currency("ILS"), basisPoints));

    [Fact]
    public void CouponNormalizationIsDeterministic()
    {
        Assert.Equal("SAVE_10", new CouponCode(" save_10 ").Value);
        Assert.Equal(new CouponCode("save_10"), new CouponCode("SAVE_10"));
        Assert.Throws<DomainException>(() => new CouponCode(""));
    }

    [Fact]
    public void ValidCouponIsAppliedAndWrongCouponIsExplained()
    {
        Promotion promotion = Promotion.Create(PromotionId.New(), "coupon", Text(), null, PromotionType.Coupon, 1,
            StackabilityPolicy.Stackable, null, FundingPolicy.Platform, Period(), UsageLimits.Unlimited,
            EligibilityRules.None, Scope(), Fixed(), new CouponCode("save10"), Now.AddDays(-1));
        promotion.Activate(Now);
        Assert.Single(PromotionEvaluator.Evaluate([promotion], Input() with { CouponCode = " SAVE10 " }, Now).Snapshots);
        Assert.Equal(PromotionErrorCodes.CouponInvalid, Assert.Single(PromotionEvaluator.Evaluate([promotion], Input() with { CouponCode = "WRONG" }, Now).Decisions).ReasonCode);
    }

    [Fact]
    public void FundingMustBeConsistent()
    {
        _ = new FundingPolicy(FundingSource.Shared, 4000, 6000);
        Assert.Throws<DomainException>(() => new FundingPolicy(FundingSource.Shared, 0, 10000));
        Assert.Throws<DomainException>(() => new FundingPolicy(FundingSource.Platform, 5000, 5000));
    }

    [Fact]
    public void UsageLimitsUseNullForUnlimitedAndValidateCustomerLimit()
    {
        Assert.True(UsageLimits.Unlimited.IsUnlimited);
        Assert.Throws<DomainException>(() => new UsageLimits(2, 3, null, null));
    }

    [Fact]
    public void CouponTypeRequiresCouponAndProductTypeRequiresProductScope()
    {
        Assert.Throws<DomainException>(() => Create(type: PromotionType.Coupon));
        Assert.Throws<DomainException>(() => Create(type: PromotionType.ProductDiscount));
    }

    [Fact]
    public void EvaluationExplainsEligibilityCurrencyAndUsageRejections()
    {
        Promotion currency = Create(benefit: new DiscountBenefit(DiscountKind.FixedAmount, new Currency("USD"), 100));
        Promotion usage = Create(id: PromotionId.New(), limits: new UsageLimits(1, 1, null, null));
        currency.Activate(Now);
        usage.Activate(Now);
        PromotionEvaluationResponse result = PromotionEvaluator.Evaluate([currency, usage], Input() with { Usage = new UsageContext(1, 1, 0, true) }, Now);
        Assert.Contains(result.Decisions, x => x.ReasonCode == PromotionErrorCodes.CurrencyMismatch);
        Assert.Contains(result.Decisions, x => x.ReasonCode == PromotionErrorCodes.UsageLimitReached);
        Assert.Empty(result.Snapshots);
    }

    [Fact]
    public void MinimumSubtotalAndMerchantScopeAreEnforced()
    {
        Promotion value = Create(eligibility: new EligibilityRules(2000, null, false));
        value.Activate(Now);
        PromotionEvaluationResponse result = PromotionEvaluator.Evaluate(
            [value],
            Input() with { Pricing = Breakdown(1000, 200) },
            Now);
        Assert.Equal(PromotionErrorCodes.NotEligible, Assert.Single(result.Decisions).ReasonCode);
    }

    [Fact]
    public void BranchScopeRejectsAnotherBranch()
    {
        Promotion value = Create(scope: new PromotionScope(PromotionScopeType.Branch, [Guid.NewGuid()], MerchantId));
        value.Activate(Now);
        Assert.Equal(PromotionErrorCodes.NotEligible, Assert.Single(PromotionEvaluator.Evaluate([value], Input() with { BranchId = Guid.NewGuid() }, Now).Decisions).ReasonCode);
    }

    [Fact]
    public void ProductAndCategoryEligibilityUseOnlyEligibleLines()
    {
        Guid product = Guid.NewGuid();
        Guid category = Guid.NewGuid();
        Promotion productPromotion = Create(type: PromotionType.ProductDiscount, scope: new PromotionScope(PromotionScopeType.Product, [product], MerchantId), benefit: Fixed(500));
        Promotion categoryPromotion = Create(id: PromotionId.New(), type: PromotionType.CategoryDiscount, scope: new PromotionScope(PromotionScopeType.Category, [category], MerchantId), benefit: Fixed(500));
        productPromotion.Activate(Now);
        categoryPromotion.Activate(Now);
        EvaluatePromotionsRequest input = Input() with { Lines = [new PromotionLineContext(product, category, 300)] };
        PromotionEvaluationResponse result = PromotionEvaluator.Evaluate([productPromotion, categoryPromotion], input, Now);
        Assert.Equal(600, result.TotalAdjustmentMinor);
    }

    [Fact]
    public void PriorityConflictGroupsAndTieBreakingAreDeterministic()
    {
        Promotion low = Create(id: new PromotionId(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")), priority: 1, conflictGroup: "order", benefit: Fixed(500));
        Promotion high = Create(id: new PromotionId(Guid.Parse("00000000-0000-0000-0000-000000000001")), priority: 2, conflictGroup: "order", benefit: Fixed(100));
        low.Activate(Now);
        high.Activate(Now);
        PromotionEvaluationResponse first = PromotionEvaluator.Evaluate([low, high], Input(), Now);
        PromotionEvaluationResponse second = PromotionEvaluator.Evaluate([high, low], Input(), Now);
        Assert.Equal(high.Id.Value, Assert.Single(first.Snapshots).PromotionId);
        Assert.Equal(first.Snapshots, second.Snapshots);
        Assert.Contains(first.Decisions, x => x.PromotionId == low.Id.Value && x.ConflictDecision == "conflict_group");
    }

    [Theory]
    [InlineData(StackabilityPolicy.Exclusive)]
    [InlineData(StackabilityPolicy.NonStackable)]
    public void ExclusivityAndNonStackabilityApplyOnlyOne(StackabilityPolicy policy)
    {
        Promotion first = Create(stackability: policy);
        Promotion second = Create(id: PromotionId.New());
        first.Activate(Now);
        second.Activate(Now);
        Assert.Single(PromotionEvaluator.Evaluate([first, second], Input(), Now).Snapshots);
    }

    [Fact]
    public void EvaluationContainsBreakdownFundingAndStableSnapshot()
    {
        Promotion value = Create(funding: new FundingPolicy(FundingSource.Shared, 4000, 6000), benefit: new DiscountBenefit(DiscountKind.Percentage, new Currency("ILS"), 1000));
        value.Activate(Now);
        Guid policyId = Guid.NewGuid();
        PricingBreakdownDto pricing = Breakdown(1000, 200);
        var pricingSnapshot = new PricingSnapshotDto(
            policyId,
            3,
            new PricingScopeDto(3, MerchantId, null, null),
            pricing,
            [],
            Now,
            null,
            MerchantId,
            null,
            null,
            null,
            true,
            null);
        PromotionEvaluationResponse result = PromotionEvaluator.Evaluate([value], Input() with { PricingSnapshot = pricingSnapshot }, Now);
        PromotionEvaluationSnapshot snapshot = Assert.Single(result.Snapshots);
        Assert.Equal(100, result.ProductDiscountMinor);
        Assert.Equal(40, snapshot.Funding.PlatformAmountMinor);
        Assert.Equal(60, snapshot.Funding.MerchantAmountMinor);
        Assert.Equal(value.ConcurrencyStamp, snapshot.PromotionVersion);
        Assert.Equal(policyId, snapshot.PricingPolicyId);
        Assert.Equal(3, snapshot.PricingPolicyVersion);
    }

    private static Promotion Create(
        PromotionId? id = null, ValidityPeriod? period = null, PromotionType type = PromotionType.MerchantDiscount,
        PromotionScope? scope = null, DiscountBenefit? benefit = null, int priority = 1,
        StackabilityPolicy stackability = StackabilityPolicy.Stackable, string? conflictGroup = null,
        FundingPolicy? funding = null, UsageLimits? limits = null, EligibilityRules? eligibility = null) =>
        Promotion.Create(id ?? PromotionId.New(), "campaign", Text(), null, type, priority, stackability, conflictGroup,
            funding ?? FundingPolicy.Platform, period ?? Period(), limits ?? UsageLimits.Unlimited, eligibility ?? EligibilityRules.None,
            scope ?? Scope(), benefit ?? Fixed(), type == PromotionType.Coupon ? null : null, Now.AddDays(-1));
    private static LocalizedText Text() => new("عرض", "מבצע", "Promotion");
    private static ValidityPeriod Period() => new(Now.AddDays(-1), Now.AddDays(1));
    private static PromotionScope Scope() => new(PromotionScopeType.Merchant, [MerchantId]);
    private static DiscountBenefit Fixed(long value = 100) => new(DiscountKind.FixedAmount, new Currency("ILS"), value);
    private static EvaluatePromotionsRequest Input() => new(null, MerchantId, null, Breakdown(1000, 200), null, [], null, new UsageContext(0, 0, 0, true));
    private static PricingBreakdownDto Breakdown(long subtotal, long delivery) =>
        new("ILS", subtotal, delivery, 0, 0, 0, 0, 0, subtotal + delivery);
}
