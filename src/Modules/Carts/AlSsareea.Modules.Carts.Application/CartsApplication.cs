using AlSsareea.Modules.Carts.Contracts;
using AlSsareea.Modules.Carts.Domain;

namespace AlSsareea.Modules.Carts.Application;

public static class CartPermissions { public const string Read = "carts.carts.read"; public const string Manage = "carts.carts.manage"; public const string ReadAll = "carts.carts.read_all"; public const string ManageAll = "carts.carts.manage_all"; }
public static class CartErrorCodes
{
    public const string NotFound = "carts.cart_not_found"; public const string NotActive = "carts.cart_not_active"; public const string Expired = "carts.cart_expired"; public const string Empty = "carts.cart_empty";
    public const string InvalidQuantity = "carts.invalid_quantity"; public const string CustomerNotFound = "carts.customer_not_found"; public const string CustomerNotAllowed = "carts.customer_not_allowed";
    public const string MerchantUnavailable = "carts.merchant_unavailable"; public const string BranchMismatch = "carts.branch_mismatch"; public const string ProductUnavailable = "carts.product_unavailable";
    public const string CurrencyMismatch = "carts.currency_mismatch"; public const string CouponInvalid = "carts.coupon_invalid"; public const string PricingFailed = "carts.pricing_failed";
    public const string PromotionsFailed = "carts.promotions_evaluation_failed"; public const string ConcurrencyConflict = "carts.concurrency_conflict"; public const string IdempotencyConflict = "carts.idempotency_conflict";
}
public sealed class CartsOptions
{
    public const string SectionName = "Carts"; public TimeSpan ActiveCartLifetime { get; init; } = TimeSpan.FromDays(30);
    public int MaximumItems { get; init; } = CartRules.MaximumItems; public int MaximumQuantityPerItem { get; init; } = CartRules.MaximumQuantity;
    public int MaximumItemNoteLength { get; init; } = CartRules.MaximumNoteLength; public int MaximumCouponCodeLength { get; init; } = CartRules.MaximumCouponLength; public int MaximumIdempotencyKeyLength { get; init; } = 200;
}
public enum CartOperationStatus { Success, Created, NotFound, Invalid, Conflict, Forbidden, Unprocessable }
public sealed record CartOperationResult<T>(CartOperationStatus Status, T? Value = default, string? ErrorCode = null);
public static class CartOperation
{
    public static CartOperationResult<T> Success<T>(T value) => new(CartOperationStatus.Success, value);
    public static CartOperationResult<T> Created<T>(T value) => new(CartOperationStatus.Created, value);
    public static CartOperationResult<T> Failure<T>(CartOperationStatus status, string code) => new(status, default, code);
}
public interface ICartRepository
{
    Task<Cart?> GetAsync(CartId id, bool tracking, CancellationToken cancellationToken);
    Task<Cart?> GetActiveAsync(Guid customerId, Guid merchantId, Guid? branchId, CancellationToken cancellationToken);
    Task AddAsync(Cart cart, CancellationToken cancellationToken);
}
public interface ICartService
{
    Task<CartOperationResult<CartResponse>> GetOrCreateAsync(Guid userId, GetOrCreateActiveCartRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<CartOperationResult<CartResponse>> GetActiveAsync(Guid userId, Guid merchantId, Guid? branchId, CancellationToken cancellationToken);
    Task<CartOperationResult<CartResponse>> GetAsync(Guid userId, Guid cartId, CancellationToken cancellationToken);
    Task<CartOperationResult<CartResponse>> AddItemAsync(Guid userId, Guid cartId, AddCartItemRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<CartOperationResult<CartResponse>> UpdateQuantityAsync(Guid userId, Guid cartId, Guid itemId, UpdateCartItemQuantityRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<CartOperationResult<CartResponse>> RemoveItemAsync(Guid userId, Guid cartId, Guid itemId, CartConcurrencyRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<CartOperationResult<CartResponse>> ApplyCouponAsync(Guid userId, Guid cartId, ApplyCartCouponRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<CartOperationResult<CartResponse>> RemoveCouponAsync(Guid userId, Guid cartId, CartConcurrencyRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<CartOperationResult<CartResponse>> ClearAsync(Guid userId, Guid cartId, CartConcurrencyRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<CartOperationResult<CartCheckoutSummaryResponse>> CheckoutSummaryAsync(Guid userId, Guid cartId, CancellationToken cancellationToken);
}
