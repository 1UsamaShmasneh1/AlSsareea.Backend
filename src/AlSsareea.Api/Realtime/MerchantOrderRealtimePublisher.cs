using AlSsareea.Modules.Orders.Application;
using AlSsareea.Modules.Orders.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace AlSsareea.Api.Realtime;

internal sealed class MerchantOrderRealtimePublisher(IHubContext<MerchantOrdersHub> hub, ILogger<MerchantOrderRealtimePublisher> logger) : IMerchantOrderRealtimePublisher
{
    private static readonly Action<ILogger, Guid, string, Exception?> PublicationFailed = LoggerMessage.Define<Guid, string>(
        LogLevel.Warning,
        new EventId(1201, nameof(PublicationFailed)),
        "Merchant order realtime publication failed for OrderId {OrderId} and EventName {EventName}");

    public async Task PublishAsync(MerchantOrderRealtimeEvent notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await hub.Clients.Group(MerchantOrderGroups.Merchant(notification.MerchantId)).SendAsync(notification.EventName, notification, cancellationToken);
            if (notification.BranchId.HasValue)
                await hub.Clients.Group(MerchantOrderGroups.Branch(notification.BranchId.Value)).SendAsync(notification.EventName, notification, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            PublicationFailed(logger, notification.OrderId, notification.EventName, exception);
        }
    }
}
