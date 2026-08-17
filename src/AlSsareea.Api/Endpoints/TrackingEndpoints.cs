using AlSsareea.Api.Security;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Tracking.Application;
using AlSsareea.Modules.Tracking.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AlSsareea.Api.Endpoints;

internal static class TrackingEndpoints
{
    public static IEndpointRouteBuilder MapTrackingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/tracking").WithTags("Tracking").RequireAuthorization();
        group.MapPost("/location", Update).RequireAuthorization(Permission(TrackingPermissions.UpdateSelf)).RequireRateLimiting("tracking-ingestion").WithMetadata(new RequestSizeLimitAttribute(16 * 1024)).WithName("UpdateDriverLocation").Produces<LocationUpdateResponse>();
        group.MapPost("/locations/batch", Batch).RequireAuthorization(Permission(TrackingPermissions.UpdateSelf)).RequireRateLimiting("tracking-ingestion").WithMetadata(new RequestSizeLimitAttribute(256 * 1024)).WithName("SynchronizeDriverLocations").Produces<LocationBatchResponse>();
        group.MapGet("/me/latest", GetMine).RequireAuthorization(Permission(TrackingPermissions.UpdateSelf)).WithName("GetMyLatestLocation").Produces<DriverLocationResponse>();
        group.MapGet("/drivers/{driverId:guid}/latest", GetLatest).RequireAuthorization(Permission(TrackingPermissions.Read)).WithName("GetLatestDriverLocation").Produces<DriverLocationResponse>();
        group.MapGet("/drivers/{driverId:guid}/history", GetHistory).RequireAuthorization(Permission(TrackingPermissions.ReadHistory)).WithName("GetDriverLocationHistory").Produces<DriverLocationHistoryResponse>();
        group.MapGet("/orders/{orderId:guid}/latest", GetOrderLatest).WithName("GetAuthorizedOrderDriverLocation").Produces<DriverLocationResponse>();
        return endpoints;
    }

    private static Task<IResult> Update(LocationUpdateRequest request, ICurrentUser user, ITrackingService service, CancellationToken ct) => Execute(service.UpdateAsync(Actor(user), request, ct));
    private static Task<IResult> Batch(LocationBatchRequest request, ICurrentUser user, ITrackingService service, CancellationToken ct) => Execute(service.BatchAsync(Actor(user), request, ct));
    private static Task<IResult> GetMine(ICurrentUser user, ITrackingService service, CancellationToken ct) => Execute(service.GetMineAsync(Actor(user), ct));
    private static Task<IResult> GetLatest(Guid driverId, ITrackingService service, CancellationToken ct) => Execute(service.GetLatestAsync(driverId, ct));
    private static Task<IResult> GetHistory(Guid driverId, DateTime fromUtc, DateTime toUtc, int page, int pageSize, ITrackingService service, CancellationToken ct) => Execute(service.GetHistoryAsync(driverId, fromUtc, toUtc, page, pageSize, ct));
    private static async Task<IResult> GetOrderLatest(Guid orderId, ICurrentUser user, ITrackingVisibilityProvider visibility, ITrackingService service, CancellationToken ct)
    {
        TrackingVisibility? decision = await visibility.ResolveOrderAsync(orderId, user.UserId?.Value ?? Guid.Empty, ct);
        return decision is null ? Results.NotFound() : await Execute(service.GetLatestAsync(decision.DriverId, ct));
    }
    private static TrackingActor Actor(ICurrentUser user) => new(user.UserId?.Value ?? Guid.Empty);
    private static string Permission(string value) => AuthenticationPolicies.PermissionPrefix + value;
    private static async Task<IResult> Execute<T>(Task<TrackingResult<T>> task)
    {
        TrackingResult<T> result = await task; if (result.Value is not null) return Results.Ok(result.Value);
        int status = result.Status switch { TrackingStatus.NotFound => 404, TrackingStatus.Forbidden => 403, TrackingStatus.Duplicate => 409, _ => 400 };
        return Results.Problem(statusCode: status, title: "Tracking operation failed.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }
}
