namespace AlSsareea.Modules.Merchants.Contracts;

public sealed record CreateMerchantRequest(string LegalName, string DisplayName, string? Description, string? RegistrationNumber, string? TaxNumber, string Email, string PhoneNumber, Guid OwnerUserId);
public sealed record UpdateMerchantRequest(string LegalName, string DisplayName, string? Description, string? RegistrationNumber, string? TaxNumber, string Email, string PhoneNumber, Guid ConcurrencyStamp);
public sealed record ReasonRequest(string Reason, Guid ConcurrencyStamp);
public sealed record ChangeMerchantOwnerRequest(Guid OwnerUserId, Guid ConcurrencyStamp);
public sealed record MerchantResponse(Guid Id, string LegalName, string DisplayName, string? Description, string? RegistrationNumber, string? TaxNumber, string Email, string PhoneNumber, Guid OwnerUserId, short Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? ActivatedAtUtc, DateTime? SuspendedAtUtc, string? SuspensionReason, DateTime? RejectedAtUtc, string? RejectionReason, DateTime? ClosedAtUtc, string? ClosingReason, Guid ConcurrencyStamp);
public sealed record MerchantSummaryResponse(Guid Id, string DisplayName, short Status, Guid OwnerUserId, DateTime CreatedAtUtc);
public sealed record MerchantListResponse(IReadOnlyList<MerchantSummaryResponse> Items, int Page, int PageSize, int TotalCount);

public sealed record BranchAddressRequest(string City, string? Area, string Street, string? BuildingNumber, string? PostalCode);
public sealed record CoordinateRequest(double Latitude, double Longitude);
public sealed record CreateMerchantBranchRequest(string Name, string? Code, string PhoneNumber, string? Email, BranchAddressRequest Address, CoordinateRequest Location, string TimeZone, bool IsPrimary);
public sealed record UpdateMerchantBranchRequest(string Name, string? Code, string PhoneNumber, string? Email, BranchAddressRequest Address, string TimeZone, Guid ConcurrencyStamp);
public sealed record UpdateMerchantBranchLocationRequest(double Latitude, double Longitude, Guid ConcurrencyStamp);
public sealed record BranchStatusRequest(string? Reason, Guid ConcurrencyStamp);
public sealed record SetPrimaryBranchRequest(Guid ConcurrencyStamp);
public sealed record MerchantBranchResponse(Guid Id, Guid MerchantId, string Name, string? Code, string PhoneNumber, string? Email, BranchAddressResponse Address, CoordinateResponse Location, short Status, bool IsPrimary, string TimeZone, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, Guid ConcurrencyStamp);
public sealed record BranchAddressResponse(string City, string? Area, string Street, string? BuildingNumber, string? PostalCode);
public sealed record CoordinateResponse(double Latitude, double Longitude);

public sealed record OpeningPeriodRequest(TimeOnly OpensAt, TimeOnly ClosesAt);
public sealed record BusinessDayRequest(DayOfWeek DayOfWeek, bool ClosedAllDay, IReadOnlyList<OpeningPeriodRequest> Periods);
public sealed record ReplaceBusinessHoursRequest(IReadOnlyList<BusinessDayRequest> Days, Guid ConcurrencyStamp);
public sealed record OpeningPeriodResponse(Guid Id, TimeOnly OpensAt, TimeOnly ClosesAt);
public sealed record BusinessDayResponse(DayOfWeek DayOfWeek, bool ClosedAllDay, IReadOnlyList<OpeningPeriodResponse> Periods);
public sealed record BusinessHoursResponse(Guid BranchId, IReadOnlyList<BusinessDayResponse> Days);
public sealed record AddExceptionalClosureRequest(DateOnly StartDate, DateOnly EndDate, string? Reason, Guid ConcurrencyStamp);
public sealed record SetSpecialHoursRequest(DateOnly Date, IReadOnlyList<OpeningPeriodRequest> Periods, string? Reason, Guid ConcurrencyStamp);
public sealed record CancelScheduleOverrideRequest(Guid ConcurrencyStamp);
public sealed record ScheduleOverrideResponse(Guid Id, DateOnly StartDate, DateOnly EndDate, bool IsClosed, string? Reason, DateTime CreatedAtUtc, DateTime? CancelledAtUtc, IReadOnlyList<OpeningPeriodResponse> Periods);
public sealed record BranchAvailabilityResponse(Guid BranchId, DateTime AtUtc, bool IsOpen, DateOnly LocalDate, string Source);

public sealed record BranchServiceAreaResponse(Guid ServiceAreaId, string Name, bool IsActive, DateTime AssignedAtUtc);

public sealed record AddMerchantEmployeeRequest(Guid UserId, Guid? BranchId, short Role, bool Invited);
public sealed record ChangeMerchantEmployeeRoleRequest(short Role, Guid ConcurrencyStamp);
public sealed record AssignMerchantEmployeeBranchRequest(Guid BranchId, Guid ConcurrencyStamp);
public sealed record MerchantEmployeeActionRequest(Guid ConcurrencyStamp);
public sealed record MerchantEmployeeResponse(Guid Id, Guid MerchantId, Guid UserId, Guid? BranchId, short Role, short Status, DateTime? JoinedAtUtc, DateTime? SuspendedAtUtc, DateTime? RemovedAtUtc, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, Guid ConcurrencyStamp);

public sealed record MerchantCatalogScope(
    Guid MerchantId,
    bool MerchantIsActive,
    bool CanManageMerchant,
    Guid? RestrictedBranchId);

public interface IMerchantCatalogScopeProvider
{
    Task<MerchantCatalogScope?> GetScopeAsync(
        Guid merchantId,
        Guid userId,
        bool isPlatformOperator,
        CancellationToken cancellationToken = default);

    Task<bool> IsOperationalBranchAsync(
        Guid merchantId,
        Guid branchId,
        CancellationToken cancellationToken = default);

}

public sealed record OrderMerchantSnapshotContract(Guid MerchantId, Guid? BranchId, string MerchantDisplayName, string? BranchDisplayName, string? BranchAddress, string? BranchPhoneNumber);
public interface IOrderMerchantSnapshotProvider
{
    Task<OrderMerchantSnapshotContract?> GetAsync(Guid merchantId, Guid? branchId, CancellationToken cancellationToken = default);
}

public sealed record MerchantOrderOperationsScope(
    Guid MerchantId,
    bool MerchantIsActive,
    Guid? RestrictedBranchId,
    bool IsOwner);

public interface IMerchantOrderOperationsScopeProvider
{
    Task<IReadOnlyList<MerchantOrderOperationsScope>> GetScopesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<MerchantOrderOperationsScope?> GetScopeAsync(
        Guid merchantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> IsOperationalBranchAsync(
        Guid merchantId,
        Guid branchId,
        CancellationToken cancellationToken = default);

    Task<bool> IsBranchInMerchantAsync(
        Guid merchantId,
        Guid branchId,
        CancellationToken cancellationToken = default);
}
