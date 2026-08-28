using AlSsareea.BuildingBlocks.Contracts;

namespace AlSsareea.Modules.Delivery.Contracts;

public static class DeliveryPermissions
{
    public const string ReadOwn = "delivery.deliveries.read.own";
    public const string ReadSelf = "delivery.deliveries.read.self";
    public const string OperateSelf = "delivery.deliveries.operate.self";
    public const string Manage = "delivery.deliveries.manage";
    public const string ReadAll = "delivery.deliveries.read.all";
}

public sealed record CreateDeliveryRequest(Guid OrderId, short ProofRequirements);
public sealed record AssignDeliveryRequest(Guid DriverId, Guid ConcurrencyStamp);
public sealed record DeliveryTransitionRequest(Guid ConcurrencyStamp);
public sealed record SubmitDeliveryProofRequest(short Type, string? Pin, Guid? MediaAssetId, string? RecipientName, Guid ConcurrencyStamp);
public sealed record ReportFailedDeliveryRequest(short Reason, string? Notes, Guid ConcurrencyStamp);
public sealed record DeliveryStatusHistoryResponse(Guid Id, short? PreviousStatus, short NewStatus, short Source, DateTime ChangedAtUtc, string? ReasonCode, string? ReasonText);
public sealed record DeliveryProofResponse(Guid Id, short Type, Guid? MediaAssetId, string? RecipientName, DateTime SubmittedAtUtc);
public sealed record DeliveryLocationSnapshotResponse(string Address, string? ContactName, string? PhoneNumber, string? Floor, string? Instructions, double? Latitude, double? Longitude);
public sealed record DeliveryResponse(
    Guid Id, Guid OrderId, Guid CustomerId, Guid MerchantId, Guid? BranchId, Guid? DriverId, short Status,
    short ProofRequirements, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? AssignedAtUtc,
    DateTime? ArrivedAtPickupAtUtc, DateTime? PickedUpAtUtc, DateTime? StartedAtUtc,
    DateTime? ArrivedAtDropOffAtUtc, DateTime? DeliveredAtUtc, DateTime? FailedAtUtc,
    short? FailureReason, string? FailureNotes, Guid ConcurrencyStamp,
    DeliveryLocationSnapshotResponse Pickup, DeliveryLocationSnapshotResponse DropOff,
    IReadOnlyList<DeliveryStatusHistoryResponse> Timeline, IReadOnlyList<DeliveryProofResponse> Proofs);
public sealed record DeliveryCreatedResponse(DeliveryResponse Delivery, string? Pin);
public sealed record DispatchDeliverySnapshot(Guid DeliveryId, Guid OrderId, Guid MerchantId, Guid? BranchId, short Status, Guid? DriverId, double? PickupLatitude, double? PickupLongitude);
public enum DispatchAssignmentStatus { Applied, AlreadyApplied, NotFound, Invalid, Conflict }
public sealed record DispatchAssignmentResult(DispatchAssignmentStatus Status, Guid? DriverId = null, string? ErrorCode = null);
public interface IDispatchDeliveryProvider
{
    Task<DispatchDeliverySnapshot?> GetAsync(Guid deliveryId, CancellationToken cancellationToken = default);
    Task<DispatchAssignmentResult> AssignAsync(Guid deliveryId, Guid driverId, Guid assignmentId, CancellationToken cancellationToken = default);
}

public sealed record DeliveryCreatedIntegrationEvent(Guid Id, int Version, Guid DeliveryId, Guid OrderId, Guid CustomerId, Guid MerchantId, DateTime OccurredAtUtc) : IIntegrationEvent;
public sealed record DeliveryDriverAssignedIntegrationEvent(Guid Id, int Version, Guid DeliveryId, Guid OrderId, Guid DriverId, DateTime OccurredAtUtc) : IIntegrationEvent;
public sealed record DeliveryStatusChangedIntegrationEvent(Guid Id, int Version, Guid DeliveryId, Guid OrderId, Guid DriverId, short PreviousStatus, short NewStatus, DateTime OccurredAtUtc) : IIntegrationEvent;
public sealed record DeliveryCompletedIntegrationEvent(Guid Id, int Version, Guid DeliveryId, Guid OrderId, Guid DriverId, DateTime OccurredAtUtc) : IIntegrationEvent;
public sealed record DeliveryFailedIntegrationEvent(Guid Id, int Version, Guid DeliveryId, Guid OrderId, Guid DriverId, short Reason, DateTime OccurredAtUtc) : IIntegrationEvent;
