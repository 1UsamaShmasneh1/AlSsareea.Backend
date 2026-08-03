using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Orders.Application;
using AlSsareea.Modules.Orders.Contracts;

namespace AlSsareea.Api.Endpoints;

internal static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/orders").WithTags("Orders").RequireAuthorization();
        group.MapPost("/", Create).RequireAuthorization(Permission(OrderPermissions.Create)).WithName("CreateOrder").Produces<CreateOrderResponse>(201).ProducesProblem(400).ProducesProblem(409).ProducesProblem(422);
        group.MapGet("/{orderId:guid}", Get).RequireAuthorization(Permission(OrderPermissions.ReadOwn)).WithName("GetOrder").Produces<OrderDetailsResponse>().ProducesProblem(404);
        group.MapGet("/by-number/{orderNumber}", GetByNumber).RequireAuthorization(Permission(OrderPermissions.ReadOwn)).WithName("GetOrderByNumber").Produces<OrderDetailsResponse>().ProducesProblem(404);
        group.MapGet("/", List).RequireAuthorization(Permission(OrderPermissions.ReadOwn)).WithName("ListOrders").Produces<OrderListResponse>();
        group.MapGet("/{orderId:guid}/timeline", Timeline).RequireAuthorization(Permission(OrderPermissions.ReadOwn)).WithName("GetOrderTimeline").Produces<IReadOnlyList<OrderTimelineEntryResponse>>().ProducesProblem(404);
        group.MapPost("/{orderId:guid}/cancel", Cancel).RequireAuthorization(Permission(OrderPermissions.CancelOwn)).WithName("CancelOrder").Produces<OrderDetailsResponse>().ProducesProblem(409);
        return endpoints;
    }

    private static Task<IResult> Create(CreateOrderRequest request, HttpRequest http, ICurrentUser user, IOrderService service, CancellationToken ct) => Execute(service.CreateAsync(UserId(user), request, http.Headers["Idempotency-Key"].ToString(), ct));
    private static Task<IResult> Get(Guid orderId, ICurrentUser user, IOrderService service, CancellationToken ct) => Execute(service.GetAsync(UserId(user), orderId, ct));
    private static Task<IResult> GetByNumber(string orderNumber, ICurrentUser user, IOrderService service, CancellationToken ct) => Execute(service.GetByNumberAsync(UserId(user), orderNumber, ct));
    private static Task<IResult> List(int page, int pageSize, ICurrentUser user, IOrderService service, CancellationToken ct) => Execute(service.ListAsync(UserId(user), page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize, ct));
    private static Task<IResult> Timeline(Guid orderId, ICurrentUser user, IOrderService service, CancellationToken ct) => Execute(service.TimelineAsync(UserId(user), orderId, ct));
    private static Task<IResult> Cancel(Guid orderId, CancelOrderRequest request, HttpContext context, ICurrentUser user, IOrderService service, CancellationToken ct) => Execute(service.CancelAsync(UserId(user), orderId, request, context.Request.Headers["X-Correlation-ID"].ToString(), ct));
    private static Guid UserId(ICurrentUser user) => user.UserId?.Value ?? Guid.Empty;
    private static string Permission(string value) => AuthenticationPolicies.PermissionPrefix + value;
    private static async Task<IResult> Execute<T>(Task<OrderOperationResult<T>> task)
    {
        OrderOperationResult<T> result = await task; if (result.Value is not null) return result.Status == OrderOperationStatus.Created ? Results.Created((string?)null, result.Value) : Results.Ok(result.Value);
        int status = result.Status switch { OrderOperationStatus.NotFound => 404, OrderOperationStatus.Forbidden => 403, OrderOperationStatus.Conflict => 409, OrderOperationStatus.Unprocessable => 422, _ => 400 };
        return Results.Problem(statusCode: status, title: "Order operation failed.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }
}
