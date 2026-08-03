using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Promotions.Application;
using AlSsareea.Modules.Promotions.Contracts;
using AlSsareea.Modules.Promotions.Domain;
using AlSsareea.Modules.Promotions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AlSsareea.Modules.Promotions.Infrastructure;

internal sealed class PromotionsService(
    PromotionsDbContext db,
    IPromotionRepository repository,
    IPromotionScopeAuthorizer scopeAuthorizer,
    IClock clock) : IPromotionsService, ICartPromotionEvaluator
{
    public async Task<PromotionEvaluationResponse?> EvaluateCartAsync(EvaluatePromotionsRequest request, CancellationToken cancellationToken = default)
    {
        PromotionOperationResult<PromotionEvaluationResponse> result = await EvaluateAsync(request, new PromotionActor(Guid.Empty, true, new HashSet<Guid>()), cancellationToken);
        return result.Value;
    }

    public async Task<CouponValidationResponse?> ValidateCartCouponAsync(ValidateCouponRequest request, CancellationToken cancellationToken = default)
    {
        PromotionOperationResult<CouponValidationResponse> result = await ValidateCouponAsync(request, new PromotionActor(Guid.Empty, true, new HashSet<Guid>()), cancellationToken);
        return result.Value;
    }
    public async Task<PromotionOperationResult<PromotionResponse>> CreateAsync(CreatePromotionRequest request, PromotionActor actor, CancellationToken ct)
    {
        try
        {
            Promotion promotion = Create(request, clock.UtcNow);
            if (!await CanManage(promotion, actor, ct)) return Forbidden<PromotionResponse>();
            await repository.AddAsync(promotion, ct);
            Audit(promotion, actor, "promotion.created");
            await db.SaveChangesAsync(ct);
            return PromotionOperation.Created(ToResponse(promotion));
        }
        catch (DbUpdateException exception) when (IsUnique(exception)) { return Conflict<PromotionResponse>(PromotionErrorCodes.CouponAlreadyExists); }
        catch (DomainException) { return Invalid<PromotionResponse>(); }
    }

    public async Task<PromotionOperationResult<PromotionResponse>> UpdateAsync(Guid id, UpdatePromotionRequest request, PromotionActor actor, CancellationToken ct)
    {
        if (id != request.Id) return Invalid<PromotionResponse>();
        return await WithPromotion(id, actor, async promotion =>
        {
            if (promotion.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<PromotionResponse>();
            try
            {
                promotion.Update(
                    request.InternalName, Text(request.DisplayName), OptionalText(request.Description), request.Priority,
                    (StackabilityPolicy)request.Stackability, request.ConflictGroup, Funding(request.Funding),
                    new ValidityPeriod(request.StartsAtUtc, request.EndsAtUtc), Limits(request.UsageLimits),
                    Eligibility(request.Eligibility), Scope(request.Scope), Benefit(request.Benefit), Coupon(request.CouponCode), clock.UtcNow);
                if (!await CanManage(promotion, actor, ct)) return Forbidden<PromotionResponse>();
                Audit(promotion, actor, "promotion.updated");
                await db.SaveChangesAsync(ct);
                return PromotionOperation.Success(ToResponse(promotion));
            }
            catch (DbUpdateConcurrencyException) { return Conflict<PromotionResponse>(); }
            catch (DbUpdateException exception) when (IsUnique(exception)) { return Conflict<PromotionResponse>(PromotionErrorCodes.CouponAlreadyExists); }
            catch (DomainException) { return Invalid<PromotionResponse>(); }
        }, ct);
    }

    public Task<PromotionOperationResult<PromotionResponse>> ActivateAsync(Guid id, PromotionActionRequest request, PromotionActor actor, CancellationToken ct) =>
        Lifecycle(id, request, actor, "promotion.activated", x => x.Activate(clock.UtcNow), ct);
    public Task<PromotionOperationResult<PromotionResponse>> SuspendAsync(Guid id, PromotionActionRequest request, PromotionActor actor, CancellationToken ct) =>
        Lifecycle(id, request, actor, "promotion.suspended", x => x.Suspend(clock.UtcNow), ct);
    public Task<PromotionOperationResult<PromotionResponse>> ArchiveAsync(Guid id, PromotionActionRequest request, PromotionActor actor, CancellationToken ct) =>
        Lifecycle(id, request, actor, "promotion.archived", x => x.Archive(clock.UtcNow), ct);

    public async Task<PromotionOperationResult<PromotionResponse>> GetAsync(Guid id, PromotionActor actor, CancellationToken ct) =>
        await WithPromotion(id, actor, promotion => Task.FromResult(PromotionOperation.Success(ToResponse(promotion))), ct);

    public async Task<PromotionOperationResult<PromotionListResponse>> ListAsync(
        int page, int pageSize, short? status, short? type, Guid? merchantId, Guid? branchId, string? couponCode, DateTime? validAtUtc,
        PromotionActor actor, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || status is not null && !Enum.IsDefined((PromotionStatus)status) ||
            type is not null && !Enum.IsDefined((PromotionType)type) || validAtUtc?.Kind is not (null or DateTimeKind.Utc))
            return Invalid<PromotionListResponse>();
        if (!actor.IsPlatformOperator && merchantId is null) return Forbidden<PromotionListResponse>();
        if (merchantId is not null && !actor.IsPlatformOperator &&
            !await scopeAuthorizer.CanManageAsync(new PromotionScope(PromotionScopeType.Merchant, [merchantId.Value]), actor, ct))
            return Forbidden<PromotionListResponse>();
        IQueryable<Promotion> query = db.Promotions.AsNoTracking();
        if (status is not null) query = query.Where(x => x.Status == (PromotionStatus)status);
        if (type is not null) query = query.Where(x => x.Type == (PromotionType)type);
        if (merchantId is not null) query = query.Where(x => x.Scope.MerchantId == merchantId.Value);
        if (branchId is not null) query = query.Where(x => x.Scope.Type == PromotionScopeType.Branch && x.Scope.TargetIds.Contains(branchId.Value));
        if (!string.IsNullOrWhiteSpace(couponCode))
        {
            string normalized;
            try { normalized = new CouponCode(couponCode).Value; } catch (DomainException) { return Invalid<PromotionListResponse>(); }
            query = query.Where(x => x.CouponCode != null && x.CouponCode.Value.Value == normalized);
        }
        if (validAtUtc is not null) query = query.Where(x => x.Validity.StartsAtUtc <= validAtUtc && x.Validity.EndsAtUtc > validAtUtc);
        long total = await query.LongCountAsync(ct);
        Promotion[] items = await query.OrderByDescending(x => x.Priority).ThenBy(x => x.Validity.StartsAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return PromotionOperation.Success(new PromotionListResponse(items.Select(ToResponse).ToArray(), page, pageSize, total));
    }

    public async Task<PromotionOperationResult<PromotionEvaluationResponse>> EvaluateAsync(EvaluatePromotionsRequest request, PromotionActor actor, CancellationToken ct)
    {
        try
        {
            if (!actor.IsPlatformOperator &&
                !await scopeAuthorizer.CanManageAsync(new PromotionScope(PromotionScopeType.Merchant, [request.MerchantId]), actor, ct))
                return Forbidden<PromotionEvaluationResponse>();
            DateTime now = clock.UtcNow;
            Promotion[] candidates = await db.Promotions.AsNoTracking()
                .Where(x => x.Status != PromotionStatus.Archived)
                .OrderByDescending(x => x.Priority).ThenBy(x => x.Id).ToArrayAsync(ct);
            return PromotionOperation.Success(PromotionEvaluator.Evaluate(candidates, request, now));
        }
        catch (DomainException) { return Invalid<PromotionEvaluationResponse>(); }
    }

    public async Task<PromotionOperationResult<CouponValidationResponse>> ValidateCouponAsync(ValidateCouponRequest request, PromotionActor actor, CancellationToken ct)
    {
        try
        {
            if (!actor.IsPlatformOperator &&
                !await scopeAuthorizer.CanManageAsync(new PromotionScope(PromotionScopeType.Merchant, [request.MerchantId]), actor, ct))
                return Forbidden<CouponValidationResponse>();
            string normalized = new CouponCode(request.CouponCode).Value;
            Promotion? promotion = await db.Promotions.AsNoTracking().SingleOrDefaultAsync(x => x.CouponCode != null && x.CouponCode.Value.Value == normalized, ct);
            if (promotion is null) return PromotionOperation.Success(new CouponValidationResponse(false, PromotionErrorCodes.CouponInvalid, null));
            var evaluationInput = new EvaluatePromotionsRequest(request.CustomerId, request.MerchantId, request.BranchId, request.Pricing, request.PricingSnapshot, request.Lines, normalized, request.Usage);
            PromotionEvaluationResponse evaluation = PromotionEvaluator.Evaluate([promotion], evaluationInput, clock.UtcNow);
            PromotionEvaluationSnapshot? snapshot = evaluation.Snapshots.SingleOrDefault();
            string? error = evaluation.Decisions.SingleOrDefault()?.ReasonCode;
            return PromotionOperation.Success(new CouponValidationResponse(snapshot is not null, snapshot is null ? error : null, snapshot));
        }
        catch (DomainException) { return Invalid<CouponValidationResponse>(PromotionErrorCodes.CouponInvalid); }
    }

    public async Task<PromotionOperationResult<PromotionUsageResponse>> RecordRedemptionAsync(RecordRedemptionRequest request, PromotionActor actor, CancellationToken ct)
    {
        try
        {
            var promotionId = new PromotionId(request.PromotionId);
            Promotion? promotion = await repository.GetAsync(promotionId, ct);
            if (promotion is null) return NotFound<PromotionUsageResponse>();
            if (!await CanManage(promotion, actor, ct)) return Forbidden<PromotionUsageResponse>();
            if (request.DiscountAmountMinor < 0) return Invalid<PromotionUsageResponse>();
            DateTime now = clock.UtcNow;
            if (!promotion.IsApplicableAt(now)) return Invalid<PromotionUsageResponse>(PromotionErrorCodes.Inactive);
            if (promotion.Benefit.Currency != new Currency(request.Currency)) return Invalid<PromotionUsageResponse>(PromotionErrorCodes.CurrencyMismatch);
            PromotionUsageResponse usage = await Usage(promotionId, request.CustomerId, ct);
            if (promotion.UsageLimits.GlobalLimit <= usage.GlobalRedemptions) return Conflict<PromotionUsageResponse>(PromotionErrorCodes.UsageLimitReached);
            if (promotion.UsageLimits.PerCustomerLimit is not null && request.CustomerId is null) return Invalid<PromotionUsageResponse>(PromotionErrorCodes.NotEligible);
            if (promotion.UsageLimits.PerCustomerLimit <= usage.CustomerRedemptions) return Conflict<PromotionUsageResponse>(PromotionErrorCodes.CustomerLimitReached);
            if (promotion.UsageLimits.BudgetLimitMinor is not null &&
                (usage.DiscountSpentMinor > promotion.UsageLimits.BudgetLimitMinor.Value - request.DiscountAmountMinor))
                return Conflict<PromotionUsageResponse>(PromotionErrorCodes.UsageLimitReached);
            var redemption = PromotionRedemption.Create(PromotionRedemptionId.New(), promotionId, request.CustomerId, request.ExternalReference, request.DiscountAmountMinor, new Currency(request.Currency), clock.UtcNow);
            db.Redemptions.Add(redemption);
            Audit(promotion, actor, "promotion.redemption_recorded");
            await db.SaveChangesAsync(ct);
            return PromotionOperation.Created(await Usage(promotionId, request.CustomerId, ct));
        }
        catch (DbUpdateException exception) when (IsUnique(exception)) { return Conflict<PromotionUsageResponse>(PromotionErrorCodes.RedemptionAlreadyExists); }
        catch (DomainException) { return Invalid<PromotionUsageResponse>(); }
    }

    public async Task<PromotionOperationResult<PromotionUsageResponse>> GetUsageAsync(Guid promotionId, Guid? customerId, PromotionActor actor, CancellationToken ct)
    {
        Promotion? promotion;
        try { promotion = await repository.GetAsync(new PromotionId(promotionId), ct); } catch (DomainException) { return NotFound<PromotionUsageResponse>(); }
        if (promotion is null) return NotFound<PromotionUsageResponse>();
        if (!await CanManage(promotion, actor, ct)) return Forbidden<PromotionUsageResponse>();
        return PromotionOperation.Success(await Usage(promotion.Id, customerId, ct));
    }

    private async Task<PromotionOperationResult<PromotionResponse>> Lifecycle(Guid id, PromotionActionRequest request, PromotionActor actor, string action, Action<Promotion> transition, CancellationToken ct) =>
        await WithPromotion(id, actor, async promotion =>
        {
            if (promotion.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<PromotionResponse>();
            try
            {
                transition(promotion);
                Audit(promotion, actor, action);
                await db.SaveChangesAsync(ct);
                return PromotionOperation.Success(ToResponse(promotion));
            }
            catch (DbUpdateConcurrencyException) { return Conflict<PromotionResponse>(); }
            catch (DomainException) { return Invalid<PromotionResponse>(PromotionErrorCodes.InvalidState); }
        }, ct);

    private async Task<PromotionOperationResult<T>> WithPromotion<T>(Guid id, PromotionActor actor, Func<Promotion, Task<PromotionOperationResult<T>>> operation, CancellationToken ct)
    {
        Promotion? promotion;
        try { promotion = await repository.GetAsync(new PromotionId(id), ct); } catch (DomainException) { return NotFound<T>(); }
        if (promotion is null) return NotFound<T>();
        if (!await CanManage(promotion, actor, ct)) return NotFound<T>();
        return await operation(promotion);
    }

    private async Task<bool> CanManage(Promotion promotion, PromotionActor actor, CancellationToken ct)
    {
        return await scopeAuthorizer.CanManageAsync(promotion.Scope, actor, ct);
    }

    private async Task<PromotionUsageResponse> Usage(PromotionId id, Guid? customerId, CancellationToken ct)
    {
        IQueryable<PromotionRedemption> query = db.Redemptions.AsNoTracking().Where(x => x.PromotionId == id);
        long count = await query.LongCountAsync(ct);
        long amount = await query.SumAsync(x => x.DiscountAmountMinor, ct);
        long customerCount = customerId is null ? 0 : await query.LongCountAsync(x => x.CustomerId == customerId, ct);
        return new(id.Value, count, amount, customerCount);
    }

    private void Audit(Promotion promotion, PromotionActor actor, string action) =>
        db.Audits.Add(PromotionAudit.Create(PromotionAuditId.New(), promotion.Id, actor.UserId, action, clock.UtcNow));
    private static bool IsUnique(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    private static PromotionOperationResult<T> NotFound<T>() => PromotionOperation.Failure<T>(PromotionOperationStatus.NotFound, PromotionErrorCodes.NotFound);
    private static PromotionOperationResult<T> Forbidden<T>() => PromotionOperation.Failure<T>(PromotionOperationStatus.Forbidden, PromotionErrorCodes.UnauthorizedScope);
    private static PromotionOperationResult<T> Conflict<T>(string code = PromotionErrorCodes.ConcurrencyConflict) => PromotionOperation.Failure<T>(PromotionOperationStatus.Conflict, code);
    private static PromotionOperationResult<T> Invalid<T>(string code = PromotionErrorCodes.Invalid) => PromotionOperation.Failure<T>(PromotionOperationStatus.Invalid, code);

    private static Promotion Create(CreatePromotionRequest r, DateTime now) => Promotion.Create(
        PromotionId.New(), r.InternalName, Text(r.DisplayName), OptionalText(r.Description), (PromotionType)r.Type, r.Priority,
        (StackabilityPolicy)r.Stackability, r.ConflictGroup, Funding(r.Funding), new ValidityPeriod(r.StartsAtUtc, r.EndsAtUtc),
        Limits(r.UsageLimits), Eligibility(r.Eligibility), Scope(r.Scope), Benefit(r.Benefit), Coupon(r.CouponCode), now);
    private static LocalizedText Text(LocalizedTextRequest r) => new(r.Arabic, r.Hebrew, r.English);
    private static LocalizedText? OptionalText(LocalizedTextRequest? r) => r is null ? null : Text(r);
    private static FundingPolicy Funding(FundingRequest r) => new((FundingSource)r.Source, r.PlatformShareBasisPoints, r.MerchantShareBasisPoints);
    private static UsageLimits Limits(UsageLimitsRequest r) => new(r.GlobalLimit, r.PerCustomerLimit, r.BudgetLimitMinor, r.MaximumRedemptionsPerOrder);
    private static EligibilityRules Eligibility(EligibilityRequest r) => new(r.MinimumSubtotalMinor, r.CustomerId, r.FirstOrderOnly);
    private static PromotionScope Scope(ScopeRequest r) => new((PromotionScopeType)r.Type, r.TargetIds, r.MerchantId);
    private static DiscountBenefit Benefit(BenefitRequest r) => new((DiscountKind)r.Kind, new Currency(r.Currency), r.Value, r.MaximumDiscountMinor);
    private static CouponCode? Coupon(string? value) => string.IsNullOrWhiteSpace(value) ? null : new CouponCode(value);
    private static PromotionResponse ToResponse(Promotion x) => new(
        x.Id.Value, x.InternalName, ResponseText(x.DisplayName), OptionalResponseText(x.Description), (short)x.Type, (short)x.Status, x.Priority,
        (short)x.Stackability, x.ConflictGroup, new FundingRequest((short)x.Funding.Source, x.Funding.PlatformShareBasisPoints, x.Funding.MerchantShareBasisPoints),
        x.Validity.StartsAtUtc, x.Validity.EndsAtUtc,
        new UsageLimitsRequest(x.UsageLimits.GlobalLimit, x.UsageLimits.PerCustomerLimit, x.UsageLimits.BudgetLimitMinor, x.UsageLimits.MaximumRedemptionsPerOrder),
        new EligibilityRequest(x.Eligibility.MinimumSubtotalMinor, x.Eligibility.CustomerId, x.Eligibility.FirstOrderOnly),
        new ScopeRequest((short)x.Scope.Type, x.Scope.TargetIds, x.Scope.MerchantId),
        new BenefitRequest((short)x.Benefit.Kind, x.Benefit.Currency.Value, x.Benefit.Value, x.Benefit.MaximumDiscountMinor),
        x.CouponCode?.Value, x.CreatedAtUtc, x.UpdatedAtUtc, x.ConcurrencyStamp);
    private static LocalizedTextRequest ResponseText(LocalizedText x) => new(x.Arabic, x.Hebrew, x.English);
    private static LocalizedTextRequest? OptionalResponseText(LocalizedText? x) => x is null ? null : ResponseText(x);
}
