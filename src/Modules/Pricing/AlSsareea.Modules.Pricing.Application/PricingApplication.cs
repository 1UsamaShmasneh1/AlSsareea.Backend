using AlSsareea.Modules.Pricing.Contracts;
using AlSsareea.Modules.Pricing.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Pricing.Application;

public static class PricingPermissions
{
    public const string View = "pricing.view";
    public const string Manage = "pricing.manage";
    public const string Calculate = "pricing.calculate";
}

public sealed record PricingActor(Guid UserId, bool IsPlatformOperator);
public enum PricingOperationStatus { Success, Created, NotFound, Invalid, Conflict, Forbidden }
public sealed record PricingOperationResult<T>(PricingOperationStatus Status, T? Value = default, string? ErrorCode = null);

public static class PricingOperation
{
    public static PricingOperationResult<T> Success<T>(T value) => new(PricingOperationStatus.Success, value);
    public static PricingOperationResult<T> Created<T>(T value) => new(PricingOperationStatus.Created, value);
    public static PricingOperationResult<T> Failure<T>(PricingOperationStatus status, string code) => new(status, default, code);
}

public interface IPricingPolicyRepository
{
    Task<PricingPolicy?> GetAsync(PricingPolicyId id, bool tracked = true, CancellationToken cancellationToken = default);
    Task AddAsync(PricingPolicy policy, CancellationToken cancellationToken = default);
}

public interface IPricingService
{
    Task<PricingOperationResult<PricingPolicyDto>> CreatePolicyAsync(CreatePricingPolicyRequest request, PricingActor actor, CancellationToken cancellationToken);
    Task<PricingOperationResult<PricingPolicyDto>> UpdatePolicyAsync(Guid policyId, UpdatePricingPolicyRequest request, PricingActor actor, CancellationToken cancellationToken);
    Task<PricingOperationResult<PricingPolicyDto>> ReplaceRulesAsync(Guid policyId, ReplacePricingRulesRequest request, PricingActor actor, CancellationToken cancellationToken);
    Task<PricingOperationResult<PricingPolicyDto>> ChangeStatusAsync(Guid policyId, string operation, PricingPolicyActionRequest request, PricingActor actor, CancellationToken cancellationToken);
    Task<PricingOperationResult<PricingPolicyDto>> GetPolicyAsync(Guid policyId, PricingActor actor, CancellationToken cancellationToken);
    Task<PricingOperationResult<PricingPolicyListDto>> ListPoliciesAsync(int page, int pageSize, short? status, Guid? merchantId, Guid? branchId, PricingActor actor, CancellationToken cancellationToken);
    Task<PricingOperationResult<PricingEstimateResponse>> EstimateAsync(PricingEstimateRequest request, PricingActor actor, CancellationToken cancellationToken);
}

public static class DependencyInjection
{
    public static IServiceCollection AddPricingApplication(this IServiceCollection services) => services;
}
