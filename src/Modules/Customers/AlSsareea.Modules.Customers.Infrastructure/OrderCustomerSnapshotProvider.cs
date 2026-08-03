using AlSsareea.Modules.Customers.Contracts;
using AlSsareea.Modules.Customers.Domain;
using AlSsareea.Modules.Customers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Customers.Infrastructure;

internal sealed class OrderCustomerSnapshotProvider(CustomersDbContext db) : IOrderCustomerSnapshotProvider
{
    public async Task<Guid?> GetCustomerIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.Customers.AsNoTracking().Where(x => x.UserId == userId && x.Status == CustomerStatus.Active).Select(x => (Guid?)x.Id.Value).SingleOrDefaultAsync(cancellationToken);

    public async Task<OrderCustomerSnapshotContract?> GetAsync(Guid userId, Guid addressId, CancellationToken cancellationToken = default)
    {
        Customer? customer = await db.Customers.AsNoTracking().Include(x => x.Addresses).Include(x => x.Preferences).SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        CustomerAddress? address = customer?.Addresses.SingleOrDefault(x => x.Id.Value == addressId);
        if (customer is null || customer.Status != CustomerStatus.Active || address is null) return null;
        string formatted = string.Join(", ", new[] { address.Street, address.BuildingNumber, address.Area, address.City }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return new(customer.Id.Value, customer.DisplayName, null, customer.Preferences.PreferredLanguage,
            new(address.Id.Value, address.Label, address.City, address.Area, address.Street, address.BuildingNumber, address.Floor, address.Apartment, address.DeliveryInstructions, address.Location?.Latitude, address.Location?.Longitude, address.PlaceId, formatted));
    }
}
