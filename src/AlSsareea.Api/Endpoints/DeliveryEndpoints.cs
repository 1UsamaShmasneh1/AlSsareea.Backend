using AlSsareea.Api.Security;
using AlSsareea.Modules.Delivery.Application;
using AlSsareea.Modules.Delivery.Contracts;
using AlSsareea.Modules.Identity.Application;

namespace AlSsareea.Api.Endpoints;

internal static class DeliveryEndpoints
{
    public static IEndpointRouteBuilder MapDeliveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/deliveries").WithTags("Delivery").RequireAuthorization();
        group.MapPost("/", Create).RequireAuthorization(Permission(DeliveryPermissions.Manage)).WithName("CreateDelivery").Produces<DeliveryCreatedResponse>(201);
        group.MapPost("/{deliveryId:guid}/assign", Assign).RequireAuthorization(Permission(DeliveryPermissions.Manage)).WithName("AssignDelivery").Produces<DeliveryResponse>();
        group.MapGet("/{deliveryId:guid}", GetForCustomer).RequireAuthorization(Permission(DeliveryPermissions.ReadOwn)).WithName("GetOwnDelivery").Produces<DeliveryResponse>();
        group.MapGet("/current", GetCurrentForCustomer).RequireAuthorization(Permission(DeliveryPermissions.ReadOwn)).WithName("GetCurrentCustomerDelivery").Produces<DeliveryResponse>();
        group.MapGet("/driver/current", GetCurrentForDriver).RequireAuthorization(Permission(DeliveryPermissions.ReadSelf)).WithName("GetCurrentDriverDelivery").Produces<DeliveryResponse>();
        MapTransition(group, "heading-to-pickup", "BeginHeadingToPickup");
        MapTransition(group, "arrive-at-pickup", "ArriveAtPickup");
        MapTransition(group, "confirm-pickup", "ConfirmPickup");
        MapTransition(group, "start", "StartDelivery");
        MapTransition(group, "arrive-at-drop-off", "ArriveAtDropOff");
        MapTransition(group, "complete", "CompleteDelivery");
        group.MapPost("/{deliveryId:guid}/proofs", SubmitProof).RequireAuthorization(Permission(DeliveryPermissions.OperateSelf)).WithName("SubmitDeliveryProof").Produces<DeliveryResponse>();
        group.MapPost("/{deliveryId:guid}/fail", ReportFailed).RequireAuthorization(Permission(DeliveryPermissions.OperateSelf)).WithName("ReportFailedDelivery").Produces<DeliveryResponse>();
        return endpoints;
    }

    private static void MapTransition(RouteGroupBuilder group, string operation, string name) =>
        group.MapPost($"/{{deliveryId:guid}}/{operation}", (Guid deliveryId, DeliveryTransitionRequest request, HttpContext context, ICurrentUser user, IDeliveryService service, CancellationToken ct) => Execute(service.TransitionAsync(Actor(user, context), deliveryId, operation, request, IdempotencyKey(context), ct)))
            .RequireAuthorization(Permission(DeliveryPermissions.OperateSelf)).WithName(name).Produces<DeliveryResponse>();

    private static Task<IResult> Create(CreateDeliveryRequest request, HttpContext context, ICurrentUser user, IDeliveryService service, CancellationToken ct) => ExecuteCreated(service.CreateAsync(Actor(user, context), request, IdempotencyKey(context), ct));
    private static Task<IResult> Assign(Guid deliveryId, AssignDeliveryRequest request, HttpContext context, ICurrentUser user, IDeliveryService service, CancellationToken ct) => Execute(service.AssignAsync(Actor(user, context), deliveryId, request, IdempotencyKey(context), ct));
    private static Task<IResult> GetForCustomer(Guid deliveryId, HttpContext context, ICurrentUser user, IDeliveryService service, CancellationToken ct) => Execute(service.GetForCustomerAsync(Actor(user, context), deliveryId, ct));
    private static Task<IResult> GetCurrentForCustomer(HttpContext context, ICurrentUser user, IDeliveryService service, CancellationToken ct) => Execute(service.GetCurrentForCustomerAsync(Actor(user, context), ct));
    private static Task<IResult> GetCurrentForDriver(HttpContext context, ICurrentUser user, IDeliveryService service, CancellationToken ct) => Execute(service.GetCurrentForDriverAsync(Actor(user, context), ct));
    private static Task<IResult> SubmitProof(Guid deliveryId, SubmitDeliveryProofRequest request, HttpContext context, ICurrentUser user, IDeliveryService service, CancellationToken ct) => Execute(service.SubmitProofAsync(Actor(user, context), deliveryId, request, IdempotencyKey(context), ct));
    private static Task<IResult> ReportFailed(Guid deliveryId, ReportFailedDeliveryRequest request, HttpContext context, ICurrentUser user, IDeliveryService service, CancellationToken ct) => Execute(service.ReportFailedAsync(Actor(user, context), deliveryId, request, IdempotencyKey(context), ct));
    private static DeliveryActor Actor(ICurrentUser user, HttpContext context) => new(user.UserId?.Value ?? Guid.Empty, context.TraceIdentifier);
    private static string IdempotencyKey(HttpContext context) => context.Request.Headers["Idempotency-Key"].ToString();
    private static string Permission(string value) => AuthenticationPolicies.PermissionPrefix + value;

    private static async Task<IResult> Execute<T>(Task<DeliveryOperationResult<T>> task)
    {
        DeliveryOperationResult<T> result = await task;
        if (result.Value is not null) return Results.Ok(result.Value);
        int status = result.Status switch { DeliveryOperationStatus.NotFound => 404, DeliveryOperationStatus.Forbidden => 403, DeliveryOperationStatus.Conflict => 409, DeliveryOperationStatus.Unprocessable => 422, _ => 400 };
        return Results.Problem(statusCode: status, title: "Delivery operation failed.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }

    private static async Task<IResult> ExecuteCreated(Task<DeliveryOperationResult<DeliveryCreatedResponse>> task)
    {
        DeliveryOperationResult<DeliveryCreatedResponse> result = await task;
        if (result.Value is not null) return result.Status == DeliveryOperationStatus.Created ? Results.Created($"/api/v1/deliveries/{result.Value.Delivery.Id}", result.Value) : Results.Ok(result.Value);
        int status = result.Status switch { DeliveryOperationStatus.NotFound => 404, DeliveryOperationStatus.Forbidden => 403, DeliveryOperationStatus.Conflict => 409, DeliveryOperationStatus.Unprocessable => 422, _ => 400 };
        return Results.Problem(statusCode: status, title: "Delivery operation failed.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }
}
