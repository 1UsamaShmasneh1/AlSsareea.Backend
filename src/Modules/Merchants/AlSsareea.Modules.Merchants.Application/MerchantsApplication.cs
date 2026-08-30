using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Merchants.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Merchants.Application;

public static class MerchantPermissions
{
    public const string View = "merchants.merchants.view";
    public const string Create = "merchants.merchants.create";
    public const string Update = "merchants.merchants.update";
    public const string LifecycleManage = "merchants.lifecycle.manage";
    public const string BranchesView = "merchants.branches.view";
    public const string BranchesManage = "merchants.branches.manage";
    public const string BusinessHoursManage = "merchants.business-hours.manage";
    public const string ServiceAreasManage = "merchants.service-areas.manage";
    public const string EmployeesView = "merchants.employees.view";
    public const string EmployeesManage = "merchants.employees.manage";
}

public sealed record MerchantActor(Guid UserId, bool IsPlatformOperator);

public enum MerchantOperationStatus { Success, Created, NotFound, Invalid, Conflict, Forbidden }
public sealed record MerchantOperationResult<T>(MerchantOperationStatus Status, T? Value = default, string? ErrorCode = null);
public static class MerchantOperation
{
    public static MerchantOperationResult<T> Success<T>(T value) => new(MerchantOperationStatus.Success, value);
    public static MerchantOperationResult<T> Created<T>(T value) => new(MerchantOperationStatus.Created, value);
    public static MerchantOperationResult<T> Failure<T>(MerchantOperationStatus status, string code) => new(status, default, code);
}

public interface IMerchantRepository
{
    Task<Merchant?> GetAsync(MerchantId id, CancellationToken cancellationToken = default);
    Task AddAsync(Merchant merchant, CancellationToken cancellationToken = default);
}

public interface IMerchantBranchRepository
{
    Task<MerchantBranch?> GetAsync(MerchantId merchantId, MerchantBranchId id, CancellationToken cancellationToken = default);
    Task AddAsync(MerchantBranch branch, CancellationToken cancellationToken = default);
}

public interface IMerchantEmployeeRepository
{
    Task<MerchantEmployee?> GetAsync(MerchantId merchantId, MerchantEmployeeId id, CancellationToken cancellationToken = default);
    Task<MerchantEmployee?> GetByUserAsync(MerchantId merchantId, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(MerchantEmployee employee, CancellationToken cancellationToken = default);
}

public interface IMerchantsService
{
    Task<MerchantOperationResult<MerchantResponse>> CreateMerchantAsync(CreateMerchantRequest request, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantResponse>> GetMerchantAsync(Guid merchantId, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantListResponse>> ListMerchantsAsync(int page, int pageSize, string? search, short? status, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantResponse>> UpdateMerchantAsync(Guid merchantId, UpdateMerchantRequest request, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantResponse>> ActivateMerchantAsync(Guid merchantId, Guid concurrencyStamp, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantResponse>> SuspendMerchantAsync(Guid merchantId, ReasonRequest request, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantResponse>> RejectMerchantAsync(Guid merchantId, ReasonRequest request, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantResponse>> CloseMerchantAsync(Guid merchantId, ReasonRequest request, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantResponse>> ChangeOwnerAsync(Guid merchantId, ChangeMerchantOwnerRequest request, MerchantActor actor, CancellationToken cancellationToken);

    Task<MerchantOperationResult<MerchantBranchResponse>> CreateBranchAsync(Guid merchantId, CreateMerchantBranchRequest request, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantBranchResponse>> GetBranchAsync(Guid merchantId, Guid branchId, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<IReadOnlyList<MerchantBranchResponse>>> ListBranchesAsync(Guid merchantId, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantBranchResponse>> UpdateBranchAsync(Guid merchantId, Guid branchId, UpdateMerchantBranchRequest request, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantBranchResponse>> UpdateBranchLocationAsync(Guid merchantId, Guid branchId, UpdateMerchantBranchLocationRequest request, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantBranchResponse>> ChangeBranchStatusAsync(Guid merchantId, Guid branchId, string operation, BranchStatusRequest request, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantBranchResponse>> SetPrimaryBranchAsync(Guid merchantId, Guid branchId, SetPrimaryBranchRequest request, MerchantActor actor, CancellationToken cancellationToken);

    Task<MerchantOperationResult<BusinessHoursResponse>> GetBusinessHoursAsync(Guid merchantId, Guid branchId, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<BusinessHoursResponse>> ReplaceBusinessHoursAsync(Guid merchantId, Guid branchId, ReplaceBusinessHoursRequest request, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<ScheduleOverrideResponse>> AddClosureAsync(Guid merchantId, Guid branchId, AddExceptionalClosureRequest request, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<ScheduleOverrideResponse>> SetSpecialHoursAsync(Guid merchantId, Guid branchId, SetSpecialHoursRequest request, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<IReadOnlyList<ScheduleOverrideResponse>>> ListScheduleOverridesAsync(Guid merchantId, Guid branchId, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<bool>> CancelScheduleOverrideAsync(Guid merchantId, Guid branchId, Guid overrideId, CancelScheduleOverrideRequest request, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<BranchAvailabilityResponse>> GetAvailabilityAsync(Guid merchantId, Guid branchId, DateTime atUtc, MerchantActor actor, CancellationToken cancellationToken);

    Task<MerchantOperationResult<BranchServiceAreaResponse>> AssignServiceAreaAsync(Guid merchantId, Guid branchId, Guid serviceAreaId, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<bool>> RemoveServiceAreaAsync(Guid merchantId, Guid branchId, Guid serviceAreaId, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<IReadOnlyList<BranchServiceAreaResponse>>> ListServiceAreasAsync(Guid merchantId, Guid branchId, MerchantActor actor, CancellationToken cancellationToken);

    Task<MerchantOperationResult<MerchantEmployeeResponse>> AddEmployeeAsync(Guid merchantId, AddMerchantEmployeeRequest request, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantEmployeeResponse>> GetEmployeeAsync(Guid merchantId, Guid employeeId, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<IReadOnlyList<MerchantEmployeeResponse>>> ListEmployeesAsync(Guid merchantId, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantEmployeeResponse>> ChangeEmployeeStatusAsync(Guid merchantId, Guid employeeId, string operation, MerchantEmployeeActionRequest request, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantEmployeeResponse>> ChangeEmployeeRoleAsync(Guid merchantId, Guid employeeId, ChangeMerchantEmployeeRoleRequest request, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantEmployeeResponse>> AssignEmployeeBranchAsync(Guid merchantId, Guid employeeId, AssignMerchantEmployeeBranchRequest request, MerchantActor actor, CancellationToken cancellationToken);
    Task<MerchantOperationResult<MerchantEmployeeResponse>> RemoveEmployeeBranchRestrictionAsync(Guid merchantId, Guid employeeId, MerchantEmployeeActionRequest request, MerchantActor actor, CancellationToken cancellationToken);
}

public interface ICustomerMerchantQueryService
{
    Task<MerchantOperationResult<CustomerMerchantListResponse>> DiscoverAsync(
        int page,
        int pageSize,
        string? query,
        bool? openNow,
        CancellationToken cancellationToken);

    Task<MerchantOperationResult<CustomerMerchantDetails>> GetDetailsAsync(
        Guid merchantId,
        CancellationToken cancellationToken);
}

public static class DependencyInjection
{
    public static IServiceCollection AddMerchantsApplication(this IServiceCollection services) => services;
}
