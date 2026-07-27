namespace AlSsareea.Modules.Pricing.Contracts;

public static class PricingErrorCodes
{
    public const string InvalidRequest = "pricing.invalid_request";
    public const string NotFound = "pricing.policy_not_found";
    public const string NoApplicablePolicy = "pricing.no_applicable_policy";
    public const string AmbiguousPolicy = "pricing.ambiguous_policy";
    public const string MinimumOrderNotMet = "pricing.minimum_order_not_met";
    public const string CurrencyMismatch = "pricing.currency_mismatch";
    public const string MaximumDistanceExceeded = "pricing.maximum_distance_exceeded";
    public const string ConcurrencyConflict = "pricing.concurrency_conflict";
    public const string Forbidden = "pricing.forbidden";
}

public sealed record PricingScopeDto(short Type, Guid? MerchantId, Guid? BranchId, Guid? ZoneId);

public sealed record PricingRuleRequest(
    Guid? Id,
    short Type,
    short Kind,
    short CalculationBase,
    int Priority,
    long AmountMinor,
    int PercentageBasisPoints,
    long? ThresholdMinor,
    long? MinimumMinor,
    long? MaximumMinor,
    int? IncludedDistanceMeters,
    int? MaximumDistanceMeters,
    long? AdditionalFeePerKilometerMinor);

public sealed record PricingRuleDto(
    Guid Id,
    short Type,
    short Kind,
    short CalculationBase,
    int Priority,
    long AmountMinor,
    int PercentageBasisPoints,
    long? ThresholdMinor,
    long? MinimumMinor,
    long? MaximumMinor,
    int? IncludedDistanceMeters,
    int? MaximumDistanceMeters,
    long? AdditionalFeePerKilometerMinor);

public sealed record CreatePricingPolicyRequest(
    string Name,
    PricingScopeDto Scope,
    string Currency,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveUntilUtc,
    int Priority);

public sealed record UpdatePricingPolicyRequest(
    string Name,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveUntilUtc,
    int Priority,
    Guid ConcurrencyStamp);

public sealed record ReplacePricingRulesRequest(IReadOnlyList<PricingRuleRequest> Rules, Guid ConcurrencyStamp);
public sealed record PricingPolicyActionRequest(Guid ConcurrencyStamp);

public sealed record PricingPolicyDto(
    Guid Id,
    string Name,
    PricingScopeDto Scope,
    string Currency,
    short Status,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveUntilUtc,
    int Priority,
    int Version,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ActivatedAtUtc,
    DateTime? DeactivatedAtUtc,
    DateTime? ArchivedAtUtc,
    Guid ConcurrencyStamp,
    IReadOnlyList<PricingRuleDto> Rules);

public sealed record PricingPolicyListDto(IReadOnlyList<PricingPolicyDto> Items, int Page, int PageSize, int TotalCount);

public sealed record PricingEstimateRequest(
    Guid MerchantId,
    Guid? BranchId,
    Guid? ZoneId,
    string Currency,
    long ItemsSubtotalMinor,
    int? DistanceMeters,
    DateTime? CalculationAtUtc);

public sealed record PricingBreakdownDto(
    string Currency,
    long ItemsSubtotalMinor,
    long DeliveryFeeMinor,
    long ServiceFeeMinor,
    long PlatformFeeMinor,
    long SmallOrderFeeMinor,
    long TaxMinor,
    long DiscountsMinor,
    long GrandTotalMinor);

public sealed record PricingSnapshotDto(
    Guid PolicyId,
    int PolicyVersion,
    PricingScopeDto EffectiveScope,
    PricingBreakdownDto Breakdown,
    IReadOnlyList<Guid> AppliedRuleIds,
    DateTime CalculatedAtUtc,
    int? DistanceMeters,
    Guid MerchantId,
    Guid? BranchId,
    Guid? ZoneId,
    long? MinimumOrderMinor,
    bool IsEligible,
    string? FailureCode);

public sealed record PricingEstimateResponse(
    PricingBreakdownDto Breakdown,
    PricingSnapshotDto Snapshot,
    bool IsEligible,
    string? FailureCode);

public interface IPricingCalculator
{
    Task<PricingEstimateResponse?> EstimateAsync(
        PricingEstimateRequest request,
        CancellationToken cancellationToken = default);
}
