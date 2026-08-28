using AlSsareea.Api.Security;
using AlSsareea.Modules.Dispatching.Application;
using AlSsareea.Modules.Dispatching.Contracts;
using AlSsareea.Modules.Identity.Application;

namespace AlSsareea.Api.Endpoints;

internal static class DispatchingEndpoints
{
    public static IEndpointRouteBuilder MapDispatchingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/dispatching").WithTags("Dispatching").RequireAuthorization();
        group.MapPost("/requests", Start).RequireAuthorization(Permission(DispatchingPermissions.Start)).RequireRateLimiting("dispatching-write").WithName("StartDispatch").Produces<DispatchResponse>(201);
        group.MapGet("/requests/{requestId:guid}", Get).RequireAuthorization(Permission(DispatchingPermissions.Read)).WithName("GetDispatch").Produces<DispatchResponse>();
        group.MapPost("/requests/{requestId:guid}/offers/{offerId:guid}/accept", Accept).RequireRateLimiting("dispatching-write").WithName("AcceptDispatchOffer").Produces<DispatchResponse>();
        group.MapPost("/requests/{requestId:guid}/offers/{offerId:guid}/decline", Decline).RequireRateLimiting("dispatching-write").WithName("DeclineDispatchOffer").Produces<DispatchResponse>();
        group.MapPost("/requests/{requestId:guid}/retry", Retry).RequireAuthorization(Permission(DispatchingPermissions.Manage)).RequireRateLimiting("dispatching-write").WithName("RetryDispatch").Produces<DispatchResponse>();
        group.MapPost("/requests/{requestId:guid}/cancel", Cancel).RequireAuthorization(Permission(DispatchingPermissions.Cancel)).RequireRateLimiting("dispatching-write").WithName("CancelDispatch").Produces<DispatchResponse>();
        group.MapPost("/requests/{requestId:guid}/manual-assignment", ManualAssign).RequireAuthorization(Permission(DispatchingPermissions.ManualAssign)).RequireRateLimiting("dispatching-write").WithName("ManuallyAssignDispatch").Produces<DispatchResponse>();
        return endpoints;
    }
    private static Task<IResult> Start(StartDispatchRequest request, HttpContext context, ICurrentUser user, IDispatchService service, CancellationToken ct) => Execute(service.StartAsync(Actor(user, context), request, Key(context), ct), true);
    private static Task<IResult> Get(Guid requestId, IDispatchService service, CancellationToken ct) => Execute(service.GetAsync(requestId, ct));
    private static Task<IResult> Accept(Guid requestId, Guid offerId, HttpContext context, ICurrentUser user, IDispatchService service, CancellationToken ct) => Execute(service.AcceptAsync(Actor(user, context), requestId, offerId, Key(context), ct));
    private static Task<IResult> Decline(Guid requestId, Guid offerId, OfferDecisionRequest request, HttpContext context, ICurrentUser user, IDispatchService service, CancellationToken ct) => Execute(service.DeclineAsync(Actor(user, context), requestId, offerId, request, Key(context), ct));
    private static Task<IResult> Retry(Guid requestId, RetryDispatchRequest request, HttpContext context, ICurrentUser user, IDispatchService service, CancellationToken ct) => Execute(service.RetryAsync(Actor(user, context), requestId, request, Key(context), ct));
    private static Task<IResult> Cancel(Guid requestId, CancelDispatchRequest request, HttpContext context, ICurrentUser user, IDispatchService service, CancellationToken ct) => Execute(service.CancelAsync(Actor(user, context), requestId, request, Key(context), ct));
    private static Task<IResult> ManualAssign(Guid requestId, ManualAssignDispatchRequest request, HttpContext context, ICurrentUser user, IDispatchService service, CancellationToken ct) => Execute(service.ManualAssignAsync(Actor(user, context), requestId, request, Key(context), ct));
    private static DispatchActor Actor(ICurrentUser user, HttpContext context) => new(user.UserId?.Value ?? Guid.Empty, context.TraceIdentifier);
    private static string Key(HttpContext context) => context.Request.Headers["Idempotency-Key"].ToString();
    private static string Permission(string value) => AuthenticationPolicies.PermissionPrefix + value;
    private static async Task<IResult> Execute(Task<DispatchOperationResult<DispatchResponse>> task, bool created = false)
    {
        DispatchOperationResult<DispatchResponse> result = await task; if (result.Value is not null) return created && result.Status == DispatchOperationStatus.Created ? Results.Created($"/api/v1/dispatching/requests/{result.Value.Id}", result.Value) : Results.Ok(result.Value);
        int status = result.Status switch { DispatchOperationStatus.NotFound => 404, DispatchOperationStatus.Forbidden => 403, DispatchOperationStatus.Conflict => 409, DispatchOperationStatus.Unprocessable => 422, _ => 400 }; return Results.Problem(statusCode: status, title: "Dispatch operation failed.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }
}
