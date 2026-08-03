using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Carts.Domain;

public readonly record struct CartId(Guid Value) { public static CartId New() => new(Guid.NewGuid()); }
public readonly record struct CartItemId(Guid Value) { public static CartItemId New() => new(Guid.NewGuid()); }
public readonly record struct CartIdempotencyRecordId(Guid Value) { public static CartIdempotencyRecordId New() => new(Guid.NewGuid()); }
public enum CartStatus : short { Active = 1, Expired = 2, Cleared = 3, Converted = 4 }

public static class CartRules
{
    public const int MaximumItems = 100;
    public const int MaximumQuantity = 99;
    public const int MaximumNoteLength = 500;
    public const int MaximumCouponLength = 64;
}

public sealed record CartCreatedDomainEvent(Guid CartId, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record CartExpiredDomainEvent(Guid CartId, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record CartClearedDomainEvent(Guid CartId, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record CartConvertedDomainEvent(Guid CartId, Guid OrderId, DateTime OccurredAtUtc) : IDomainEvent;
