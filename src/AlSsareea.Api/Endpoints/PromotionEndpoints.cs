using AlSsareea.Api.Security;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Promotions.Application;
using AlSsareea.Modules.Promotions.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AlSsareea.Api.Endpoints;

public static class PromotionEndpoints
{
    public static IEndpointRouteBuilder MapPromotionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder root = endpoints.MapGroup("/api/v1/promotions").WithTags("Promotions").RequireAuthorization();
        root.MapPost("/", Create).RequireAuthorization(Permission(PromotionPermissions.Create));
        root.MapGet("/", List).RequireAuthorization(Permission(PromotionPermissions.View));
        root.MapGet("/{promotionId:guid}", Get).RequireAuthorization(Permission(PromotionPermissions.View));
        root.MapPut("/{promotionId:guid}", Update).RequireAuthorization(Permission(PromotionPermissions.Update));
        root.MapPost("/{promotionId:guid}/activate", Activate).RequireAuthorization(Permission(PromotionPermissions.Activate));
        root.MapPost("/{promotionId:guid}/suspend", Suspend).RequireAuthorization(Permission(PromotionPermissions.Suspend));
        root.MapPost("/{promotionId:guid}/archive", Archive).RequireAuthorization(Permission(PromotionPermissions.Archive));
        root.MapPost("/evaluate", Evaluate).RequireAuthorization(Permission(PromotionPermissions.Evaluate));
        root.MapPost("/coupons/validate", ValidateCoupon).RequireAuthorization(Permission(PromotionPermissions.Evaluate));
        root.MapPost("/redemptions", RecordRedemption).RequireAuthorization(Permission(PromotionPermissions.RecordUsage));
        root.MapGet("/{promotionId:guid}/usage", Usage).RequireAuthorization(Permission(PromotionPermissions.ViewUsage));
        return endpoints;
    }

    private static Task<IResult> Create(CreatePromotionRequest request, ICurrentUser current, IPromotionsService service, CancellationToken ct) => Run(service.CreateAsync(request, Actor(current), ct));
    private static Task<IResult> Get(Guid promotionId, ICurrentUser current, IPromotionsService service, CancellationToken ct) => Run(service.GetAsync(promotionId, Actor(current), ct));
    private static Task<IResult> Update(Guid promotionId, UpdatePromotionRequest request, ICurrentUser current, IPromotionsService service, CancellationToken ct) => Run(service.UpdateAsync(promotionId, request, Actor(current), ct));
    private static Task<IResult> Activate(Guid promotionId, PromotionActionRequest request, ICurrentUser current, IPromotionsService service, CancellationToken ct) => Run(service.ActivateAsync(promotionId, request, Actor(current), ct));
    private static Task<IResult> Suspend(Guid promotionId, PromotionActionRequest request, ICurrentUser current, IPromotionsService service, CancellationToken ct) => Run(service.SuspendAsync(promotionId, request, Actor(current), ct));
    private static Task<IResult> Archive(Guid promotionId, PromotionActionRequest request, ICurrentUser current, IPromotionsService service, CancellationToken ct) => Run(service.ArchiveAsync(promotionId, request, Actor(current), ct));
    private static Task<IResult> Evaluate(EvaluatePromotionsRequest request, ICurrentUser current, IPromotionsService service, CancellationToken ct) => Run(service.EvaluateAsync(request, Actor(current), ct));
    private static Task<IResult> ValidateCoupon(ValidateCouponRequest request, ICurrentUser current, IPromotionsService service, CancellationToken ct) => Run(service.ValidateCouponAsync(request, Actor(current), ct));
    private static Task<IResult> RecordRedemption(RecordRedemptionRequest request, ICurrentUser current, IPromotionsService service, CancellationToken ct) => Run(service.RecordRedemptionAsync(request, Actor(current), ct));
    private static Task<IResult> Usage(Guid promotionId, [FromQuery] Guid? customerId, ICurrentUser current, IPromotionsService service, CancellationToken ct) => Run(service.GetUsageAsync(promotionId, customerId, Actor(current), ct));
    private static Task<IResult> List(
        [FromQuery] int page, [FromQuery] int pageSize, [FromQuery] short? status, [FromQuery] short? type,
        [FromQuery] Guid? merchantId, [FromQuery] Guid? branchId, [FromQuery] string? couponCode, [FromQuery] DateTime? validAtUtc,
        ICurrentUser current, IPromotionsService service, CancellationToken ct) =>
        Run(service.ListAsync(page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize, status, type, merchantId, branchId, couponCode, validAtUtc, Actor(current), ct));

    private static PromotionActor Actor(ICurrentUser current)
    {
        bool platform = current.Roles.Any(x =>
            x.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("platform-admin", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("operations", StringComparison.OrdinalIgnoreCase));
        return new(current.UserId?.Value ?? Guid.Empty, platform, new HashSet<Guid>());
    }
    private static string Permission(string permission) => AuthenticationPolicies.PermissionPrefix + permission;
    private static async Task<IResult> Run<T>(Task<PromotionOperationResult<T>> operation)
    {
        PromotionOperationResult<T> result = await operation;
        return result.Status switch
        {
            PromotionOperationStatus.Success => Results.Ok(result.Value),
            PromotionOperationStatus.Created => Results.Json(result.Value, statusCode: StatusCodes.Status201Created),
            PromotionOperationStatus.NotFound => Problem(404, result.ErrorCode),
            PromotionOperationStatus.Forbidden => Problem(403, result.ErrorCode),
            PromotionOperationStatus.Conflict => Problem(409, result.ErrorCode),
            _ => Problem(400, result.ErrorCode),
        };
    }
    private static IResult Problem(int status, string? code) => Results.Problem(
        statusCode: status,
        title: status switch { 403 => "Forbidden", 404 => "Not found", 409 => "Conflict", _ => "Invalid request" },
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
