using System.Data;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Pricing.Application;
using AlSsareea.Modules.Pricing.Contracts;
using AlSsareea.Modules.Pricing.Domain;
using AlSsareea.Modules.Pricing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Pricing.Infrastructure;

internal sealed class PricingService(
    PricingDbContext db,
    IPricingPolicyRepository repository,
    IMerchantCatalogScopeProvider merchants,
    IClock clock) : IPricingService, IPricingCalculator
{
    public async Task<PricingOperationResult<PricingPolicyDto>> CreatePolicyAsync(
        CreatePricingPolicyRequest request,
        PricingActor actor,
        CancellationToken cancellationToken) => await Run(async () =>
    {
        PricingScope scope = ToScope(request.Scope);
        if (!await CanManage(scope, actor, cancellationToken)) return Forbidden<PricingPolicyDto>();
        PricingPolicy policy = PricingPolicy.Create(
            PricingPolicyId.New(), request.Name, scope, request.Currency,
            request.EffectiveFromUtc, request.EffectiveUntilUtc, request.Priority, clock.UtcNow);
        await repository.AddAsync(policy, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return PricingOperation.Created(ToDto(policy));
    });

    public async Task<PricingOperationResult<PricingPolicyDto>> UpdatePolicyAsync(
        Guid policyId,
        UpdatePricingPolicyRequest request,
        PricingActor actor,
        CancellationToken cancellationToken) => await WithPolicy(policyId, actor, async policy =>
    {
        if (policy.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<PricingPolicyDto>();
        policy.UpdateDraft(request.Name, request.EffectiveFromUtc, request.EffectiveUntilUtc, request.Priority, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return PricingOperation.Success(ToDto(policy));
    }, cancellationToken);

    public async Task<PricingOperationResult<PricingPolicyDto>> ReplaceRulesAsync(
        Guid policyId,
        ReplacePricingRulesRequest request,
        PricingActor actor,
        CancellationToken cancellationToken) => await WithPolicy(policyId, actor, async policy =>
    {
        if (policy.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<PricingPolicyDto>();
        if (request.Rules.Count > 50) return Invalid<PricingPolicyDto>();
        policy.ReplaceRules(request.Rules.Select(ToRule), clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return PricingOperation.Success(ToDto(policy));
    }, cancellationToken);

    public async Task<PricingOperationResult<PricingPolicyDto>> ChangeStatusAsync(
        Guid policyId,
        string operation,
        PricingPolicyActionRequest request,
        PricingActor actor,
        CancellationToken cancellationToken) => await Run(async () =>
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        PricingPolicy? policy = await repository.GetAsync(new PricingPolicyId(policyId), true, cancellationToken);
        if (policy is null) return NotFound<PricingPolicyDto>();
        if (!await CanManage(policy.Scope, actor, cancellationToken)) return Forbidden<PricingPolicyDto>();
        if (policy.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<PricingPolicyDto>();
        DateTime now = clock.UtcNow;

        if (string.Equals(operation, "activate", StringComparison.OrdinalIgnoreCase))
        {
            bool overlap = await db.Policies.AnyAsync(x =>
                x.Id != policy.Id &&
                x.Status == PricingPolicyStatus.Active &&
                x.ScopeKey == policy.ScopeKey &&
                x.Currency == policy.Currency &&
                (!x.EffectiveUntilUtc.HasValue || x.EffectiveUntilUtc > policy.EffectiveFromUtc) &&
                (!policy.EffectiveUntilUtc.HasValue || x.EffectiveFromUtc < policy.EffectiveUntilUtc), cancellationToken);
            if (overlap) return Conflict<PricingPolicyDto>("pricing.active_policy_overlap");
            policy.Activate(now);
        }
        else if (string.Equals(operation, "deactivate", StringComparison.OrdinalIgnoreCase)) policy.Deactivate(now);
        else if (string.Equals(operation, "archive", StringComparison.OrdinalIgnoreCase)) policy.Archive(now);
        else return Invalid<PricingPolicyDto>();

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PricingOperation.Success(ToDto(policy));
    });

    public async Task<PricingOperationResult<PricingPolicyDto>> GetPolicyAsync(
        Guid policyId,
        PricingActor actor,
        CancellationToken cancellationToken) => await Run(async () =>
    {
        PricingPolicy? policy = await repository.GetAsync(new PricingPolicyId(policyId), false, cancellationToken);
        if (policy is null) return NotFound<PricingPolicyDto>();
        return await CanManage(policy.Scope, actor, cancellationToken)
            ? PricingOperation.Success(ToDto(policy))
            : Forbidden<PricingPolicyDto>();
    });

    public async Task<PricingOperationResult<PricingPolicyListDto>> ListPoliciesAsync(
        int page,
        int pageSize,
        short? status,
        Guid? merchantId,
        Guid? branchId,
        PricingActor actor,
        CancellationToken cancellationToken) => await Run(async () =>
    {
        if (page < 1 || pageSize is < 1 or > 100) return Invalid<PricingPolicyListDto>();
        if (status.HasValue && !Enum.IsDefined((PricingPolicyStatus)status.Value)) return Invalid<PricingPolicyListDto>();
        if (!actor.IsPlatformOperator)
        {
            if (!merchantId.HasValue) return Forbidden<PricingPolicyListDto>();
            MerchantCatalogScope? scope = await merchants.GetScopeAsync(merchantId.Value, actor.UserId, false, cancellationToken);
            if (scope?.CanManageMerchant != true) return Forbidden<PricingPolicyListDto>();
            if (scope.RestrictedBranchId.HasValue)
            {
                if (branchId != scope.RestrictedBranchId) return Forbidden<PricingPolicyListDto>();
            }
        }

        IQueryable<PricingPolicy> query = db.Policies.AsNoTracking().Include(x => x.Rules);
        if (status.HasValue) query = query.Where(x => x.Status == (PricingPolicyStatus)status.Value);
        if (merchantId.HasValue) query = query.Where(x => x.MerchantId == merchantId);
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId);
        int total = await query.CountAsync(cancellationToken);
        PricingPolicy[] values = await query.OrderByDescending(x => x.UpdatedAtUtc)
            .ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return PricingOperation.Success(new PricingPolicyListDto(values.Select(ToDto).ToArray(), page, pageSize, total));
    });

    public async Task<PricingOperationResult<PricingEstimateResponse>> EstimateAsync(
        PricingEstimateRequest request,
        PricingActor actor,
        CancellationToken cancellationToken) => await Run(async () =>
    {
        if (request.MerchantId == Guid.Empty || request.ItemsSubtotalMinor < 0 ||
            request.BranchId == Guid.Empty || request.ZoneId == Guid.Empty || request.DistanceMeters < 0)
            return Invalid<PricingEstimateResponse>();
        MerchantCatalogScope? merchantScope = await merchants.GetScopeAsync(
            request.MerchantId, actor.UserId, actor.IsPlatformOperator, cancellationToken);
        if (merchantScope is null || !merchantScope.MerchantIsActive) return Forbidden<PricingEstimateResponse>();
        if (!actor.IsPlatformOperator && !merchantScope.CanManageMerchant) return Forbidden<PricingEstimateResponse>();
        if (merchantScope.RestrictedBranchId.HasValue && merchantScope.RestrictedBranchId != request.BranchId)
            return Forbidden<PricingEstimateResponse>();
        if (request.BranchId.HasValue &&
            !await merchants.IsOperationalBranchAsync(request.MerchantId, request.BranchId.Value, cancellationToken))
            return Invalid<PricingEstimateResponse>();

        DateTime at = request.CalculationAtUtc ?? clock.UtcNow;
        if (at.Kind != DateTimeKind.Utc) return Invalid<PricingEstimateResponse>();
        string currency = request.Currency.Trim().ToUpperInvariant();
        PricingPolicy[] scopeCandidates = await db.Policies.AsNoTracking().Include(x => x.Rules)
            .Where(x => x.Status == PricingPolicyStatus.Active &&
                x.EffectiveFromUtc <= at &&
                (!x.EffectiveUntilUtc.HasValue || x.EffectiveUntilUtc > at) &&
                (x.ScopeType == PricingScopeType.Global ||
                 x.ScopeType == PricingScopeType.ServiceZone && x.ZoneId == request.ZoneId ||
                 x.ScopeType == PricingScopeType.Merchant && x.MerchantId == request.MerchantId ||
                 x.ScopeType == PricingScopeType.MerchantBranch && x.MerchantId == request.MerchantId && x.BranchId == request.BranchId))
            .ToArrayAsync(cancellationToken);
        if (scopeCandidates.Length == 0)
            return NotFound<PricingEstimateResponse>(PricingErrorCodes.NoApplicablePolicy);
        PricingPolicy[] candidates = [.. scopeCandidates.Where(x => x.Currency == currency)];
        if (candidates.Length == 0)
            return Invalid<PricingEstimateResponse>(PricingErrorCodes.CurrencyMismatch);

        PricingPolicy[] ordered = [.. candidates.OrderByDescending(x => x.Scope.Specificity).ThenByDescending(x => x.Priority).ThenBy(x => x.Id.Value)];
        if (ordered.Length > 1 && ordered[0].Scope.Specificity == ordered[1].Scope.Specificity && ordered[0].Priority == ordered[1].Priority)
            return Conflict<PricingEstimateResponse>(PricingErrorCodes.AmbiguousPolicy);

        PricingPolicy policy = ordered[0];
        PricingComputation computation;
        try { computation = PricingPolicyCalculator.Calculate(policy, request.ItemsSubtotalMinor, request.DistanceMeters); }
        catch (DomainException exception) when (exception.Message.Contains("Maximum delivery distance", StringComparison.Ordinal))
        {
            return Invalid<PricingEstimateResponse>(PricingErrorCodes.MaximumDistanceExceeded);
        }
        PricingBreakdownDto breakdown = new(
            policy.Currency, computation.ItemsSubtotalMinor, computation.DeliveryFeeMinor,
            computation.ServiceFeeMinor, computation.PlatformFeeMinor, computation.SmallOrderFeeMinor,
            computation.TaxMinor, computation.DiscountsMinor, computation.GrandTotalMinor);
        PricingScopeDto scopeDto = ToDto(policy.Scope);
        PricingSnapshotDto snapshot = new(
            policy.Id.Value, policy.Version, scopeDto, breakdown,
            computation.AppliedRuleIds.Select(x => x.Value).ToArray(), at, request.DistanceMeters,
            request.MerchantId, request.BranchId, request.ZoneId, computation.MinimumOrderMinor,
            computation.IsEligible, computation.FailureCode);
        return PricingOperation.Success(new PricingEstimateResponse(breakdown, snapshot, computation.IsEligible, computation.FailureCode));
    });

    async Task<PricingEstimateResponse?> IPricingCalculator.EstimateAsync(
        PricingEstimateRequest request,
        CancellationToken cancellationToken)
    {
        PricingOperationResult<PricingEstimateResponse> result = await EstimateAsync(
            request, new PricingActor(Guid.Empty, true), cancellationToken);
        return result.Value;
    }

    private async Task<PricingOperationResult<PricingPolicyDto>> WithPolicy(
        Guid policyId,
        PricingActor actor,
        Func<PricingPolicy, Task<PricingOperationResult<PricingPolicyDto>>> operation,
        CancellationToken cancellationToken) => await Run(async () =>
    {
        PricingPolicy? policy = await repository.GetAsync(new PricingPolicyId(policyId), true, cancellationToken);
        if (policy is null) return NotFound<PricingPolicyDto>();
        return await CanManage(policy.Scope, actor, cancellationToken)
            ? await operation(policy)
            : Forbidden<PricingPolicyDto>();
    });

    private async Task<bool> CanManage(PricingScope scope, PricingActor actor, CancellationToken cancellationToken)
    {
        if (scope.Type is PricingScopeType.Global or PricingScopeType.ServiceZone) return actor.IsPlatformOperator;
        MerchantCatalogScope? value = await merchants.GetScopeAsync(
            scope.MerchantId!.Value, actor.UserId, actor.IsPlatformOperator, cancellationToken);
        if (value?.CanManageMerchant != true) return false;
        return !value.RestrictedBranchId.HasValue ||
            scope.Type == PricingScopeType.MerchantBranch && value.RestrictedBranchId == scope.BranchId;
    }

    private static PricingScope ToScope(PricingScopeDto value)
    {
        if (!Enum.IsDefined((PricingScopeType)value.Type)) throw new DomainException("Pricing scope type is invalid.");
        return PricingScope.Create((PricingScopeType)value.Type, value.MerchantId, value.BranchId, value.ZoneId);
    }

    private static PricingRule ToRule(PricingRuleRequest value)
    {
        if (!Enum.IsDefined((PricingRuleType)value.Type) ||
            !Enum.IsDefined((PricingCalculationKind)value.Kind) ||
            !Enum.IsDefined((PricingCalculationBase)value.CalculationBase))
            throw new DomainException("Pricing rule enum value is invalid.");
        return PricingRule.Create(
            value.Id.HasValue ? new PricingRuleId(value.Id.Value) : PricingRuleId.New(),
            (PricingRuleType)value.Type, (PricingCalculationKind)value.Kind,
            (PricingCalculationBase)value.CalculationBase, value.Priority, value.AmountMinor,
            value.PercentageBasisPoints, value.ThresholdMinor, value.MinimumMinor, value.MaximumMinor,
            value.IncludedDistanceMeters, value.MaximumDistanceMeters, value.AdditionalFeePerKilometerMinor);
    }

    private static PricingPolicyDto ToDto(PricingPolicy value) => new(
        value.Id.Value, value.Name, ToDto(value.Scope), value.Currency, (short)value.Status,
        value.EffectiveFromUtc, value.EffectiveUntilUtc, value.Priority, value.Version,
        value.CreatedAtUtc, value.UpdatedAtUtc, value.ActivatedAtUtc, value.DeactivatedAtUtc,
        value.ArchivedAtUtc, value.ConcurrencyStamp, value.Rules.Select(ToDto).ToArray());

    private static PricingScopeDto ToDto(PricingScope value) =>
        new((short)value.Type, value.MerchantId, value.BranchId, value.ZoneId);

    private static PricingRuleDto ToDto(PricingRule value) => new(
        value.Id.Value, (short)value.Type, (short)value.Kind, (short)value.CalculationBase,
        value.Priority, value.AmountMinor, value.PercentageBasisPoints, value.ThresholdMinor,
        value.MinimumMinor, value.MaximumMinor, value.IncludedDistanceMeters,
        value.MaximumDistanceMeters, value.AdditionalFeePerKilometerMinor);

    private static async Task<PricingOperationResult<T>> Run<T>(Func<Task<PricingOperationResult<T>>> operation)
    {
        try { return await operation(); }
        catch (DomainException) { return Invalid<T>(); }
        catch (OverflowException) { return Invalid<T>("pricing.amount_overflow"); }
        catch (DbUpdateConcurrencyException) { return Conflict<T>(); }
        catch (DbUpdateException) { return Conflict<T>("pricing.database_constraint"); }
    }

    private static PricingOperationResult<T> Invalid<T>(string code = PricingErrorCodes.InvalidRequest) =>
        PricingOperation.Failure<T>(PricingOperationStatus.Invalid, code);
    private static PricingOperationResult<T> Conflict<T>(string code = PricingErrorCodes.ConcurrencyConflict) =>
        PricingOperation.Failure<T>(PricingOperationStatus.Conflict, code);
    private static PricingOperationResult<T> NotFound<T>(string code = PricingErrorCodes.NotFound) =>
        PricingOperation.Failure<T>(PricingOperationStatus.NotFound, code);
    private static PricingOperationResult<T> Forbidden<T>() =>
        PricingOperation.Failure<T>(PricingOperationStatus.Forbidden, PricingErrorCodes.Forbidden);
}
