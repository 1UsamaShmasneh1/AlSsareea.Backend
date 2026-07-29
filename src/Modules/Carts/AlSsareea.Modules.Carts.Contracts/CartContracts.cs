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
public sealed record CartCheckoutItemResponse(Guid CartItemId, Guid ProductId, string? ProductName, Guid? VariantId, string? VariantName, int Quantity, long UnitPriceMinor, long LineSubtotalMinor, long LineDiscountMinor, long LineTotalMinor, bool IsAvailable, bool HasChanged, IReadOnlyList<CartBlockingReason> BlockingReasons);
public sealed record CartCheckoutSummaryResponse(Guid CartId, Guid CustomerId, Guid MerchantId, Guid? BranchId, short CartStatus, string? Currency, IReadOnlyList<CartCheckoutItemResponse> Items, long SubtotalMinor, long DeliveryFeeMinor, long ServiceFeeMinor, long TaxMinor, long OtherFeesMinor, long PromotionDiscountMinor, long GrandTotalMinor, IReadOnlyList<CartBlockingReason> BlockingReasons, bool IsCheckoutReady, string? PricingReference, string? PromotionEvaluationReference, DateTime CalculatedAtUtc, Guid ConcurrencyStamp, DateTime ExpiresAtUtc);
