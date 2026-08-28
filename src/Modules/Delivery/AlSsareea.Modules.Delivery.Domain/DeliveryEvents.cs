using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Delivery.Domain;

public sealed record DeliveryCreatedDomainEvent(Guid DeliveryId, Guid OrderId, Guid CustomerId, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record DriverAssignedToDeliveryDomainEvent(Guid DeliveryId, Guid OrderId, Guid DriverId, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record DeliveryStatusChangedDomainEvent(Guid DeliveryId, DeliveryStatus PreviousStatus, DeliveryStatus NewStatus, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record DeliveryCompletedDomainEvent(Guid DeliveryId, Guid OrderId, Guid DriverId, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record DeliveryFailedDomainEvent(Guid DeliveryId, Guid OrderId, Guid DriverId, DeliveryFailureReason Reason, DateTime OccurredAtUtc) : IDomainEvent;
