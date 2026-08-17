using AlSsareea.BuildingBlocks.Contracts;
using AlSsareea.Modules.Delivery.Contracts;
using AlSsareea.Modules.Delivery.Domain;
using DeliveryAggregate = AlSsareea.Modules.Delivery.Domain.Delivery;

namespace AlSsareea.Modules.Delivery.Application;

public static class DeliveryErrorCodes
{
    public const string NotFound = "delivery.not_found";
    public const string InvalidRequest = "delivery.invalid_request";
    public const string OrderInvalid = "delivery.order_invalid";
    public const string OrderIneligible = "delivery.order_ineligible";
    public const string DriverInvalid = "delivery.driver_invalid";
    public const string DriverIneligible = "delivery.driver_ineligible";
    public const string MediaInvalid = "delivery.media_invalid";
    public const string Forbidden = "delivery.forbidden";
    public const string InvalidTransition = "delivery.invalid_transition";
    public const string ProofIncomplete = "delivery.proof_incomplete";
    public const string PinInvalid = "delivery.pin_invalid";
    public const string PinLocked = "delivery.pin_locked";
    public const string IdempotencyConflict = "delivery.idempotency_conflict";
    public const string ConcurrencyConflict = "delivery.concurrency_conflict";
    public const string OrderAlreadyHasDelivery = "delivery.order_already_has_delivery";
}

public enum DeliveryOperationStatus { Success, Created, NotFound, Invalid, Conflict, Forbidden, Unprocessable }
public sealed record DeliveryOperationResult<T>(DeliveryOperationStatus Status, T? Value = default, string? ErrorCode = null);
public sealed record DeliveryActor(Guid UserId, string? CorrelationId);
public sealed record DeliveryIdempotencyResult(Guid DeliveryId, string RequestHash);
public sealed record DeliveryAuditEntry(Guid ActorUserId, DeliveryId DeliveryId, string Operation, DeliveryStatus OldStatus, DeliveryStatus NewStatus, DateTime OccurredAtUtc, string? CorrelationId, string IdempotencyKeyHash, string? SafeReasonCode);

public interface IDeliveryRepository
{
    Task<DeliveryAggregate?> GetAsync(DeliveryId id, bool noTracking, CancellationToken cancellationToken);
    Task<DeliveryAggregate?> GetByOrderAsync(Guid orderId, bool noTracking, CancellationToken cancellationToken);
    Task<DeliveryAggregate?> GetCurrentForCustomerAsync(Guid customerId, CancellationToken cancellationToken);
    Task<DeliveryAggregate?> GetCurrentForDriverAsync(Guid driverId, CancellationToken cancellationToken);
    Task<DeliveryIdempotencyResult?> FindIdempotencyAsync(Guid actorId, string operation, string keyHash, CancellationToken cancellationToken);
    Task<bool> CreateAsync(DeliveryAggregate delivery, Guid actorId, string keyHash, string requestHash, IReadOnlyCollection<IIntegrationEvent> integrationEvents, CancellationToken cancellationToken);
    Task<bool> SaveOperationAsync(DeliveryAggregate delivery, Guid actorId, string operation, string keyHash, string requestHash, DeliveryAuditEntry audit, IReadOnlyCollection<IIntegrationEvent> integrationEvents, CancellationToken cancellationToken);
}

public interface IDeliveryPinProtector
{
    DeliveryPinSecret Generate();
    bool Verify(string candidate, string hash, string salt);
}

public sealed record DeliveryPinSecret(string Pin, string Hash, string Salt);

public interface IDeliveryService
{
    Task<DeliveryOperationResult<DeliveryCreatedResponse>> CreateAsync(DeliveryActor actor, CreateDeliveryRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<DeliveryOperationResult<DeliveryResponse>> AssignAsync(DeliveryActor actor, Guid deliveryId, AssignDeliveryRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<DeliveryOperationResult<DeliveryResponse>> GetForCustomerAsync(DeliveryActor actor, Guid deliveryId, CancellationToken cancellationToken);
    Task<DeliveryOperationResult<DeliveryResponse>> GetCurrentForCustomerAsync(DeliveryActor actor, CancellationToken cancellationToken);
    Task<DeliveryOperationResult<DeliveryResponse>> GetCurrentForDriverAsync(DeliveryActor actor, CancellationToken cancellationToken);
    Task<DeliveryOperationResult<DeliveryResponse>> TransitionAsync(DeliveryActor actor, Guid deliveryId, string operation, DeliveryTransitionRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<DeliveryOperationResult<DeliveryResponse>> SubmitProofAsync(DeliveryActor actor, Guid deliveryId, SubmitDeliveryProofRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<DeliveryOperationResult<DeliveryResponse>> ReportFailedAsync(DeliveryActor actor, Guid deliveryId, ReportFailedDeliveryRequest request, string idempotencyKey, CancellationToken cancellationToken);
}
