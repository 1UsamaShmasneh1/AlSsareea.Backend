namespace AlSsareea.Modules.Carts.Contracts;

public sealed record GetOrCreateActiveCartRequest(Guid MerchantId, Guid? BranchId);
public sealed record CartItemOptionRequest(Guid OptionGroupId, Guid OptionItemId, int Quantity = 1);
public sealed record AddCartItemRequest(Guid ProductId, Guid? ProductVariantId, int Quantity, string? CustomerNote, IReadOnlyList<CartItemOptionRequest> SelectedOptions, Guid ConcurrencyStamp);
public sealed record UpdateCartItemQuantityRequest(int Quantity, Guid ConcurrencyStamp);
public sealed record ApplyCartCouponRequest(string CouponCode, Guid ConcurrencyStamp);
public sealed record CartConcurrencyRequest(Guid ConcurrencyStamp);
public sealed record CartItemOptionResponse(Guid OptionGroupId, Guid OptionItemId, int Quantity, int CatalogVersion);
public sealed record CartItemResponse(Guid Id, Guid ProductId, Guid? ProductVariantId, int Quantity, string? CustomerNote, int CatalogVersion, IReadOnlyList<CartItemOptionResponse> SelectedOptions, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record CartResponse(Guid Id, Guid CustomerId, Guid MerchantId, Guid? BranchId, short Status, string? CouponCode, DateTime ExpiresAtUtc, DateTime? LastPricedAtUtc, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, Guid ConcurrencyStamp, IReadOnlyList<CartItemResponse> Items);
public sealed record CartBlockingReason(string Code, string Message);
public sealed record CartCheckoutOptionResponse(Guid OptionGroupId, Guid OptionId, string OptionGroupName, string OptionName, int Quantity, long UnitPriceAdjustmentMinor, long TotalPriceAdjustmentMinor);
public sealed record CartCheckoutItemResponse(Guid CartItemId, Guid ProductId, int ProductVersion, string? ProductName, string? Sku, Guid? VariantId, string? VariantName, int Quantity, long UnitBasePriceMinor, long UnitOptionsPriceMinor, long UnitPriceMinor, long LineSubtotalMinor, long LineDiscountMinor, long LineTotalMinor, string? CustomerNote, bool IsAvailable, bool HasChanged, IReadOnlyList<CartCheckoutOptionResponse> Options, IReadOnlyList<CartBlockingReason> BlockingReasons);
public sealed record CartCheckoutSummaryResponse(Guid CartId, Guid CustomerId, Guid MerchantId, Guid? BranchId, short CartStatus, string? Currency, IReadOnlyList<CartCheckoutItemResponse> Items, long SubtotalMinor, long DeliveryFeeMinor, long ServiceFeeMinor, long TaxMinor, long OtherFeesMinor, long PromotionDiscountMinor, long GrandTotalMinor, IReadOnlyList<CartBlockingReason> BlockingReasons, bool IsCheckoutReady, string? PricingReference, string? PromotionEvaluationReference, DateTime CalculatedAtUtc, Guid ConcurrencyStamp, DateTime ExpiresAtUtc);

public enum OrderCheckoutStatus { Success, NotFound, Invalid, Conflict }
public sealed record OrderCheckoutResult(OrderCheckoutStatus Status, CartCheckoutSummaryResponse? Summary = null, string? ErrorCode = null);
public interface IOrderCheckoutProvider
{
    Task<OrderCheckoutResult> GetTrustedSummaryAsync(Guid userId, Guid cartId, Guid? expectedConcurrencyStamp, CancellationToken cancellationToken = default);
    Task<bool> MarkConvertedAsync(Guid userId, Guid cartId, Guid orderId, CancellationToken cancellationToken = default);
}
