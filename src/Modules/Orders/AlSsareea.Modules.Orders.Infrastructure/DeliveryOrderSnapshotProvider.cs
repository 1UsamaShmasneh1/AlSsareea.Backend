using AlSsareea.Modules.Orders.Contracts;
using AlSsareea.Modules.Orders.Domain;
using AlSsareea.Modules.Orders.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Orders.Infrastructure;

internal sealed class DeliveryOrderSnapshotProvider(OrdersDbContext db) : IDeliveryOrderSnapshotProvider
{
    public Task<DeliveryOrderSnapshot?> GetAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty) return Task.FromResult<DeliveryOrderSnapshot?>(null);
        OrderId id = new(orderId);
        return db.Orders.AsNoTracking().Where(x => x.Id == id).Select(x => new DeliveryOrderSnapshot(
            x.Id.Value, x.CustomerId, x.MerchantId, x.MerchantBranchId, (short)x.Type, (short)x.Status,
            x.Status == OrderStatus.ReadyForPickup || x.Status == OrderStatus.SearchingForDriver || x.Status == OrderStatus.DriverAssigned,
            x.Merchant.BranchAddress ?? x.Merchant.MerchantDisplayName,
            x.Merchant.BranchDisplayName ?? x.Merchant.MerchantDisplayName,
            x.Merchant.BranchPhoneNumber, null, null, null,
            x.DeliveryAddress.AddressId,
            x.DeliveryAddress.FormattedAddress ?? (x.DeliveryAddress.Street + ", " + x.DeliveryAddress.City),
            x.Customer.DisplayName, x.Customer.PhoneNumber, x.DeliveryAddress.Floor, x.DeliveryAddress.DeliveryInstructions,
            x.DeliveryAddress.Latitude, x.DeliveryAddress.Longitude)).SingleOrDefaultAsync(cancellationToken);
    }
}
