using AlSsareea.BuildingBlocks.Contracts;

namespace AlSsareea.Modules.Drivers.Contracts;

public static class DriverPermissions
{
    public const string ProfileReadSelf = "drivers.profile.read.self";
    public const string ProfileManageSelf = "drivers.profile.manage.self";
    public const string ProfileRead = "drivers.profile.read";
    public const string ProfileManage = "drivers.profile.manage";
    public const string ReviewManage = "drivers.review.manage";
    public const string ActivationManage = "drivers.activation.manage";
    public const string VehiclesManageSelf = "drivers.vehicles.manage.self";
    public const string VehiclesManage = "drivers.vehicles.manage";
    public const string DocumentsManageSelf = "drivers.documents.manage.self";
    public const string DocumentsReview = "drivers.documents.review";
    public const string ZonesManage = "drivers.zones.manage";
    public const string AvailabilityManageSelf = "drivers.availability.manage.self";
    public const string ShiftsManage = "drivers.shifts.manage";
    public const string ShiftsRead = "drivers.shifts.read";
    public const string ShiftsManageSelf = "drivers.shifts.manage.self";
    public const string ShiftsReadSelf = "drivers.shifts.read.self";
    public const string ViolationsManage = "drivers.violations.manage";
    public const string SuspensionsManage = "drivers.suspensions.manage";
}

public sealed record CreateDriverRequest(string DisplayName, short EmploymentType, int MaximumConcurrentDeliveries, Guid? ProfilePhotoMediaId);
public sealed record UpdateDriverProfileRequest(string DisplayName, DateOnly? DateOfBirth, Guid? ProfilePhotoMediaId, Guid ConcurrencyStamp);
public sealed record RejectDriverRequest(string Reason, Guid ConcurrencyStamp);
public sealed record DriverTransitionRequest(Guid ConcurrencyStamp);
public sealed record AddVehicleRequest(short VehicleType, string? Make, string? Model, int? Year, string? Color, string? PlateNumber, string? RegistrationCountry, bool IsPrimary);
public sealed record VehicleReviewRequest(Guid ConcurrencyStamp, string? Reason = null);
public sealed record SubmitDriverDocumentRequest(short DocumentType, Guid MediaAssetId, DateTime? IssuedAtUtc, DateTime? ExpiresAtUtc);
public sealed record DocumentReviewRequest(Guid ConcurrencyStamp, string? Reason = null);
public sealed record AssignDriverZoneRequest(Guid ZoneId, bool IsPrimary);
public sealed record CreateDriverShiftRequest(DateTime ScheduledStartUtc, DateTime ScheduledEndUtc);
public sealed record RecordDriverViolationRequest(string ViolationType, short Severity, string Description, DateTime OccurredAtUtc);
public sealed record ResolveDriverViolationRequest(string ResolutionNotes);
public sealed record SuspendDriverRequest(string ReasonCode, string Reason, DateTime StartsAtUtc, DateTime? EndsAtUtc);
public sealed record LiftDriverSuspensionRequest(string Reason);

public sealed record VehicleResponse(Guid Id, short Type, string? Make, string? Model, int? Year, string? Color, string? PlateNumber, string? RegistrationCountry, bool IsPrimary, short Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? VerifiedAtUtc, Guid ConcurrencyStamp);
public sealed record DriverDocumentResponse(Guid Id, short Type, Guid MediaAssetId, short Status, DateTime? IssuedAtUtc, DateTime? ExpiresAtUtc, DateTime SubmittedAtUtc, DateTime? ReviewedAtUtc, string? RejectionReason, Guid ConcurrencyStamp);
public sealed record DriverZoneResponse(Guid Id, Guid ZoneId, bool IsPrimary, bool IsActive, DateTime AssignedAtUtc, DateTime? RemovedAtUtc);
public sealed record DriverAvailabilityResponse(short Status, int CurrentLoad, int MaximumConcurrentDeliveries, DateTime ChangedAtUtc);
public sealed record DriverShiftResponse(Guid Id, DateTime ScheduledStartUtc, DateTime ScheduledEndUtc, DateTime? ActualStartUtc, DateTime? ActualEndUtc, short Status, Guid ConcurrencyStamp);
public sealed record DriverViolationResponse(Guid Id, string ViolationType, short Severity, string Description, DateTime OccurredAtUtc, DateTime RecordedAtUtc, short Status, DateTime? ResolvedAtUtc, string? ResolutionNotes);
public sealed record DriverSuspensionResponse(Guid Id, string ReasonCode, string Reason, DateTime StartsAtUtc, DateTime? EndsAtUtc, short Status, DateTime? LiftedAtUtc, string? LiftReason);
public sealed record DriverProfileResponse(Guid Id, Guid UserId, string DisplayName, DateOnly? DateOfBirth, Guid? ProfilePhotoMediaId, short Status, short ActivationStatus, short EmploymentType, DriverAvailabilityResponse Availability, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? ActivatedAtUtc, Guid ConcurrencyStamp, IReadOnlyList<VehicleResponse> Vehicles, IReadOnlyList<DriverDocumentResponse> Documents, IReadOnlyList<DriverZoneResponse> Zones);
public sealed record DriverSummaryResponse(Guid Id, string DisplayName, short Status, short ActivationStatus, short EmploymentType, short AvailabilityStatus, int CurrentLoad, int MaximumConcurrentDeliveries, DateTime UpdatedAtUtc);
public sealed record PagedDriversResponse(IReadOnlyList<DriverSummaryResponse> Items, int Page, int PageSize, int TotalCount);
public sealed record DriverQuery(short? Status, short? ActivationStatus, short? AvailabilityStatus, short? EmploymentType, Guid? ZoneId, string? Search, int Page = 1, int PageSize = 20);

public sealed record DriverEligibilitySnapshot(Guid DriverId, bool IsActive, bool IsApproved, short AvailabilityStatus, short? PrimaryVehicleType, IReadOnlyList<Guid> ActiveZoneIds, int MaximumCapacity, int CurrentLoad, bool HasActiveSuspension);
public interface IDriverEligibilityProvider { Task<DriverEligibilitySnapshot?> GetAsync(Guid driverId, CancellationToken cancellationToken = default); }
public interface IDriverOperationalSnapshotProvider { Task<DriverEligibilitySnapshot?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default); }
public sealed record DriverDispatchCandidateSnapshot(Guid DriverId, bool IsActive, bool IsApproved, short AvailabilityStatus, short? PrimaryVehicleType, IReadOnlyList<Guid> ActiveZoneIds, int MaximumCapacity, int CurrentLoad, bool HasActiveSuspension, DateTime? LastAssignmentAtUtc);
public interface IDriverDispatchCandidateProvider { Task<IReadOnlyList<DriverDispatchCandidateSnapshot>> FindAsync(Guid zoneId, short? requiredVehicleType, int maximumResults, CancellationToken cancellationToken = default); }

public sealed record DriverActivatedIntegrationEvent(Guid Id, int Version, Guid DriverId, DateTime OccurredAtUtc) : IIntegrationEvent;
public sealed record DriverAvailabilityChangedIntegrationEvent(Guid Id, int Version, Guid DriverId, short Status, DateTime OccurredAtUtc) : IIntegrationEvent;
public sealed record DriverSuspendedIntegrationEvent(Guid Id, int Version, Guid DriverId, DateTime OccurredAtUtc) : IIntegrationEvent;
public sealed record DriverVehicleChangedIntegrationEvent(Guid Id, int Version, Guid DriverId, Guid VehicleId, DateTime OccurredAtUtc) : IIntegrationEvent;
public sealed record DriverOperationCompletedIntegrationEvent(Guid Id, int Version, Guid DriverId, string Operation, DateTime OccurredAtUtc) : IIntegrationEvent;
