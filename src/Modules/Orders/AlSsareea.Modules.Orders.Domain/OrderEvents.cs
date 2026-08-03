using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Orders.Domain;

public sealed record OrderCreatedDomainEvent(Guid OrderId, string OrderNumber, Guid CustomerId, Guid MerchantId, Guid SourceCartId, OrderStatus Status, long TotalMinor, string Currency, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record OrderStatusChangedDomainEvent(Guid OrderId, OrderStatus PreviousStatus, OrderStatus NewStatus, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record OrderCancelledDomainEvent(Guid OrderId, OrderStatus Status, CancellationActor Actor, string ReasonCode, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record OrderAcceptedByMerchantDomainEvent(Guid OrderId, Guid MerchantId, Guid? BranchId, Guid ActorUserId, int PreparationMinutes, DateTime EstimatedReadyAtUtc, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record OrderRejectedByMerchantDomainEvent(Guid OrderId, Guid MerchantId, Guid? BranchId, Guid ActorUserId, MerchantOrderRejectionReason Reason, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record OrderPreparationTimeUpdatedDomainEvent(Guid OrderId, Guid MerchantId, Guid? BranchId, Guid ActorUserId, int PreparationMinutes, DateTime EstimatedReadyAtUtc, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record OrderPreparationStartedDomainEvent(Guid OrderId, Guid MerchantId, Guid? BranchId, Guid ActorUserId, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record OrderReadyForPickupDomainEvent(Guid OrderId, Guid MerchantId, Guid? BranchId, Guid ActorUserId, DateTime OccurredAtUtc) : IDomainEvent;
