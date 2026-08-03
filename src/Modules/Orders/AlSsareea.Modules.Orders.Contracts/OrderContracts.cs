namespace AlSsareea.Modules.Orders.Contracts;

public static class OrderPermissions
{
    public const string Create = "orders.orders.create";
    public const string ReadOwn = "orders.orders.read_own";
    public const string CancelOwn = "orders.orders.cancel_own";
    public const string ReadMerchant = "orders.orders.read_merchant";
    public const string Manage = "orders.orders.manage";
    public const string ReadAll = "orders.orders.read_all";
}

public sealed record CreateOrderRequest(Guid CartId, Guid DeliveryAddressId, short OrderType, DateTime? ScheduledForUtc, string? CustomerNotes, Guid? ExpectedCartVersion);
public sealed record CancelOrderRequest(short Actor, string ReasonCode, string? Reason, Guid ConcurrencyStamp);
public sealed record CreateOrderResponse(Guid OrderId, string OrderNumber, short Status, string Currency, long TotalMinor, DateTime CreatedAtUtc);
public sealed record OrderOptionResponse(Guid OptionGroupId, Guid OptionId, string OptionGroupName, string OptionName, int Quantity, long UnitPriceAdjustmentMinor, long TotalPriceAdjustmentMinor);
public sealed record OrderItemResponse(Guid Id, Guid ProductId, int ProductVersion, Guid? VariantId, string ProductName, string? VariantName, string? Sku, int Quantity, long UnitBasePriceMinor, long UnitOptionsPriceMinor, long UnitDiscountMinor, long UnitFinalPriceMinor, long LineSubtotalMinor, long LineDiscountMinor, long LineTotalMinor, string? CustomerNote, IReadOnlyList<OrderOptionResponse> Options);
public sealed record OrderCustomerResponse(Guid CustomerId, string DisplayName, string PreferredLanguage);
public sealed record OrderAddressResponse(Guid AddressId, string Label, string City, string? Area, string Street, string? BuildingNumber, string? Floor, string? Apartment, string? DeliveryInstructions, double? Latitude, double? Longitude, string? PlaceId, string? FormattedAddress);
public sealed record OrderMerchantResponse(Guid MerchantId, Guid? BranchId, string MerchantDisplayName, string? BranchDisplayName, string? BranchAddress, string? BranchPhoneNumber);
public sealed record OrderPricingResponse(long SubtotalMinor, long OptionsTotalMinor, long ProductDiscountMinor, long CouponDiscountMinor, long DeliveryDiscountMinor, long DeliveryFeeMinor, long ServiceFeeMinor, long PlatformFeeMinor, long SmallOrderFeeMinor, long TaxMinor, long TotalMinor, string Currency, string? PricingReference, DateTime CalculatedAtUtc);
public sealed record OrderTimelineEntryResponse(Guid Id, short? PreviousStatus, short NewStatus, DateTime ChangedAtUtc, short Source, string? ReasonCode, string? ReasonText, string? CorrelationId);
public sealed record OrderDetailsResponse(Guid Id, string OrderNumber, Guid SourceCartId, short Type, short Status, string Currency, long TotalMinor, DateTime? ScheduledForUtc, string? CustomerNotes, string? CancellationCode, string? CancellationReason, short? CancelledBy, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, Guid ConcurrencyStamp, OrderCustomerResponse Customer, OrderAddressResponse DeliveryAddress, OrderMerchantResponse Merchant, OrderPricingResponse Pricing, IReadOnlyList<OrderItemResponse> Items, IReadOnlyList<OrderTimelineEntryResponse> Timeline);
public sealed record OrderListItemResponse(Guid Id, string OrderNumber, short Type, short Status, string Currency, long TotalMinor, string MerchantDisplayName, DateTime CreatedAtUtc, DateTime? ScheduledForUtc);
public sealed record OrderListResponse(IReadOnlyList<OrderListItemResponse> Items, int Page, int PageSize, int TotalCount);

public sealed record OrderCreatedIntegrationEvent(Guid Id, int Version, Guid OrderId, string OrderNumber, Guid CustomerId, Guid MerchantId, Guid? BranchId, Guid SourceCartId, short Status, long TotalMinor, string Currency, DateTime OccurredAtUtc) : AlSsareea.BuildingBlocks.Contracts.IIntegrationEvent;
public sealed record OrderCancelledIntegrationEvent(Guid Id, int Version, Guid OrderId, Guid CustomerId, Guid MerchantId, short PreviousStatus, short Actor, string ReasonCode, DateTime OccurredAtUtc) : AlSsareea.BuildingBlocks.Contracts.IIntegrationEvent;
