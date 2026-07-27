using AlSsareea.Modules.Catalog.Domain;
using AlSsareea.Modules.Promotions.Contracts;
using AlSsareea.Modules.Promotions.Domain;

namespace AlSsareea.Modules.Promotions.Application;

public static class PromotionEvaluator
{
    public static PromotionEvaluationResponse Evaluate(
        IReadOnlyCollection<Promotion> promotions,
        EvaluatePromotionsRequest input,
        DateTime evaluatedAtUtc)
    {
        Currency currency = new(input.Pricing.Currency);
        if (input.MerchantId == Guid.Empty || input.Pricing.ItemsSubtotalMinor < 0 || input.Pricing.DeliveryFeeMinor < 0 ||
            input.CustomerId == Guid.Empty || input.BranchId == Guid.Empty ||
            input.Lines.Any(x => x.ProductId == Guid.Empty || x.CategoryId == Guid.Empty || x.SubtotalMinor < 0) ||
            input.PricingSnapshot is not null &&
            (input.PricingSnapshot.MerchantId != input.MerchantId ||
             input.PricingSnapshot.BranchId != input.BranchId ||
             input.PricingSnapshot.Breakdown != input.Pricing))
            throw new AlSsareea.BuildingBlocks.Domain.DomainException("Evaluation input is invalid.");

        TypedLine[] lines = input.Lines
            .Select(x => new TypedLine(new ProductId(x.ProductId), x.CategoryId is null ? null : new CategoryId(x.CategoryId.Value), x.SubtotalMinor))
            .ToArray();
        string? coupon = string.IsNullOrWhiteSpace(input.CouponCode) ? null : new CouponCode(input.CouponCode).Value;
        List<Candidate> candidates = [];
        List<PromotionDecision> rejected = [];
        foreach (Promotion promotion in promotions.OrderBy(x => x.Id.Value))
        {
            string? rejection = Reject(promotion, input, lines, currency, coupon, evaluatedAtUtc);
            if (rejection is not null)
            {
                rejected.Add(new PromotionDecision(promotion.Id.Value, false, rejection, 0, null));
                continue;
            }
            long eligible = EligibleAmount(promotion, input, lines);
            long discount = promotion.Benefit.Calculate(eligible, input.Pricing.DeliveryFeeMinor);
            candidates.Add(new Candidate(promotion, discount));
        }

        Candidate[] ordered = [.. candidates
            .OrderByDescending(x => x.Promotion.Priority)
            .ThenByDescending(x => x.Discount)
            .ThenBy(x => x.Promotion.Validity.StartsAtUtc)
            .ThenBy(x => x.Promotion.Id.Value)];
        List<Candidate> applied = [];
        List<PromotionDecision> conflictRejected = [];
        foreach (Candidate candidate in ordered)
        {
            string? conflict = Conflict(candidate.Promotion, applied.Select(x => x.Promotion));
            if (conflict is not null)
            {
                conflictRejected.Add(new PromotionDecision(candidate.Promotion.Id.Value, false, PromotionErrorCodes.Conflict, 0, conflict));
                continue;
            }
            applied.Add(candidate);
        }

        long productDiscount = applied.Where(x => x.Promotion.Benefit.Kind != DiscountKind.FreeDelivery).Aggregate(0L, (total, value) => SaturatingAdd(total, value.Discount));
        productDiscount = Math.Min(productDiscount, input.Pricing.ItemsSubtotalMinor);
        long freeDelivery = Math.Min(applied.Where(x => x.Promotion.Benefit.Kind == DiscountKind.FreeDelivery).Aggregate(0L, (total, value) => SaturatingAdd(total, value.Discount)), input.Pricing.DeliveryFeeMinor);
        List<PromotionDecision> decisions = [.. rejected, .. conflictRejected];
        List<PromotionEvaluationSnapshot> snapshots = [];
        foreach (Candidate candidate in applied)
        {
            long discount = candidate.Promotion.Benefit.Kind == DiscountKind.FreeDelivery
                ? Math.Min(candidate.Discount, Math.Max(0, input.Pricing.DeliveryFeeMinor - snapshots.Where(x => x.PromotionType == (short)PromotionType.FreeDelivery).Aggregate(0L, (total, value) => SaturatingAdd(total, value.DiscountAmountMinor))))
                : Math.Min(candidate.Discount, Math.Max(0, input.Pricing.ItemsSubtotalMinor - snapshots.Where(x => x.PromotionType != (short)PromotionType.FreeDelivery).Aggregate(0L, (total, value) => SaturatingAdd(total, value.DiscountAmountMinor))));
            FundingBreakdown funding = Funding(candidate.Promotion.Funding, discount);
            decisions.Add(new PromotionDecision(candidate.Promotion.Id.Value, true, "promotions.applied", discount, null));
            snapshots.Add(new PromotionEvaluationSnapshot(
                candidate.Promotion.Id.Value,
                candidate.Promotion.ConcurrencyStamp,
                (short)candidate.Promotion.Type,
                candidate.Promotion.CouponCode?.Value,
                discount,
                currency.Value,
                funding,
                (short)candidate.Promotion.Scope.Type,
                "promotions.applied",
                evaluatedAtUtc,
                input.PricingSnapshot?.PolicyId,
                input.PricingSnapshot?.PolicyVersion));
        }

        return new PromotionEvaluationResponse(
            input.Pricing.ItemsSubtotalMinor,
            input.Pricing.DeliveryFeeMinor,
            productDiscount,
            freeDelivery,
            SaturatingAdd(productDiscount, freeDelivery),
            input.Pricing.ItemsSubtotalMinor - productDiscount,
            input.Pricing.DeliveryFeeMinor - freeDelivery,
            currency.Value,
            evaluatedAtUtc,
            decisions.OrderByDescending(x => x.Applied).ThenBy(x => x.PromotionId).ToArray(),
            snapshots);
    }

    private static string? Reject(Promotion promotion, EvaluatePromotionsRequest input, IReadOnlyCollection<TypedLine> lines, Currency currency, string? coupon, DateTime now)
    {
        if (promotion.Status != PromotionStatus.Active) return PromotionErrorCodes.Inactive;
        if (now < promotion.Validity.StartsAtUtc) return PromotionErrorCodes.NotStarted;
        if (now >= promotion.Validity.EndsAtUtc) return PromotionErrorCodes.Expired;
        if (promotion.Benefit.Currency != currency) return PromotionErrorCodes.CurrencyMismatch;
        if (promotion.CouponCode is not null && promotion.CouponCode.Value.Value != coupon) return coupon is null ? PromotionErrorCodes.CouponRequired : PromotionErrorCodes.CouponInvalid;
        if (promotion.Eligibility.MinimumSubtotalMinor > input.Pricing.ItemsSubtotalMinor ||
            promotion.Eligibility.CustomerId is not null && promotion.Eligibility.CustomerId != input.CustomerId ||
            promotion.Eligibility.FirstOrderOnly && !input.Usage.IsFirstOrder)
            return PromotionErrorCodes.NotEligible;
        if (promotion.UsageLimits.GlobalLimit <= input.Usage.GlobalRedemptions) return PromotionErrorCodes.UsageLimitReached;
        if (promotion.UsageLimits.PerCustomerLimit <= input.Usage.CustomerRedemptions) return PromotionErrorCodes.CustomerLimitReached;
        if (promotion.UsageLimits.BudgetLimitMinor <= input.Usage.DiscountSpentMinor) return PromotionErrorCodes.UsageLimitReached;
        return ScopeApplies(promotion.Scope, input, lines) ? null : PromotionErrorCodes.NotEligible;
    }

    private static bool ScopeApplies(PromotionScope scope, EvaluatePromotionsRequest input, IReadOnlyCollection<TypedLine> lines) =>
        scope.MerchantId is not null && scope.MerchantId != input.MerchantId ? false : scope.Type switch
        {
            PromotionScopeType.Global => true,
            PromotionScopeType.Merchant => scope.TargetIds.Contains(input.MerchantId),
            PromotionScopeType.Branch => input.BranchId is not null && scope.TargetIds.Contains(input.BranchId.Value),
            PromotionScopeType.Product => lines.Any(x => scope.TargetIds.Contains(x.ProductId.Value)),
            PromotionScopeType.Category => lines.Any(x => x.CategoryId is not null && scope.TargetIds.Contains(x.CategoryId.Value.Value)),
            _ => false,
        };
    private static long EligibleAmount(Promotion promotion, EvaluatePromotionsRequest input, IReadOnlyCollection<TypedLine> lines) => promotion.Scope.Type switch
    {
        PromotionScopeType.Product => lines.Where(x => promotion.Scope.TargetIds.Contains(x.ProductId.Value)).Sum(x => x.SubtotalMinor),
        PromotionScopeType.Category => lines.Where(x => x.CategoryId is not null && promotion.Scope.TargetIds.Contains(x.CategoryId.Value.Value)).Sum(x => x.SubtotalMinor),
        _ => input.Pricing.ItemsSubtotalMinor,
    };
    private static string? Conflict(Promotion candidate, IEnumerable<Promotion> selected)
    {
        Promotion[] values = selected.ToArray();
        if (values.Length == 0) return null;
        if (candidate.Stackability == StackabilityPolicy.Exclusive || values.Any(x => x.Stackability == StackabilityPolicy.Exclusive)) return "exclusive";
        if (candidate.Stackability == StackabilityPolicy.NonStackable || values.Any(x => x.Stackability == StackabilityPolicy.NonStackable)) return "non_stackable";
        return candidate.ConflictGroup is not null && values.Any(x => x.ConflictGroup == candidate.ConflictGroup) ? "conflict_group" : null;
    }
    private static FundingBreakdown Funding(FundingPolicy policy, long amount)
    {
        long platform = amount / 10000 * policy.PlatformShareBasisPoints + amount % 10000 * policy.PlatformShareBasisPoints / 10000;
        return new(platform, amount - platform);
    }
    private static long SaturatingAdd(long left, long right) => left > long.MaxValue - right ? long.MaxValue : left + right;
    private sealed record Candidate(Promotion Promotion, long Discount);
    private sealed record TypedLine(ProductId ProductId, CategoryId? CategoryId, long SubtotalMinor);
}
