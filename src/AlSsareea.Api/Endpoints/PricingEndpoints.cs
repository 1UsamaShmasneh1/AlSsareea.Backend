using AlSsareea.Api.Security;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Pricing.Application;
using AlSsareea.Modules.Pricing.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AlSsareea.Api.Endpoints;

public static class PricingEndpoints
{
    public static IEndpointRouteBuilder MapPricingEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/pricing").WithTags("Pricing");
        group.MapPost("/estimates", Estimate)
            .RequireAuthorization(Permission(PricingPermissions.Calculate))
            .RequireRateLimiting("pricing-calculate");

        RouteGroupBuilder policies = group.MapGroup("/policies");
        policies.MapPost("", Create).RequireAuthorization(Permission(PricingPermissions.Manage)).RequireRateLimiting("pricing-write");
        policies.MapGet("", List).RequireAuthorization(Permission(PricingPermissions.View)).RequireRateLimiting("pricing-read");
        policies.MapGet("/{policyId:guid}", Get).RequireAuthorization(Permission(PricingPermissions.View)).RequireRateLimiting("pricing-read");
        policies.MapPut("/{policyId:guid}", Update).RequireAuthorization(Permission(PricingPermissions.Manage)).RequireRateLimiting("pricing-write");
        policies.MapPut("/{policyId:guid}/rules", ReplaceRules).RequireAuthorization(Permission(PricingPermissions.Manage)).RequireRateLimiting("pricing-write");
        policies.MapPost("/{policyId:guid}/activate", (Guid policyId, PricingPolicyActionRequest request, ICurrentUser current, IPricingService service, CancellationToken ct) =>
            ChangeStatus(policyId, "activate", request, current, service, ct)).RequireAuthorization(Permission(PricingPermissions.Manage)).RequireRateLimiting("pricing-write");
        policies.MapPost("/{policyId:guid}/deactivate", (Guid policyId, PricingPolicyActionRequest request, ICurrentUser current, IPricingService service, CancellationToken ct) =>
            ChangeStatus(policyId, "deactivate", request, current, service, ct)).RequireAuthorization(Permission(PricingPermissions.Manage)).RequireRateLimiting("pricing-write");
        policies.MapPost("/{policyId:guid}/archive", (Guid policyId, PricingPolicyActionRequest request, ICurrentUser current, IPricingService service, CancellationToken ct) =>
            ChangeStatus(policyId, "archive", request, current, service, ct)).RequireAuthorization(Permission(PricingPermissions.Manage)).RequireRateLimiting("pricing-write");
        return app;
    }

    private static Task<IResult> Create(
        [FromBody] CreatePricingPolicyRequest request,
        [FromServices] ICurrentUser current,
        [FromServices] IPricingService service,
        CancellationToken cancellationToken) =>
        Result(service.CreatePolicyAsync(request, Actor(current), cancellationToken));

    private static Task<IResult> Update(
        [FromRoute] Guid policyId,
        [FromBody] UpdatePricingPolicyRequest request,
        [FromServices] ICurrentUser current,
        [FromServices] IPricingService service,
        CancellationToken cancellationToken) =>
        Result(service.UpdatePolicyAsync(policyId, request, Actor(current), cancellationToken));

    private static Task<IResult> ReplaceRules(
        [FromRoute] Guid policyId,
        [FromBody] ReplacePricingRulesRequest request,
        [FromServices] ICurrentUser current,
        [FromServices] IPricingService service,
        CancellationToken cancellationToken) =>
        Result(service.ReplaceRulesAsync(policyId, request, Actor(current), cancellationToken));

    private static Task<IResult> Get(
        [FromRoute] Guid policyId,
        [FromServices] ICurrentUser current,
        [FromServices] IPricingService service,
        CancellationToken cancellationToken) =>
        Result(service.GetPolicyAsync(policyId, Actor(current), cancellationToken));

    private static Task<IResult> List(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] short? status,
        [FromQuery] Guid? merchantId,
        [FromQuery] Guid? branchId,
        [FromServices] ICurrentUser current,
        [FromServices] IPricingService service,
        CancellationToken cancellationToken) =>
        Result(service.ListPoliciesAsync(page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize, status, merchantId, branchId, Actor(current), cancellationToken));

    private static Task<IResult> Estimate(
        [FromBody] PricingEstimateRequest request,
        [FromServices] ICurrentUser current,
        [FromServices] IPricingService service,
        CancellationToken cancellationToken) =>
        Result(service.EstimateAsync(request, Actor(current), cancellationToken));

    private static Task<IResult> ChangeStatus(
        Guid policyId,
        string operation,
        PricingPolicyActionRequest request,
        ICurrentUser current,
        IPricingService service,
        CancellationToken cancellationToken) =>
        Result(service.ChangeStatusAsync(policyId, operation, request, Actor(current), cancellationToken));

    private static PricingActor Actor(ICurrentUser current) =>
        new(current.UserId?.Value ?? Guid.Empty, current.Roles.Any(x => x is "admin" or "platform-admin" or "operations"));

    private static string Permission(string value) => AuthenticationPolicies.PermissionPrefix + value;

    private static async Task<IResult> Result<T>(Task<PricingOperationResult<T>> task)
    {
        PricingOperationResult<T> result = await task;
        return result.Status switch
        {
            PricingOperationStatus.Success => Results.Ok(result.Value),
            PricingOperationStatus.Created => Results.Json(result.Value, statusCode: StatusCodes.Status201Created),
            PricingOperationStatus.NotFound => Problem(StatusCodes.Status404NotFound, result.ErrorCode),
            PricingOperationStatus.Forbidden => Problem(StatusCodes.Status403Forbidden, result.ErrorCode),
            PricingOperationStatus.Conflict => Problem(StatusCodes.Status409Conflict, result.ErrorCode),
            _ => Problem(StatusCodes.Status400BadRequest, result.ErrorCode),
        };
    }

    private static IResult Problem(int status, string? code) =>
        Results.Problem(
            statusCode: status,
            title: status switch
            {
                StatusCodes.Status403Forbidden => "Forbidden",
                StatusCodes.Status404NotFound => "Not found",
                StatusCodes.Status409Conflict => "Conflict",
                _ => "Invalid request",
            },
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
