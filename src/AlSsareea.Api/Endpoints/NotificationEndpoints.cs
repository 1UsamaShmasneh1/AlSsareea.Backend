using AlSsareea.Api.Security;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Notifications.Application;
using AlSsareea.Modules.Notifications.Contracts;

namespace AlSsareea.Api.Endpoints;

internal static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/notifications").WithTags("Notifications").RequireAuthorization();
        group.MapGet("/", List).RequireRateLimiting("notifications-read").WithName("ListOwnNotifications").Produces<NotificationListResponse>();
        group.MapPost("/{notificationId:guid}/read", MarkRead).RequireRateLimiting("notifications-write").WithName("MarkNotificationRead");
        group.MapPost("/read-all", MarkAllRead).RequireRateLimiting("notifications-write").WithName("MarkAllNotificationsRead");
        group.MapPost("/devices", RegisterDevice).RequireRateLimiting("notifications-write").WithName("RegisterNotificationDevice").Produces<DeviceTokenResponse>();
        group.MapDelete("/devices/{deviceTokenId:guid}", UnregisterDevice).RequireRateLimiting("notifications-write").WithName("UnregisterNotificationDevice");
        group.MapGet("/preferences", GetPreferences).RequireRateLimiting("notifications-read").WithName("GetNotificationPreferences").Produces<NotificationPreferencesResponse>();
        group.MapPut("/preferences", UpdatePreferences).RequireRateLimiting("notifications-write").WithName("UpdateNotificationPreferences").Produces<NotificationPreferencesResponse>();
        return endpoints;
    }
    private static Task<NotificationListResponse> List(int? page, int? pageSize, ICurrentUser user, INotificationService service, CancellationToken ct) => service.ListAsync(User(user), page ?? 1, pageSize ?? 20, ct);
    private static Task<IResult> MarkRead(Guid notificationId, ICurrentUser user, INotificationService service, CancellationToken ct) => Result(service.MarkReadAsync(User(user), notificationId, ct));
    private static async Task<IResult> MarkAllRead(ICurrentUser user, INotificationService service, CancellationToken ct) => Results.Ok(new { updated = await service.MarkAllReadAsync(User(user), ct) });
    private static Task<IResult> RegisterDevice(RegisterDeviceTokenRequest request, ICurrentUser user, INotificationService service, CancellationToken ct) => Result(service.RegisterDeviceAsync(User(user), request, ct));
    private static Task<IResult> UnregisterDevice(Guid deviceTokenId, ICurrentUser user, INotificationService service, CancellationToken ct) => Result(service.UnregisterDeviceAsync(User(user), deviceTokenId, ct));
    private static Task<NotificationPreferencesResponse> GetPreferences(ICurrentUser user, INotificationService service, CancellationToken ct) => service.GetPreferencesAsync(User(user), ct);
    private static Task<IResult> UpdatePreferences(UpdateNotificationPreferencesRequest request, ICurrentUser user, INotificationService service, CancellationToken ct) => Result(service.UpdatePreferencesAsync(User(user), request, ct));
    private static Guid User(ICurrentUser currentUser) => currentUser.UserId?.Value ?? Guid.Empty;
    private static async Task<IResult> Result<T>(Task<NotificationOperationResult<T>> task)
    {
        NotificationOperationResult<T> result = await task; if (result.Status is NotificationOperationStatus.Ok or NotificationOperationStatus.Created) return result.Status == NotificationOperationStatus.Created ? Results.Json(result.Value, statusCode: StatusCodes.Status201Created) : Results.Ok(result.Value);
        int status = result.Status switch { NotificationOperationStatus.NotFound => 404, NotificationOperationStatus.Forbidden => 403, NotificationOperationStatus.Conflict => 409, _ => 400 }; return Results.Problem(statusCode: status, title: "Notification operation failed.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }
}
