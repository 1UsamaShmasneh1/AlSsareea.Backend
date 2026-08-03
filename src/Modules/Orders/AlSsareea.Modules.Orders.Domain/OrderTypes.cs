using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Orders.Domain;

public readonly record struct OrderId
{
    public OrderId(Guid value) { if (value == Guid.Empty) throw new DomainException("Order identifier is required."); Value = value; }
    public Guid Value { get; }
    public static OrderId New() => new(Guid.NewGuid());
}
public readonly record struct OrderItemId
{
    public OrderItemId(Guid value) { if (value == Guid.Empty) throw new DomainException("Order item identifier is required."); Value = value; }
    public Guid Value { get; }
    public static OrderItemId New() => new(Guid.NewGuid());
}
public readonly record struct OrderItemOptionId
{
    public OrderItemOptionId(Guid value) { if (value == Guid.Empty) throw new DomainException("Order item option identifier is required."); Value = value; }
    public Guid Value { get; }
    public static OrderItemOptionId New() => new(Guid.NewGuid());
}
public readonly record struct OrderStatusHistoryId
{
    public OrderStatusHistoryId(Guid value) { if (value == Guid.Empty) throw new DomainException("Order history identifier is required."); Value = value; }
    public Guid Value { get; }
    public static OrderStatusHistoryId New() => new(Guid.NewGuid());
}
public readonly record struct OrderOutboxMessageId
{
    public OrderOutboxMessageId(Guid value) { if (value == Guid.Empty) throw new DomainException("Outbox message identifier is required."); Value = value; }
    public Guid Value { get; }
    public static OrderOutboxMessageId New() => new(Guid.NewGuid());
}
public readonly record struct OrderCreationIdempotencyId
{
    public OrderCreationIdempotencyId(Guid value) { if (value == Guid.Empty) throw new DomainException("Idempotency identifier is required."); Value = value; }
    public Guid Value { get; }
    public static OrderCreationIdempotencyId New() => new(Guid.NewGuid());
}

public enum OrderType : short { Restaurant = 1, Store = 2, Parcel = 3 }
public enum OrderStatus : short
{
    Draft = 1, PendingPayment = 2, PaymentAuthorized = 3, Submitted = 4, AcceptedByMerchant = 5,
    RejectedByMerchant = 6, Preparing = 7, ReadyForPickup = 8, SearchingForDriver = 9,
    DriverAssigned = 10, DriverArrivingToPickup = 11, PickedUp = 12, OnTheWay = 13,
    Arrived = 14, Delivered = 15, Cancelled = 16, RefundPending = 17, Refunded = 18, Failed = 19,
}
public enum OrderChangeSource : short { Customer = 1, Merchant = 2, Operations = 3, System = 4, Payment = 5, Delivery = 6 }
public enum CancellationActor : short { Customer = 1, Merchant = 2, Operations = 3, System = 4 }

public static class OrderRules
{
    public const int OrderNumberMaximumLength = 32;
    public const int CurrencyLength = 3;
    public const int CustomerNotesMaximumLength = 500;
    public const int MerchantNotesMaximumLength = 500;
    public const int NameMaximumLength = 300;
    public const int AddressMaximumLength = 300;
    public const int ReasonCodeMaximumLength = 80;
    public const int ReasonTextMaximumLength = 500;
    public const int IdempotencyKeyMaximumLength = 200;
}
