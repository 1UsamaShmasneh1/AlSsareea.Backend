using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Orders.Domain;

public sealed record OrderCreatedDomainEvent(Guid OrderId, string OrderNumber, Guid CustomerId, Guid MerchantId, Guid SourceCartId, OrderStatus Status, long TotalMinor, string Currency, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record OrderStatusChangedDomainEvent(Guid OrderId, OrderStatus PreviousStatus, OrderStatus NewStatus, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record OrderCancelledDomainEvent(Guid OrderId, OrderStatus Status, CancellationActor Actor, string ReasonCode, DateTime OccurredAtUtc) : IDomainEvent;
