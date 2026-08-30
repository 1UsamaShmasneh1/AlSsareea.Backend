using AlSsareea.Modules.Drivers.Contracts;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Tracking.Application;
using AlSsareea.Modules.Tracking.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AlSsareea.Api.Realtime;

public static class TrackingGroups
{
    public const string Operations = "tracking:operations";
    public static string Driver(Guid driverId) => $"tracking:driver:{driverId:N}";
    public static string Order(Guid orderId) => $"tracking:order:{orderId:N}";
}

[Authorize]
public sealed class TrackingHub(IDriverOperationalSnapshotProvider drivers, ITrackingVisibilityProvider visibility, ICurrentUser currentUser) : Hub
{
    public async Task SubscribeSelf()
    {
        DriverEligibilitySnapshot? driver = await drivers.GetByUserAsync(currentUser.UserId?.Value ?? Guid.Empty, Context.ConnectionAborted);
        if (driver is null) throw new HubException("tracking_scope_denied");
        await Groups.AddToGroupAsync(Context.ConnectionId, TrackingGroups.Driver(driver.DriverId), Context.ConnectionAborted);
    }
    public async Task SubscribeOperations()
    {
        if (!currentUser.Permissions.Contains(TrackingPermissions.RealtimeOperations)) throw new HubException("tracking_scope_denied");
        await Groups.AddToGroupAsync(Context.ConnectionId, TrackingGroups.Operations, Context.ConnectionAborted);
    }
    public async Task SubscribeOrder(Guid orderId)
    {
        TrackingVisibility? decision = await visibility.ResolveOrderAsync(orderId, currentUser.UserId?.Value ?? Guid.Empty, Context.ConnectionAborted);
        if (decision is null) throw new HubException("tracking_scope_denied");
        await Groups.AddToGroupAsync(Context.ConnectionId, TrackingGroups.Order(orderId), Context.ConnectionAborted);
    }
}

internal sealed class TrackingRealtimePublisher(IHubContext<TrackingHub> hub, ITrackingOrderAudienceProvider audiences, ILogger<TrackingRealtimePublisher> logger) : ILocationRealtimePublisher
{
    private static readonly Action<ILogger, Guid, Exception?> PublicationFailed = LoggerMessage.Define<Guid>(LogLevel.Warning, new EventId(1401, nameof(PublicationFailed)), "Tracking realtime publication failed for DriverId {DriverId}");

    public async Task PublishAsync(Guid driverId, TrackingRealtimePayload payload, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.Group(TrackingGroups.Driver(driverId)).SendAsync("LocationUpdated", payload, cancellationToken);
            await hub.Clients.Group(TrackingGroups.Operations).SendAsync("LocationUpdated", new { DriverId = driverId, Location = payload }, cancellationToken);
            IReadOnlyList<Guid> orderIds = await audiences.GetVisibleOrderIdsForDriverAsync(driverId, cancellationToken);
            foreach (Guid orderId in orderIds)
                await hub.Clients.Group(TrackingGroups.Order(orderId)).SendAsync("LocationUpdated", payload, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException) { PublicationFailed(logger, driverId, exception); }
    }
}
