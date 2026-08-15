using AlSsareea.BuildingBlocks.Contracts;
using AlSsareea.Modules.Drivers.Contracts;
using AlSsareea.Modules.Drivers.Domain;

namespace AlSsareea.Modules.Drivers.Application;

public static class DriverErrorCodes
{
    public const string NotFound = "drivers.driver_not_found";
    public const string InvalidRequest = "drivers.invalid_request";
    public const string Forbidden = "drivers.forbidden";
    public const string Conflict = "drivers.conflict";
    public const string ConcurrencyConflict = "drivers.concurrency_conflict";
    public const string IdempotencyConflict = "drivers.idempotency_conflict";
    public const string IdentityInvalid = "drivers.identity_invalid";
    public const string MediaInvalid = "drivers.media_invalid";
    public const string ZoneInvalid = "drivers.zone_invalid";
}

public enum DriverOperationStatus { Success, Created, NotFound, Invalid, Conflict, Forbidden }
public sealed record DriverOperationResult<T>(DriverOperationStatus Status, T? Value = default, string? ErrorCode = null);
public sealed record DriverActor(Guid UserId, string? CorrelationId);
public sealed record DriverAuditEntry(Guid ActorUserId, DriverId DriverId, string Action, DateTime OccurredAtUtc, string? CorrelationId, string? ReasonCode);
public sealed record DriverIdempotencyEntry(Guid ActorUserId, string Operation, string KeyHash, string RequestHash, DriverId DriverId, DriverOperationStatus ResponseStatus, string ResponseJson, DateTime CreatedAtUtc);
public enum DriverIdempotencyState { New, SameRequest, DifferentRequest }
public sealed record DriverIdempotencyResult(DriverIdempotencyState State, DriverOperationStatus? ResponseStatus = null, string? ResponseJson = null);

public interface IDriverRepository
{
    Task<Driver?> GetAsync(DriverId id, CancellationToken cancellationToken);
    Task<Driver?> GetByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> UserHasDriverAsync(Guid userId, CancellationToken cancellationToken);
    Task AddAsync(Driver driver, CancellationToken cancellationToken);
    Task<DriverIdempotencyResult> CheckIdempotencyAsync(Guid actorUserId, string operation, string keyHash, string requestHash, CancellationToken cancellationToken);
    Task<bool> SaveOperationAsync(Driver driver, DriverIdempotencyEntry? idempotency, DriverAuditEntry? audit, IReadOnlyCollection<IIntegrationEvent> integrationEvents, CancellationToken cancellationToken);
    Task<DriverProfileResponse?> GetProfileAsync(DriverId id, CancellationToken cancellationToken);
    Task<DriverProfileResponse?> GetProfileByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<PagedDriversResponse> ListAsync(DriverQuery query, CancellationToken cancellationToken);
}

public interface IDriverService
{
    Task<DriverOperationResult<DriverProfileResponse>> CreateAsync(DriverActor actor, CreateDriverRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverProfileResponse>> GetMyAsync(DriverActor actor, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverProfileResponse>> GetAsync(DriverActor actor, Guid driverId, CancellationToken cancellationToken);
    Task<DriverOperationResult<PagedDriversResponse>> ListAsync(DriverQuery query, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverProfileResponse>> UpdateProfileAsync(DriverActor actor, UpdateDriverProfileRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverProfileResponse>> TransitionAsync(DriverActor actor, Guid driverId, string operation, Guid concurrencyStamp, string? reason, string idempotencyKey, CancellationToken cancellationToken);
    Task<DriverOperationResult<VehicleResponse>> AddVehicleAsync(DriverActor actor, AddVehicleRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverProfileResponse>> ReviewVehicleAsync(DriverActor actor, Guid driverId, Guid vehicleId, bool approve, VehicleReviewRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverProfileResponse>> SetPrimaryVehicleAsync(DriverActor actor, Guid vehicleId, string idempotencyKey, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverDocumentResponse>> SubmitDocumentAsync(DriverActor actor, SubmitDriverDocumentRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverProfileResponse>> ReviewDocumentAsync(DriverActor actor, Guid driverId, Guid documentId, bool approve, DocumentReviewRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverProfileResponse>> AssignZoneAsync(DriverActor actor, Guid driverId, AssignDriverZoneRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverProfileResponse>> RemoveZoneAsync(DriverActor actor, Guid driverId, Guid zoneId, string idempotencyKey, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverAvailabilityResponse>> ChangeAvailabilityAsync(DriverActor actor, string operation, string idempotencyKey, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverShiftResponse>> CreateShiftAsync(DriverActor actor, Guid driverId, CreateDriverShiftRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverProfileResponse>> ChangeShiftAsync(DriverActor actor, Guid driverId, Guid shiftId, string operation, string idempotencyKey, CancellationToken cancellationToken);
    Task<DriverOperationResult<IReadOnlyList<DriverShiftResponse>>> ListShiftsAsync(DriverActor actor, Guid driverId, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverShiftResponse>> GetShiftAsync(DriverActor actor, Guid driverId, Guid shiftId, CancellationToken cancellationToken);
    Task<DriverOperationResult<IReadOnlyList<DriverShiftResponse>>> ListMyShiftsAsync(DriverActor actor, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverShiftResponse>> GetMyShiftAsync(DriverActor actor, Guid shiftId, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverProfileResponse>> ChangeMyShiftAsync(DriverActor actor, Guid shiftId, string operation, string idempotencyKey, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverViolationResponse>> RecordViolationAsync(DriverActor actor, Guid driverId, RecordDriverViolationRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverProfileResponse>> ResolveViolationAsync(DriverActor actor, Guid driverId, Guid violationId, ResolveDriverViolationRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverSuspensionResponse>> SuspendAsync(DriverActor actor, Guid driverId, SuspendDriverRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<DriverOperationResult<DriverProfileResponse>> LiftSuspensionAsync(DriverActor actor, Guid driverId, Guid suspensionId, LiftDriverSuspensionRequest request, string idempotencyKey, CancellationToken cancellationToken);
}
