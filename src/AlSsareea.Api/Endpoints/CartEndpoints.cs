using AlSsareea.Modules.Carts.Application;
using AlSsareea.Modules.Carts.Contracts;
using AlSsareea.Modules.Identity.Application;

namespace AlSsareea.Api.Endpoints;

internal static class CartEndpoints
{
    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/carts").WithTags("Carts").RequireAuthorization();
        group.MapPost("/", GetOrCreate).RequireRateLimiting("carts-write").WithName("GetOrCreateCart").Produces<CartResponse>(201).ProducesProblem(409);
        group.MapGet("/active", GetActive).WithName("GetActiveCart").Produces<CartResponse>().ProducesProblem(404);
        group.MapGet("/{cartId:guid}", Get).WithName("GetCart").Produces<CartResponse>().ProducesProblem(404);
        group.MapPost("/{cartId:guid}/items", AddItem).RequireRateLimiting("carts-write").WithName("AddCartItem");
        group.MapPatch("/{cartId:guid}/items/{itemId:guid}", UpdateQuantity).RequireRateLimiting("carts-write").WithName("UpdateCartItemQuantity");
        group.MapDelete("/{cartId:guid}/items/{itemId:guid}", RemoveItem).RequireRateLimiting("carts-write").WithName("RemoveCartItem");
        group.MapPut("/{cartId:guid}/coupon", ApplyCoupon).RequireRateLimiting("carts-write").WithName("ApplyCartCoupon");
        group.MapDelete("/{cartId:guid}/coupon", RemoveCoupon).RequireRateLimiting("carts-write").WithName("RemoveCartCoupon");
        group.MapDelete("/{cartId:guid}/items", Clear).RequireRateLimiting("carts-write").WithName("ClearCart");
        group.MapPost("/{cartId:guid}/reprice", Summary).RequireRateLimiting("carts-write").WithName("RepriceCart");
        group.MapGet("/{cartId:guid}/checkout-summary", Summary).WithName("GetCartCheckoutSummary").Produces<CartCheckoutSummaryResponse>();
        return endpoints;
    }
    private static Task<IResult> GetOrCreate(GetOrCreateActiveCartRequest request, HttpRequest http, ICurrentUser user, ICartService service, CancellationToken ct) =>
        Execute(service.GetOrCreateAsync(UserId(user), request, Key(http), ct));
    private static Task<IResult> GetActive(Guid merchantId, Guid? branchId, ICurrentUser user, ICartService service, CancellationToken ct) =>
        Execute(service.GetActiveAsync(UserId(user), merchantId, branchId, ct));
    private static Task<IResult> Get(Guid cartId, ICurrentUser user, ICartService service, CancellationToken ct) => Execute(service.GetAsync(UserId(user), cartId, ct));
    private static Task<IResult> AddItem(Guid cartId, AddCartItemRequest request, HttpRequest http, ICurrentUser user, ICartService service, CancellationToken ct) =>
        Execute(service.AddItemAsync(UserId(user), cartId, request, Key(http), ct));
    private static Task<IResult> UpdateQuantity(Guid cartId, Guid itemId, UpdateCartItemQuantityRequest request, HttpRequest http, ICurrentUser user, ICartService service, CancellationToken ct) =>
        Execute(service.UpdateQuantityAsync(UserId(user), cartId, itemId, request, Key(http), ct));
    private static Task<IResult> RemoveItem(Guid cartId, Guid itemId, Guid concurrencyStamp, HttpRequest http, ICurrentUser user, ICartService service, CancellationToken ct) =>
        Execute(service.RemoveItemAsync(UserId(user), cartId, itemId, new CartConcurrencyRequest(concurrencyStamp), Key(http), ct));
    private static Task<IResult> ApplyCoupon(Guid cartId, ApplyCartCouponRequest request, HttpRequest http, ICurrentUser user, ICartService service, CancellationToken ct) =>
        Execute(service.ApplyCouponAsync(UserId(user), cartId, request, Key(http), ct));
    private static Task<IResult> RemoveCoupon(Guid cartId, Guid concurrencyStamp, HttpRequest http, ICurrentUser user, ICartService service, CancellationToken ct) =>
        Execute(service.RemoveCouponAsync(UserId(user), cartId, new CartConcurrencyRequest(concurrencyStamp), Key(http), ct));
    private static Task<IResult> Clear(Guid cartId, Guid concurrencyStamp, HttpRequest http, ICurrentUser user, ICartService service, CancellationToken ct) =>
        Execute(service.ClearAsync(UserId(user), cartId, new CartConcurrencyRequest(concurrencyStamp), Key(http), ct));
    private static Task<IResult> Summary(Guid cartId, ICurrentUser user, ICartService service, CancellationToken ct) => Execute(service.CheckoutSummaryAsync(UserId(user), cartId, ct));
    private static Guid UserId(ICurrentUser user) => user.UserId?.Value ?? Guid.Empty;
    private static string Key(HttpRequest request) => request.Headers["Idempotency-Key"].ToString();
    private static async Task<IResult> Execute<T>(Task<CartOperationResult<T>> task)
    {
        CartOperationResult<T> result = await task;
        if (result.Value is not null) return result.Status == CartOperationStatus.Created ? Results.Created((string?)null, result.Value) : Results.Ok(result.Value);
        int status = result.Status switch { CartOperationStatus.NotFound => 404, CartOperationStatus.Forbidden => 403, CartOperationStatus.Conflict => 409, CartOperationStatus.Unprocessable => 422, _ => 400 };
        return Results.Problem(statusCode: status, title: "Cart operation failed.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }
}
