using AlSsareea.Modules.Pricing.Contracts;

namespace AlSsareea.Modules.Promotions.Contracts;

public sealed record LocalizedTextRequest(string Arabic, string? Hebrew, string English);
public sealed record FundingRequest(short Source, int PlatformShareBasisPoints, int MerchantShareBasisPoints);
public sealed record UsageLimitsRequest(long? GlobalLimit, long? PerCustomerLimit, long? BudgetLimitMinor, int? MaximumRedemptionsPerOrder);
public sealed record EligibilityRequest(long? MinimumSubtotalMinor, Guid? CustomerId, bool FirstOrderOnly);
public sealed record ScopeRequest(short Type, IReadOnlyList<Guid>? TargetIds, Guid? MerchantId = null);
public sealed record BenefitRequest(short Kind, string Currency, long Value, long? MaximumDiscountMinor);

public sealed record CreatePromotionRequest(
    string InternalName,
    LocalizedTextRequest DisplayName,
    LocalizedTextRequest? Description,
    short Type,
    int Priority,
    short Stackability,
    string? ConflictGroup,
    FundingRequest Funding,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    UsageLimitsRequest UsageLimits,
    EligibilityRequest Eligibility,
    ScopeRequest Scope,
    BenefitRequest Benefit,
    string? CouponCode);

public sealed record UpdatePromotionRequest(
    Guid Id,
    string InternalName,
    LocalizedTextRequest DisplayName,
    LocalizedTextRequest? Description,
    int Priority,
    short Stackability,
    string? ConflictGroup,
    FundingRequest Funding,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    UsageLimitsRequest UsageLimits,
    EligibilityRequest Eligibility,
    ScopeRequest Scope,
    BenefitRequest Benefit,
    string? CouponCode,
    Guid ConcurrencyStamp);

public sealed record PromotionActionRequest(Guid ConcurrencyStamp);

public sealed record PromotionResponse(
    Guid Id,
    string InternalName,
    LocalizedTextRequest DisplayName,
    LocalizedTextRequest? Description,
    short Type,
    short Status,
    int Priority,
    short Stackability,
    string? ConflictGroup,
    FundingRequest Funding,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    UsageLimitsRequest UsageLimits,
    EligibilityRequest Eligibility,
    ScopeRequest Scope,
    BenefitRequest Benefit,
    string? NormalizedCouponCode,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid ConcurrencyStamp);

public sealed record PromotionListResponse(IReadOnlyList<PromotionResponse> Items, int Page, int PageSize, long Total);

public sealed record PromotionLineContext(Guid ProductId, Guid? CategoryId, long SubtotalMinor);
public sealed record UsageContext(long GlobalRedemptions, long CustomerRedemptions, long DiscountSpentMinor, bool IsFirstOrder);
public sealed record EvaluatePromotionsRequest(
    Guid? CustomerId,
    Guid MerchantId,
    Guid? BranchId,
    PricingBreakdownDto Pricing,
    PricingSnapshotDto? PricingSnapshot,
    IReadOnlyList<PromotionLineContext> Lines,
    string? CouponCode,
    UsageContext Usage);

public sealed record FundingBreakdown(long PlatformAmountMinor, long MerchantAmountMinor);
public sealed record PromotionEvaluationSnapshot(
    Guid PromotionId,
    Guid PromotionVersion,
    short PromotionType,
    string? NormalizedCouponCode,
    long DiscountAmountMinor,
    string Currency,
    FundingBreakdown Funding,
    short AppliedScope,
    string ReasonCode,
    DateTime EvaluatedAtUtc,
    Guid? PricingPolicyId,
    int? PricingPolicyVersion);
public sealed record PromotionDecision(Guid PromotionId, bool Applied, string ReasonCode, long DiscountAmountMinor, string? ConflictDecision);
public sealed record PromotionEvaluationResponse(
    long OriginalSubtotalMinor,
    long OriginalDeliveryFeeMinor,
    long ProductDiscountMinor,
    long FreeDeliveryAmountMinor,
    long TotalAdjustmentMinor,
    long FinalSubtotalMinor,
    long FinalDeliveryFeeMinor,
    string Currency,
    DateTime EvaluatedAtUtc,
    IReadOnlyList<PromotionDecision> Decisions,
    IReadOnlyList<PromotionEvaluationSnapshot> Snapshots);

public sealed record ValidateCouponRequest(
    string CouponCode,
    Guid? CustomerId,
    Guid MerchantId,
    Guid? BranchId,
    PricingBreakdownDto Pricing,
    PricingSnapshotDto? PricingSnapshot,
    IReadOnlyList<PromotionLineContext> Lines,
    UsageContext Usage);
public sealed record CouponValidationResponse(bool IsValid, string? ErrorCode, PromotionEvaluationSnapshot? Snapshot);

public sealed record RecordRedemptionRequest(
    Guid PromotionId,
    Guid? CustomerId,
    string ExternalReference,
    long DiscountAmountMinor,
    string Currency);
public sealed record PromotionUsageResponse(Guid PromotionId, long GlobalRedemptions, long DiscountSpentMinor, long CustomerRedemptions);
