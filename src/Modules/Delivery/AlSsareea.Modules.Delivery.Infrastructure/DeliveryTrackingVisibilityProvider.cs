using AlSsareea.Modules.Delivery.Domain;
using AlSsareea.Modules.Delivery.Infrastructure.Persistence;
using AlSsareea.Modules.Tracking.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Delivery.Infrastructure;

internal sealed class DeliveryTrackingVisibilityProvider(DeliveryDbContext db) : ITrackingVisibilityProvider
{
    private static readonly DeliveryStatus[] VisibleStatuses = [DeliveryStatus.PickedUp, DeliveryStatus.InTransit, DeliveryStatus.ArrivedAtDropOff];

    public Task<TrackingVisibility?> ResolveOrderAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty || userId == Guid.Empty) return Task.FromResult<TrackingVisibility?>(null);
        return db.Deliveries.AsNoTracking()
            .Where(x => x.OrderId == orderId && x.CustomerUserId == userId && x.DriverId != null && VisibleStatuses.Contains(x.Status))
            .Select(x => new TrackingVisibility(x.DriverId!.Value, $"tracking:order:{orderId}"))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
