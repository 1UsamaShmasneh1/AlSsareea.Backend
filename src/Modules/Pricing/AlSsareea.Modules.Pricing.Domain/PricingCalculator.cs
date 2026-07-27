using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Pricing.Domain;

public sealed record PricingComputation(
    long ItemsSubtotalMinor,
    long DeliveryFeeMinor,
    long ServiceFeeMinor,
    long PlatformFeeMinor,
    long SmallOrderFeeMinor,
    long TaxMinor,
    long DiscountsMinor,
    long GrandTotalMinor,
    long? MinimumOrderMinor,
    bool IsEligible,
    string? FailureCode,
    IReadOnlyList<PricingRuleId> AppliedRuleIds);

public static class PricingPolicyCalculator
{
    public static PricingComputation Calculate(PricingPolicy policy, long itemsSubtotalMinor, int? distanceMeters)
    {
        if (policy.Status != PricingPolicyStatus.Active) throw new DomainException("Pricing policy is not active.");
        PricingRules.NonNegative(itemsSubtotalMinor, nameof(itemsSubtotalMinor));
        if (distanceMeters < 0) throw new DomainException("Distance cannot be negative.");

        var applied = new List<PricingRuleId>();
        PricingRule? minimumRule = Single(policy.Rules.Where(x => x.Type == PricingRuleType.MinimumOrder), false);
        long? minimum = minimumRule?.ThresholdMinor;
        if (minimum.HasValue && itemsSubtotalMinor < minimum.Value)
        {
            applied.Add(minimumRule!.Id);
            return new(itemsSubtotalMinor, 0, 0, 0, 0, 0, 0, itemsSubtotalMinor, minimum, false, "pricing.minimum_order_not_met", applied);
        }

        PricingRule? deliveryRule = SelectDelivery(policy.Rules, distanceMeters);
        long delivery = deliveryRule switch
        {
            null => 0,
            { Type: PricingRuleType.DistanceDelivery } when distanceMeters.HasValue => deliveryRule.CalculateDistance(distanceMeters.Value),
            _ => deliveryRule.Calculate(itemsSubtotalMinor),
        };
        Add(deliveryRule);

        PricingRule? serviceRule = Single(policy.Rules.Where(x => x.Type == PricingRuleType.ServiceFee), false);
        long serviceBase = Base(serviceRule, itemsSubtotalMinor, delivery, 0, 0, 0);
        long service = serviceRule?.Calculate(serviceBase) ?? 0;
        Add(serviceRule);

        PricingRule? platformRule = Single(policy.Rules.Where(x => x.Type == PricingRuleType.PlatformFee), false);
        long platformBase = Base(platformRule, itemsSubtotalMinor, delivery, service, 0, 0);
        long platform = platformRule?.Calculate(platformBase) ?? 0;
        Add(platformRule);

        PricingRule? smallRule = Single(policy.Rules.Where(x => x.Type == PricingRuleType.SmallOrderFee), false);
        long small = smallRule?.ThresholdMinor is long threshold && itemsSubtotalMinor < threshold ? smallRule.Calculate(itemsSubtotalMinor) : 0;
        if (small > 0) Add(smallRule);

        PricingRule? taxRule = Single(policy.Rules.Where(x => x.Type == PricingRuleType.Tax), false);
        long taxBase = Base(taxRule, itemsSubtotalMinor, delivery, service, platform, small);
        long tax = taxRule?.Calculate(taxBase) ?? 0;
        if (tax > 0) Add(taxRule);

        const long discounts = 0;
        long total = PricingMath.Add(itemsSubtotalMinor, delivery, service, platform, small, tax);
        return new(itemsSubtotalMinor, delivery, service, platform, small, tax, discounts, total, minimum, true, null, applied);

        void Add(PricingRule? rule)
        {
            if (rule is not null) applied.Add(rule.Id);
        }
    }

    private static PricingRule? SelectDelivery(IEnumerable<PricingRule> rules, int? distanceMeters)
    {
        PricingRule[] candidates = [.. rules.Where(x =>
            x.Type is PricingRuleType.FixedDelivery or PricingRuleType.ZoneDelivery ||
            x.Type == PricingRuleType.DistanceDelivery && distanceMeters.HasValue)];
        return Single(candidates, true);
    }

    private static PricingRule? Single(IEnumerable<PricingRule> rules, bool byPriority)
    {
        PricingRule[] values = byPriority
            ? [.. rules.OrderByDescending(x => x.Priority).ThenBy(x => x.Id.Value)]
            : [.. rules];
        if (values.Length == 0) return null;
        if (byPriority && values.Length > 1 && values[0].Priority == values[1].Priority)
            throw new DomainException("Pricing rules are ambiguous at the same priority.");
        if (!byPriority && values.Length > 1) throw new DomainException("Pricing policy contains duplicate rules.");
        return values[0];
    }

    private static long Base(PricingRule? rule, long items, long delivery, long service, long platform, long small) =>
        rule?.CalculationBase switch
        {
            PricingCalculationBase.ItemsSubtotalPlusDelivery => PricingMath.Add(items, delivery),
            PricingCalculationBase.PreTaxTotal => PricingMath.Add(items, delivery, service, platform, small),
            _ => items,
        };
}
