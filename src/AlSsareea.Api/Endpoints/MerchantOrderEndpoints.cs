using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Orders.Application;
using AlSsareea.Modules.Orders.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AlSsareea.Api.Endpoints;

internal static class MerchantOrderEndpoints
{
    public static IEndpointRouteBuilder MapMerchantOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/merchant/orders").WithTags("Merchant Orders").RequireAuthorization();
        group.MapGet("/", List).RequireAuthorization(Permission(OrderPermissions.MerchantRead)).RequireRateLimiting("merchant-orders-read")
            .WithName("ListMerchantOrders").Produces<PagedMerchantOrdersResponse>().ProducesProblem(400).ProducesProblem(404);
        group.MapGet("/{orderId:guid}", Get).RequireAuthorization(Permission(OrderPermissions.MerchantRead)).RequireRateLimiting("merchant-orders-read")
            .WithName("GetMerchantOrder").Produces<MerchantOrderDetails>().ProducesProblem(404);
        group.MapGet("/{orderId:guid}/history", History).RequireAuthorization(Permission(OrderPermissions.MerchantHistory)).RequireRateLimiting("merchant-orders-read")
            .WithName("GetMerchantOrderHistory").Produces<IReadOnlyList<MerchantOrderStatusHistoryEntry>>().ProducesProblem(404);
        group.MapPost("/{orderId:guid}/accept", Accept).RequireAuthorization(Permission(OrderPermissions.MerchantAccept)).RequireRateLimiting("merchant-orders-write")
            .WithName("AcceptMerchantOrder").Produces<MerchantOrderDetails>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/{orderId:guid}/reject", Reject).RequireAuthorization(Permission(OrderPermissions.MerchantReject)).RequireRateLimiting("merchant-orders-write")
            .WithName("RejectMerchantOrder").Produces<MerchantOrderDetails>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/{orderId:guid}/start-preparation", StartPreparation).RequireAuthorization(Permission(OrderPermissions.MerchantPrepare)).RequireRateLimiting("merchant-orders-write")
            .WithName("StartMerchantOrderPreparation").Produces<MerchantOrderDetails>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/{orderId:guid}/mark-ready", MarkReady).RequireAuthorization(Permission(OrderPermissions.MerchantReady)).RequireRateLimiting("merchant-orders-write")
            .WithName("MarkMerchantOrderReady").Produces<MerchantOrderDetails>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPut("/{orderId:guid}/preparation-time", UpdatePreparationTime).RequireAuthorization(Permission(OrderPermissions.MerchantPrepare)).RequireRateLimiting("merchant-orders-write")
            .WithName("UpdateMerchantOrderPreparationTime").Produces<MerchantOrderDetails>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        return endpoints;
    }

    private static Task<IResult> List([AsParameters] MerchantOrderQueryParameters query, HttpContext context, ICurrentUser user, IMerchantOrderService service, CancellationToken ct) => Execute(service.ListAsync(Actor(user, context), query, ct));
    private static Task<IResult> Get(Guid orderId, HttpContext context, ICurrentUser user, IMerchantOrderService service, CancellationToken ct) => Execute(service.GetAsync(Actor(user, context), orderId, ct));
    private static Task<IResult> History(Guid orderId, HttpContext context, ICurrentUser user, IMerchantOrderService service, CancellationToken ct) => Execute(service.HistoryAsync(Actor(user, context), orderId, ct));
    private static Task<IResult> Accept(Guid orderId, AcceptMerchantOrderRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, HttpContext context, ICurrentUser user, IMerchantOrderService service, CancellationToken ct) => Execute(service.AcceptAsync(Actor(user, context), orderId, request, idempotencyKey, ct));
    private static Task<IResult> Reject(Guid orderId, RejectMerchantOrderRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, HttpContext context, ICurrentUser user, IMerchantOrderService service, CancellationToken ct) => Execute(service.RejectAsync(Actor(user, context), orderId, request, idempotencyKey, ct));
    private static Task<IResult> StartPreparation(Guid orderId, MerchantOrderTransitionRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, HttpContext context, ICurrentUser user, IMerchantOrderService service, CancellationToken ct) => Execute(service.StartPreparationAsync(Actor(user, context), orderId, request, idempotencyKey, ct));
    private static Task<IResult> MarkReady(Guid orderId, MerchantOrderTransitionRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, HttpContext context, ICurrentUser user, IMerchantOrderService service, CancellationToken ct) => Execute(service.MarkReadyAsync(Actor(user, context), orderId, request, idempotencyKey, ct));
    private static Task<IResult> UpdatePreparationTime(Guid orderId, UpdatePreparationTimeRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, HttpContext context, ICurrentUser user, IMerchantOrderService service, CancellationToken ct) => Execute(service.UpdatePreparationTimeAsync(Actor(user, context), orderId, request, idempotencyKey, ct));

    private static MerchantOrderActor Actor(ICurrentUser user, HttpContext context) => new(user.UserId?.Value ?? Guid.Empty, context.TraceIdentifier);
    private static string Permission(string value) => AuthenticationPolicies.PermissionPrefix + value;
    private static async Task<IResult> Execute<T>(Task<OrderOperationResult<T>> operation)
    {
        OrderOperationResult<T> result = await operation;
        if (result.Value is not null) return Results.Ok(result.Value);
        int status = result.Status switch
        {
            OrderOperationStatus.NotFound => StatusCodes.Status404NotFound,
            OrderOperationStatus.Forbidden => StatusCodes.Status403Forbidden,
            OrderOperationStatus.Conflict => StatusCodes.Status409Conflict,
            OrderOperationStatus.Unprocessable => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(statusCode: status, title: "Merchant order operation failed.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }
}
