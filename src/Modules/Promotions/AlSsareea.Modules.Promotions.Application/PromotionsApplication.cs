using AlSsareea.Modules.Promotions.Contracts;
using AlSsareea.Modules.Promotions.Domain;

namespace AlSsareea.Modules.Promotions.Application;

public static class PromotionPermissions
{
    public const string View = "promotions.promotions.view";
    public const string Create = "promotions.promotions.create";
    public const string Update = "promotions.promotions.update";
    public const string Activate = "promotions.promotions.activate";
    public const string Suspend = "promotions.promotions.suspend";
    public const string Archive = "promotions.promotions.archive";
    public const string Evaluate = "promotions.promotions.evaluate";
    public const string ViewUsage = "promotions.usage.view";
    public const string RecordUsage = "promotions.usage.record";
}

public static class PromotionErrorCodes
{
    public const string NotFound = "promotions.promotion_not_found";
    public const string Inactive = "promotions.promotion_inactive";
    public const string Expired = "promotions.promotion_expired";
    public const string NotStarted = "promotions.promotion_not_started";
    public const string InvalidState = "promotions.promotion_invalid_state";
    public const string CurrencyMismatch = "promotions.currency_mismatch";
    public const string NotEligible = "promotions.not_eligible";
    public const string UsageLimitReached = "promotions.usage_limit_reached";
    public const string CustomerLimitReached = "promotions.customer_limit_reached";
    public const string CouponRequired = "promotions.coupon_required";
    public const string CouponInvalid = "promotions.coupon_invalid";
    public const string CouponExpired = "promotions.coupon_expired";
    public const string CouponAlreadyExists = "promotions.coupon_already_exists";
    public const string CouponNotApplicable = "promotions.coupon_not_applicable";
    public const string Conflict = "promotions.conflict";
    public const string ConcurrencyConflict = "promotions.concurrency_conflict";
    public const string UnauthorizedScope = "promotions.unauthorized_scope";
    public const string Invalid = "promotions.invalid_request";
    public const string RedemptionAlreadyExists = "promotions.redemption_already_exists";
}

public sealed record PromotionActor(Guid UserId, bool IsPlatformOperator, IReadOnlySet<Guid> MerchantIds);
public enum PromotionOperationStatus { Success, Created, NotFound, Invalid, Conflict, Forbidden }
public sealed record PromotionOperationResult<T>(PromotionOperationStatus Status, T? Value = default, string? ErrorCode = null);
public static class PromotionOperation
{
    public static PromotionOperationResult<T> Success<T>(T value) => new(PromotionOperationStatus.Success, value);
    public static PromotionOperationResult<T> Created<T>(T value) => new(PromotionOperationStatus.Created, value);
    public static PromotionOperationResult<T> Failure<T>(PromotionOperationStatus status, string code) => new(status, default, code);
}

public interface IPromotionRepository
{
    Task<Promotion?> GetAsync(PromotionId id, CancellationToken cancellationToken = default);
    Task AddAsync(Promotion promotion, CancellationToken cancellationToken = default);
}

public interface IPromotionScopeAuthorizer
{
    Task<bool> CanManageAsync(PromotionScope scope, PromotionActor actor, CancellationToken cancellationToken);
}

public interface IPromotionsService
{
    Task<PromotionOperationResult<PromotionResponse>> CreateAsync(CreatePromotionRequest request, PromotionActor actor, CancellationToken cancellationToken);
    Task<PromotionOperationResult<PromotionResponse>> UpdateAsync(Guid id, UpdatePromotionRequest request, PromotionActor actor, CancellationToken cancellationToken);
    Task<PromotionOperationResult<PromotionResponse>> ActivateAsync(Guid id, PromotionActionRequest request, PromotionActor actor, CancellationToken cancellationToken);
    Task<PromotionOperationResult<PromotionResponse>> SuspendAsync(Guid id, PromotionActionRequest request, PromotionActor actor, CancellationToken cancellationToken);
    Task<PromotionOperationResult<PromotionResponse>> ArchiveAsync(Guid id, PromotionActionRequest request, PromotionActor actor, CancellationToken cancellationToken);
    Task<PromotionOperationResult<PromotionResponse>> GetAsync(Guid id, PromotionActor actor, CancellationToken cancellationToken);
    Task<PromotionOperationResult<PromotionListResponse>> ListAsync(int page, int pageSize, short? status, short? type, Guid? merchantId, Guid? branchId, string? couponCode, DateTime? validAtUtc, PromotionActor actor, CancellationToken cancellationToken);
    Task<PromotionOperationResult<PromotionEvaluationResponse>> EvaluateAsync(EvaluatePromotionsRequest request, PromotionActor actor, CancellationToken cancellationToken);
    Task<PromotionOperationResult<CouponValidationResponse>> ValidateCouponAsync(ValidateCouponRequest request, PromotionActor actor, CancellationToken cancellationToken);
    Task<PromotionOperationResult<PromotionUsageResponse>> RecordRedemptionAsync(RecordRedemptionRequest request, PromotionActor actor, CancellationToken cancellationToken);
    Task<PromotionOperationResult<PromotionUsageResponse>> GetUsageAsync(Guid promotionId, Guid? customerId, PromotionActor actor, CancellationToken cancellationToken);
}
