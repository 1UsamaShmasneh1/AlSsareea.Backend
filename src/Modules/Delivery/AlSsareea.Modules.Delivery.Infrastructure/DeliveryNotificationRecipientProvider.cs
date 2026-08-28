using AlSsareea.Modules.Customers.Contracts;
using AlSsareea.Modules.Delivery.Contracts;
using AlSsareea.Modules.Delivery.Domain;
using AlSsareea.Modules.Delivery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Delivery.Infrastructure;

internal sealed class DeliveryNotificationRecipientProvider(DeliveryDbContext db, ICustomerNotificationRecipientProvider customers) : IDeliveryNotificationRecipientProvider
{
    public async Task<DeliveryNotificationRecipient?> GetAsync(Guid deliveryId, CancellationToken ct = default)
    {
        Guid? customerId = await db.Deliveries.AsNoTracking().Where(x => x.Id == new DeliveryId(deliveryId)).Select(x => (Guid?)x.CustomerId).SingleOrDefaultAsync(ct); if (customerId is null) return null; CustomerNotificationRecipient? recipient = await customers.GetAsync(customerId.Value, ct); return recipient is null ? null : new(recipient.UserId, recipient.Language);
    }
}
